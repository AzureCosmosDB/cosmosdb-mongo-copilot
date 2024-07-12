using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using Azure.AI.OpenAI;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Connectors.AzureCosmosDBMongoDB;
using Azure.Identity;
using Search.Options;
using Search.Models;
using Azure.Core;

#pragma warning disable  CS8600, CS8602, CS8604 
#pragma warning disable SKEXP0010, SKEXP0001, SKEXP0020

namespace Search.Services;

public class SemanticKernelService
{

    private readonly string _embeddingsModelOrDeployment = string.Empty;
    private readonly string _completionsModelOrDeployment = string.Empty;

    private readonly int _maxCompletionTokens = default;
    private readonly int _maxContextTokens = default;
    private readonly int _maxConversationTokens = default;

    private readonly ILogger _logger;

    //Semantic Kernel
    private readonly Kernel kernel;
    private readonly AzureCosmosDBMongoDBMemoryStore memoryStore;
    private readonly ISemanticTextMemory memory;

    private readonly string _simpleSystemPrompt = @"
        You are a cheerful intelligent assistant for the Cosmic Works Bike Company 
        You answer as truthfully as possible.
        ";

    private readonly string _cosmicSystemPrompt = @"
     You are an intelligent assistant for the Cosmic Works Bike Company. 
     You are designed to provide helpful answers to user questions about
     product, product category, customer and sales order information 
     provided in JSON format in the following context information section.

     Context information:";


    //System prompt to send with user prompts to instruct the model for summarization
    private readonly string _summarizeSystemPrompt = @"
        Summarize the text below in one or two words to use as a label in a button on a web page. Output words only. Summarize the text below here:" + Environment.NewLine;

    private readonly string _sourceSelectionSystemPrompt = @"
        Select which source of additional information would be most usefull to answer the question provided from either
        product, customer and sales order information sources based on the prompt provided.

        The product source contains information about the products with the following properties: category Id, categoryName, sku, productName, description, price and tags
        The customer source contains information about the customer and has the following properties: customerId, title, firstName, lastName, emailAddress,  phone Number, addresses and order creation Date
        The sales order source contains information about customer sales and has the following properties: customerId, order Date, ship Date, sku, name, price and quantity

        Instructions:
        - If you're unsure of an answer, you must say ""unknown"".
         - Always respond ""salesOrder"" when the question contains the words ""sales"", ""purchases"" or ""invoices""
        - Only provide a one-word answer:
            ""products"" if the product source is the most relevant
            ""customers"" if the customer source is preferred
            ""salesOrders"" if the sales order source is preferred
            ""none"" 
            ""unknown"" if you are unsure.

        Text of the question is :";


   
    public SemanticKernelService(OpenAi semanticKernelOptions, MongoDb mongoDbOptions, ILogger logger)
    {
        _simpleSystemPrompt += "";
        _cosmicSystemPrompt += "";
        _sourceSelectionSystemPrompt += "";
        ArgumentNullException.ThrowIfNullOrEmpty(semanticKernelOptions.Endpoint);
        ArgumentNullException.ThrowIfNullOrEmpty(semanticKernelOptions.CompletionsDeployment);
        ArgumentNullException.ThrowIfNullOrEmpty(semanticKernelOptions.EmbeddingsDeployment);
        ArgumentNullException.ThrowIfNullOrEmpty(semanticKernelOptions.MaxCompletionTokens);
        ArgumentNullException.ThrowIfNullOrEmpty(semanticKernelOptions.MaxContextTokens);
        ArgumentNullException.ThrowIfNullOrEmpty(semanticKernelOptions.MaxContextTokens);


        ArgumentNullException.ThrowIfNullOrEmpty(mongoDbOptions.Connection);
        ArgumentNullException.ThrowIfNullOrEmpty(mongoDbOptions.DatabaseName);

        _maxCompletionTokens = int.TryParse(semanticKernelOptions.MaxCompletionTokens, out _maxCompletionTokens) ? _maxCompletionTokens : 0;
        _maxConversationTokens = int.TryParse(semanticKernelOptions.MaxConversationTokens, out _maxConversationTokens) ? _maxConversationTokens : 0;
        _maxContextTokens = int.TryParse(semanticKernelOptions.MaxContextTokens, out _maxContextTokens) ? _maxContextTokens : 0;

        _logger = logger;
        TokenCredential credential = new DefaultAzureCredential();
        // Initialize the Semantic Kernel
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.AddAzureOpenAIChatCompletion(
            semanticKernelOptions.CompletionsDeployment,
            semanticKernelOptions.Endpoint,
            credential);
        kernelBuilder.AddAzureOpenAITextEmbeddingGeneration(
            semanticKernelOptions.EmbeddingsDeployment,
            semanticKernelOptions.Endpoint,
            credential);
        kernel = kernelBuilder.Build();

        // Build Sematic Kernel memory with Cosmos DB for MongoDB connector
        AzureCosmosDBMongoDBConfig memoryConfig = new(1536);
        memoryConfig.Kind = AzureCosmosDBVectorSearchType.VectorHNSW;

        memoryStore = new(
            mongoDbOptions.Connection,
            mongoDbOptions.DatabaseName,
            memoryConfig);

        memory = new MemoryBuilder()
                .WithAzureOpenAITextEmbeddingGeneration(
                    semanticKernelOptions.EmbeddingsDeployment,
                    semanticKernelOptions.Endpoint,
                    credential)
                .WithMemoryStore(memoryStore)
                .Build();
    }

    public async Task<(string? response, int promptTokens, int responseTokens)>
            GetChatCompletionAsync(List<Message> conversationMessages, string RAGContext, string prompt)
    {
        try
        {
            var response = "";
            var promptTokens = 0;
            var completionTokens = 0;

            // Construct chatHistory
            string systemPrompt = _simpleSystemPrompt;
            ChatHistory chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            
                //Add code to include conversationMesssages to chat context

            chatHistory.AddUserMessage(prompt);


            // Construct settings 
            OpenAIPromptExecutionSettings settings = new();
            settings.Temperature = 0.2;
            settings.MaxTokens = _maxCompletionTokens;
            settings.TopP = 0.7;
            settings.FrequencyPenalty = 0;
            settings.PresencePenalty = -2;

            // Get Completion
            var result = await kernel.GetRequiredService<IChatCompletionService>()
                .GetChatMessageContentAsync(chatHistory, settings);
            response = result.Items[0].ToString();

            // Get Token usage
            CompletionsUsage completionUsage = (CompletionsUsage)result.Metadata["Usage"];
            promptTokens = completionUsage.PromptTokens;
            completionTokens = completionUsage.CompletionTokens;

            return (
             response: response,
             promptTokens: promptTokens,
             responseTokens: completionTokens
             );

        }
        catch (Exception ex)
        {

            string message = $"OpenAiService.GetChatCompletionAsync(): {ex.Message}";
            _logger.LogError(message);
            throw;

        }
    }

    public async Task<(float[] vectors, int embeddingsTokens)>
        GetEmbeddingsAsync(string input)
    {
        try
        {
            // Generate embeddings
            // add code to generate embedding here
            await Task.Delay(0);
            float[] embeddingsArray = new float[0];


            int responseTokens = 0;
            return (embeddingsArray, responseTokens);
        }
        catch (Exception ex)
        {
            string message = $"SemanticKernel.GetEmbeddingsAsync(): {ex.Message}";
            _logger.LogError(message);
            throw;

        }
    }

    public async Task AddCachedMemory(string promptText, string completionText)
    {
        //Save prompt and completion to memory
        // add code to save to memory
        await Task.Delay(0); // place holder code
    }

    public async Task<string> CheckCache(string userPrompt)
    {
        string cacheResult = string.Empty;

        //Search memory for userPrompt
        // add code to query memory
        await Task.Delay(0); // place holder code

        return cacheResult;
    }

    public async Task ClearCacheAsync()
    {
        // Clear cache by deleting memory store collection
        // add code to clear the cache
        await Task.Delay(0); // place holder code
    }

    public async Task<(string? response, int promptTokens, int responseTokens)>
        GetPreferredSourceAsync(string prompt)
    {
        ChatHistory sourceChatHistory = new ChatHistory();

        sourceChatHistory.AddSystemMessage(_sourceSelectionSystemPrompt);
        sourceChatHistory.AddUserMessage(prompt);

        try
        {

            OpenAIPromptExecutionSettings settings = new();

            settings.Temperature = 1.0;
            settings.MaxTokens = _maxCompletionTokens;
            settings.TopP = 1.0;
            settings.FrequencyPenalty = 0;
            settings.PresencePenalty = -2;

            var result = await kernel.GetRequiredService<IChatCompletionService>().GetChatMessageContentAsync(sourceChatHistory, settings);

            CompletionsUsage completionUsage = (CompletionsUsage)result.Metadata["Usage"];

            return (
             response: result.Items[0].ToString(),
             promptTokens: completionUsage.PromptTokens,
             responseTokens: completionUsage.CompletionTokens
            );

        }
        catch (Exception ex)
        {

            string message = $"OpenAiService.GetChatCompletionAsync(): {ex.Message}";
            _logger.LogError(message);
            throw;

        }

    }
    
    public async Task<string> SummarizeConversationAsync(string conversation)
    {
        //return await summarizePlugin.SummarizeConversationAsync(conversation, kernel);

        var skChatHistory = new ChatHistory();
        skChatHistory.AddSystemMessage(_summarizeSystemPrompt);
        skChatHistory.AddUserMessage(conversation);


        OpenAIPromptExecutionSettings settings = new();
        settings.Temperature = 0.0;
        settings.MaxTokens = 200;
        settings.TopP = 1.0;
        settings.FrequencyPenalty = -2;
        settings.PresencePenalty = -2;

        var result = await kernel.GetRequiredService<IChatCompletionService>().GetChatMessageContentAsync(skChatHistory, settings);

        string completion = result.Items[0].ToString()!;
        string summary = Regex.Replace(completion, @"[^a-zA-Z0-9\s]", "");

        return summary;
    }
    
    public int MaxCompletionTokens
    {
        get => _maxCompletionTokens;
    }

    public int MaxContextTokens
    {
        get => _maxContextTokens;
    }

    public int MaxConversationTokens
    {
        get => _maxConversationTokens;
    }

};