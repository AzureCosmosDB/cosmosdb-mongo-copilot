using MongoDB.Bson;
using MongoDB.Driver;
using Search.Models;
using MongoDB.Bson.Serialization;
using Newtonsoft.Json.Linq;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using Microsoft.ML.Tokenizers;

namespace Search.Services;

/// <summary>
/// Service to access Azure Cosmos DB for Mongo vCore.
/// </summary>
public class MongoDbService
{
    private readonly MongoClient _client;
    private readonly IMongoDatabase _database;

    private readonly IMongoCollection<Product> _products;
    private readonly IMongoCollection<Customer> _customers;
    private readonly IMongoCollection<SalesOrder> _salesOrders;
    private readonly IMongoCollection<Session> _sessions;
    private readonly IMongoCollection<Message> _messages;
    private readonly string _vectorIndexType;
    private readonly int _maxVectorSearchResults = default;

    private readonly Tokenizer _tokenizer = Tokenizer.CreateTiktokenForModel("gpt-3.5-turbo");
    private readonly SemanticKernelService _semanticKernelService;
    private readonly ILogger _logger;

    private int counter = 0;

    public int incCounter()
    {
        counter = counter++;
        return counter;
    }


    /// <summary>
    /// Creates a new instance of the service.
    /// </summary>
    /// <param name="endpoint">Endpoint URI.</param>
    /// <param name="key">Account key.</param>
    /// <param name="databaseName">Name of the database to access.</param>
    /// <param name="collectionNames">Names of the collections for this retail sample.</param>
    /// <exception cref="ArgumentNullException">Thrown when endpoint, key, databaseName, or collectionNames is either null or empty.</exception>
    /// <remarks>
    /// This constructor will validate credentials and create a service client instance.
    /// </remarks>
    public MongoDbService(string connection, string databaseName, string collectionNames, string maxVectorSearchResults, string vectorIndexType, SemanticKernelService semanticKernelService, ILogger logger)
    {

        Console.WriteLine("Starting MongoDBService ");

        ArgumentException.ThrowIfNullOrEmpty(connection);
        ArgumentException.ThrowIfNullOrEmpty(databaseName);
        ArgumentException.ThrowIfNullOrEmpty(collectionNames);
        ArgumentException.ThrowIfNullOrEmpty(maxVectorSearchResults);
        ArgumentException.ThrowIfNullOrEmpty(vectorIndexType);

        _semanticKernelService = semanticKernelService;
        _logger = logger;

        _client = new MongoClient(connection);
        _database = _client.GetDatabase(databaseName);
        _maxVectorSearchResults = int.TryParse(maxVectorSearchResults, out _maxVectorSearchResults) ? _maxVectorSearchResults : 10;
        _vectorIndexType = vectorIndexType;

        _products = _database.GetCollection<Product>("products");
        _customers = _database.GetCollection<Customer>("customers");
        _salesOrders = _database.GetCollection<SalesOrder>("salesOrders");
        _sessions = _database.GetCollection<Session>("completions");
        _messages = _database.GetCollection<Message>("completions");

    }

    /// <summary>
    /// Perform a vector search on the collection.
    /// </summary>
    /// <param name="collectionName">Name of the collection to execute the vector search.</param>
    /// <param name="embeddings">vectors to use in the vector search.</param>
    /// <param name="path"> property path of the embeddings vector </param>
    /// <param name="maxTokens"> property path of the embeddings vector </param>
    /// <returns>string payload of documents returned from the vector query</returns>
    public async Task<string> VectorSearchAsync(string collectionName, string path, float[] embeddings, int maxTokens)
    {

        try
        {
            string resultDocuments = "["; // string.Empty;

            // Connect to collection
            IMongoCollection<BsonDocument> collection = _database.GetCollection<BsonDocument>(collectionName);

            // Convert embeddings to BSON array
            var embeddingsArray = new BsonArray(embeddings.Select(e => new BsonDouble(Convert.ToDouble(e))));

            // Define MongoDB pipeline query
            BsonDocument[] pipeline = new BsonDocument[]
            {
                new BsonDocument
                {
                    {
                        "$search", new BsonDocument
                        {
                            {
                                "cosmosSearch", new BsonDocument
                                {
                                    { "vector", embeddingsArray },
                                    { "path", path },
                                    { "k", _maxVectorSearchResults }
                                }
                            },
                            { "returnStoredSource", true }
                        }
                    }
                },
                new BsonDocument
                {
                    {
                        "$project", new BsonDocument
                        {
                            {"_id", 0 },
                            {path, 0 },
                        }
                    }
                }
            };

            // Execute query 
            List<BsonDocument> bsonDocuments = await collection.Aggregate<BsonDocument>(pipeline).ToListAsync();
            List<string> textDocuments = bsonDocuments.ConvertAll(bsonDocument => bsonDocument.ToString());

            // Tokenize and limit to maxTokens 
            var totalTokens = 0;

            foreach (var document in textDocuments)
            {
                var tokens = _tokenizer.CountTokens(document);
                if ((totalTokens + tokens) > maxTokens)
                {
                    break;
                }
                totalTokens += tokens;
                resultDocuments = resultDocuments + "," + document;
            }

            resultDocuments = resultDocuments + "]";

            return resultDocuments;
        }
        catch (MongoException ex)
        {
            _logger.LogError($"Exception: VectorSearchAsync(): {ex.Message}");
            throw;
        }


    }

    public void SetupCollections()
    {

        Console.WriteLine("creating database");
        _client.GetDatabase("retaildb");

        Console.WriteLine("creating collections");
        try
        {
            _database.DropCollection("products");
        }
        catch
        {
            _logger.LogError($"Exception: Unable to drop collection products");
        }

        try
        {
            _database.DropCollection("customers");
        }
        catch
        {
            _logger.LogError($"Exception: Unable to drop collection customers");
        }
        try
        {
            _database.DropCollection("salesOrders");
        }
        catch
        {
            _logger.LogError($"Exception: Unable to drop collection salesOrders");
        }

        Console.WriteLine("creating collections");
        _database.CreateCollection("products");
        _database.CreateCollection("customers");
        _database.CreateCollection("salesOrders");
      

        Console.WriteLine("creating indexes");
        CreateVectorIndexIfNotExists("products", "embedding", _vectorIndexType);
        CreateVectorIndexIfNotExists("customers", "embedding", _vectorIndexType);
        CreateVectorIndexIfNotExists("salesOrders", "embedding", _vectorIndexType);
    }

    public void CreateVectorIndexIfNotExists(string collectionName, string vectorPath, string vectorIndexType)
    {

        try
        {

            var vectorIndexDefinition = RetrieveVectorIndexDefinition(collectionName, vectorPath, vectorIndexType); //hnsw or ivf

            IMongoCollection<BsonDocument> collection = _database.GetCollection<BsonDocument>(collectionName);

            string vectorIndexName = $"vectorSearchIndex_";

            //Find if vector index exists in vectors collection
            using (IAsyncCursor<BsonDocument> indexCursor = collection.Indexes.List())
            {
                bool vectorIndexExists = indexCursor.ToList().Any(x => x["name"] == vectorIndexName);
                if (!vectorIndexExists)
                {
                    BsonDocumentCommand<BsonDocument> command = new BsonDocumentCommand<BsonDocument>(
                        vectorIndexDefinition
                    );

                    BsonDocument result = _database.RunCommand(command);
                    if (result["ok"] != 1)
                    {
                        _logger.LogError("CreateIndex failed with response: " + result.ToJson());
                    }
                }
            }

        }
        catch (MongoException ex)
        {
            _logger.LogError("MongoDbService InitializeVectorIndex: " + ex.Message);
            throw;
        }

    }

    private BsonDocument RetrieveVectorIndexDefinition(string collectionName, string vectorPath, string vectorIndexType)
    {
        var vectorIndex = new BsonDocument();
        var vectorIndexName = $"vectorSearchIndex_{vectorPath}";
        if (vectorIndexType == "hnsw")
        {
            vectorIndex = new BsonDocument
            {
                { "createIndexes", collectionName },
                { "indexes", new BsonArray
                    {
                        new BsonDocument
                        {
                            { "name", vectorIndexName },
                            { "key", new BsonDocument { { vectorPath, "cosmosSearch" } } },
                            { "cosmosSearchOptions", new BsonDocument
                                {
                                    { "kind", "vector-hnsw" },
                                    { "m", 64 },
                                    { "efConstruction", 500 },
                                    { "similarity", "COS" },
                                    { "dimensions", 1536 }
                                }
                            }
                        }
                    }
                }
            };
        }
        return vectorIndex;

    }


    

    public async Task<Product> UpsertProductAsync(Product product)
    {

        //Vectorize and add new vector property and store in vectors collection.

        try
        {

            //Serialize the product object to send to OpenAI
            string sProduct = RemoveVectorAndSerialize(product);

            (product.embedding, int tokens) = await _semanticKernelService.GetEmbeddingsAsync(sProduct);

            await _products.ReplaceOneAsync(
                filter: Builders<Product>.Filter.Eq("categoryId", product.categoryId)
                      & Builders<Product>.Filter.Eq("_id", product.id),
                options: new ReplaceOptions { IsUpsert = true },
                replacement: product);

        }
        catch (MongoException ex)
        {
            _logger.LogError($"Exception: UpsertProductAsync(): {ex.Message}");
            throw;

        }

        return product;
    }

    public async Task DeleteProductAsync(Product product)
    {

        try
        {

            var filter = Builders<Product>.Filter.And(
                 Builders<Product>.Filter.Eq("categoryId", product.categoryId),
                 Builders<Product>.Filter.Eq("_id", product.id));

            //Delete from the product collection
            await _products.DeleteOneAsync(filter);


        }
        catch (MongoException ex)
        {
            _logger.LogError($"Exception: DeleteProductAsync(): {ex.Message}");
            throw;

        }

    }

    public async Task<Customer> UpsertCustomerAsync(Customer customer)
    {

        try
        {
            //Remove any existing vectors, then serialize the object to send to OpenAI
            string sObject = RemoveVectorAndSerialize(customer);

            (customer.embedding, int tokens) = await _semanticKernelService.GetEmbeddingsAsync(sObject);

            await _customers.ReplaceOneAsync(
                filter: Builders<Customer>.Filter.Eq("customerId", customer.customerId)
                      & Builders<Customer>.Filter.Eq("_id", customer.id),
                options: new ReplaceOptions { IsUpsert = true },
                replacement: customer);

        }
        catch (MongoException ex)
        {
            _logger.LogError($"Exception: UpsertCustomerAsync(): {ex.Message}");
            throw;

        }

        return customer;

    }

    public async Task DeleteCustomerAsync(Customer customer)
    {

        try
        {
            var filter = Builders<Customer>.Filter.And(
                Builders<Customer>.Filter.Eq("customerId", customer.customerId),
                Builders<Customer>.Filter.Eq("_id", customer.id));

            //Delete customer from customer collection
            await _customers.DeleteOneAsync(filter);


        }
        catch (MongoException ex)
        {
            _logger.LogError($"Exception: DeleteCustomerAsync(): {ex.Message}");
            throw;

        }

    }

    public async Task<SalesOrder> UpsertSalesOrderAsync(SalesOrder salesOrder)
    {

        try
        {

            //Remove any existing vectors, then serialize the object to send to OpenAI
            string sObject = RemoveVectorAndSerialize(salesOrder);

            (salesOrder.embedding, int tokens) = await _semanticKernelService.GetEmbeddingsAsync(sObject);

            await _salesOrders.ReplaceOneAsync(
                filter: Builders<SalesOrder>.Filter.Eq("customerId", salesOrder.customerId)
                      & Builders<SalesOrder>.Filter.Eq("_id", salesOrder.id),
                options: new ReplaceOptions { IsUpsert = true },
                replacement: salesOrder);


        }
        catch (MongoException ex)
        {
            _logger.LogError($"Exception: UpsertSalesOrderAsync(): {ex.Message}");
            throw;

        }

        return salesOrder;

    }

    public async Task DeleteSalesOrderAsync(SalesOrder salesOrder)
    {

        try
        {
            var filter = Builders<SalesOrder>.Filter.And(
                Builders<SalesOrder>.Filter.Eq("customerId", salesOrder.customerId),
                Builders<SalesOrder>.Filter.Eq("_id", salesOrder.id));

            await _salesOrders.DeleteOneAsync(filter);

        }
        catch (MongoException ex)
        {
            _logger.LogError($"Exception: DeleteSalesOrderAsync(): {ex.Message}");
            throw;

        }

    }

    private string RemoveVectorAndSerialize(object o)
    {
        string sObject = string.Empty;

        try
        {
            JObject obj = JObject.FromObject(o);

            obj.Remove("vector");

            sObject = obj.ToString();
        }
        catch { }

        return sObject;
    }

    public async Task ImportAndVectorizeAsync(string collectionName, string json)
    {
        int documentCounter = 0;

        try
        {

            IEnumerable<BsonDocument> documents = BsonSerializer.Deserialize<IEnumerable<BsonDocument>>(json);

            _logger.LogInformation("Loading documents");

            foreach (var document in documents)
            {
                documentCounter++;

                //Vectorize item, add to vector property, save in collection.
                (float[] embeddings, int tokens) = await _semanticKernelService.GetEmbeddingsAsync(document.ToString());

                document["embedding"] = BsonValue.Create(embeddings);

                await _database.GetCollection<BsonDocument>(collectionName).InsertOneAsync(document);

                if (documentCounter % 50 == 0)
                {
                    _logger.LogInformation($"written {documentCounter} documents ");
                }

                /// Very slow load to avoid rate limiting - remove to improve load performance 
                //  _logger.LogInformation("waiting 60 seconds");
                // System.Threading.Thread.Sleep(60000);

            }

        }

        catch (MongoException ex)
        {
            _logger.LogError($"Exception: ImportJsonAsync(): {ex.Message}");
            throw;
        }
    }

    public async Task IngestDataFromBlobStorageAsync()
    {

        try
        {
            BlobContainerClient blobContainerClient = new BlobContainerClient(new Uri("https://cosmosdbcosmicworks.blob.core.windows.net/cosmic-works-mongo-vcore/"));

            //hard-coded here.  In a real-world scenario, you would want to dynamically get the list of blobs in the container and iterate through them.
            //as well as drive all of the schema and meta-data from a configuration file.
            List<string> blobIds = new List<string>() { "products", "customers", "salesOrders" };


            foreach (string blobId in blobIds)
            {
                _logger.LogInformation($"Ingesting {blobId} data from blob storage.");
                BlobClient blob = blobContainerClient.GetBlobClient($"{blobId}.json");
                if (await blob.ExistsAsync())
                {
                    BlobClient blobClient = blobContainerClient.GetBlobClient($"{blobId}.json");
                    BlobDownloadStreamingResult blobResult = await blobClient.DownloadStreamingAsync();

                    using (StreamReader pReader = new StreamReader(blobResult.Content))
                    {
                        string json = await pReader.ReadToEndAsync();
                        await ImportAndVectorizeAsync(blobId, json);

                    }

                    _logger.LogInformation($"{blobId} data ingestion complete.");

                }
            }

        }
        catch (Exception ex)
        {
            _logger.LogError($"Exception: IngestDataFromBlobStorageAsync(): {ex.Message}");
            throw;
        }
    }



    /// <summary>
    /// Gets a list of all current chat sessions.
    /// </summary>
    /// <returns>List of distinct chat session items.</returns>
    public async Task<List<Session>> GetSessionsAsync()
    {
        List<Session> sessions = new List<Session>();
        try
        {

            sessions = await _sessions.Find(
                filter: Builders<Session>.Filter.Eq("Type", nameof(Session))).SortByDescending(e => e.Created)
                .ToListAsync();

        }
        catch (MongoException ex)
        {
            _logger.LogError($"Exception: GetSessionsAsync(): {ex.Message}");
            throw;
        }

        return sessions;
    }

    /// <summary>
    /// Gets a list of all current chat messages for a specified session identifier.
    /// </summary>
    /// <param name="sessionId">Chat session identifier used to filter messages.</param>
    /// <returns>List of chat message items for the specified session.</returns>
    public async Task<List<Message>> GetSessionMessagesAsync(string sessionId)
    {
        List<Message> messages = new();

        try
        {

            messages = await _messages.Find(
                filter: Builders<Message>.Filter.Eq("Type", nameof(Message))
                & Builders<Message>.Filter.Eq("SessionId", sessionId))
                .SortBy(e => e.TimeStamp)
                .ToListAsync();

        }
        catch (MongoException ex)
        {
            _logger.LogError($"Exception: GetSessionMessagesAsync(): {ex.Message}");
            throw;
        }

        return messages;

    }

    /// <summary>
    /// Creates a new chat session.
    /// </summary>
    /// <param name="session">Chat session item to create.</param>
    /// <returns>Newly created chat session item.</returns>
    public async Task InsertSessionAsync(Session session)
    {
        try
        {

            await _sessions.InsertOneAsync(session);

        }
        catch (MongoException ex)
        {
            _logger.LogError($"Exception: InsertSessionAsync(): {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Creates a new chat message.
    /// </summary>
    /// <param name="message">Chat message item to create.</param>
    /// <returns>Newly created chat message item.</returns>
    public async Task InsertMessageAsync(Message message)
    {
        try
        {

            await _messages.InsertOneAsync(message);

        }
        catch (MongoException ex)
        {
            _logger.LogError($"Exception: InsertMessageAsync(): {ex.Message}");
            throw;
        }

    }

    /// <summary>
    /// Updates an existing chat session.
    /// </summary>
    /// <param name="session">Chat session item to update.</param>
    /// <returns>Revised created chat session item.</returns>
    public async Task UpdateSessionAsync(Session session)
    {

        try
        {

            await _sessions.ReplaceOneAsync(
                filter: Builders<Session>.Filter.Eq("Type", nameof(Session))
                & Builders<Session>.Filter.Eq("SessionId", session.SessionId),
                replacement: session);

        }
        catch (MongoException ex)
        {
            _logger.LogError($"Exception: UpdateSessionAsync(): {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Batch create or update chat messages and session.
    /// </summary>
    /// <param name="messages">Chat message and session items to create or replace.</param>
    public async Task UpsertSessionBatchAsync(Session session, Message message)
    {
        using (var transaction = await _client.StartSessionAsync())
        {
            transaction.StartTransaction();

            try
            {

                await _sessions.ReplaceOneAsync(
                    filter: Builders<Session>.Filter.Eq("Type", nameof(Session))
                        & Builders<Session>.Filter.Eq("SessionId", session.SessionId)
                        & Builders<Session>.Filter.Eq("Id", session.Id),
                    replacement: session);

                await _messages.InsertOneAsync(message);

                await transaction.CommitTransactionAsync();
            }
            catch (MongoException ex)
            {
                await transaction.AbortTransactionAsync();
                _logger.LogError($"Exception: UpsertSessionBatchAsync(): {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// Batch deletes an existing chat session and all related messages.
    /// </summary>
    /// <param name="sessionId">Chat session identifier used to flag messages and sessions for deletion.</param>
    public async Task DeleteSessionAndMessagesAsync(string sessionId)
    {
        try
        {

            await _database.GetCollection<BsonDocument>("completions").DeleteManyAsync(
                filter: Builders<BsonDocument>.Filter.Eq("SessionId", sessionId));

        }
        catch (MongoException ex)
        {
            _logger.LogError($"Exception: DeleteSessionAndMessagesAsync(): {ex.Message}");
            throw;
        }

    }

}