using MongoDB.Bson;
using MongoDB.Driver;
using Search.Models;
using MongoDB.Bson.Serialization;
using Newtonsoft.Json.Linq;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using Microsoft.ML.Tokenizers;

#pragma warning disable  CS8600, CS8602, CS8604 

namespace Search.Services;

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

    public async Task<string> VectorSearchAsync(string collectionName, string path, float[] embeddings, int maxTokens)
    {
        try
        {
            string resultDocuments = "[";

            // Perform vector search
            // add vector search code here
            await Task.Delay(0);


            resultDocuments = resultDocuments + "]";
            return resultDocuments;
        }
        catch (MongoException ex)
        {
            _logger.LogError($"Exception: VectorSearchAsync(): {ex.Message}");
            throw;
        }

       
    }

    public int incCounter()
    {
        counter = counter++;
        return counter;
    }

    public void SetupCollections()
    {

        Console.WriteLine("creating database");
        _client.GetDatabase("retaildb");


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

            Console.WriteLine("Loading documents");

            foreach (var document in documents)
            {
                documentCounter++;

                //Vectorize item, add to vector property, save in collection.
                (float[] embeddings, int tokens) = await _semanticKernelService.GetEmbeddingsAsync(document.ToString());

                document["embedding"] = BsonValue.Create(embeddings);

                await _database.GetCollection<BsonDocument>(collectionName).InsertOneAsync(document);

                if (documentCounter % 50 == 0)
                {
                    Console.WriteLine($"written {documentCounter} documents ");
                }

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
                Console.WriteLine($"Ingesting {blobId} data from blob storage.");
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