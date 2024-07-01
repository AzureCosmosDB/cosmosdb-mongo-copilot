using Search.Models;
using MongoDB.Driver;
using Search.Constants;
using Azure.AI.OpenAI;
using System.Collections;
using Microsoft.ML.Tokenizers;

#pragma warning disable  CS8600, CS8602, CS8604 

namespace Search.Services;

public class ChatService
{
    /// <summary>
    /// All data is cached in the _sessions List object.
    /// </summary>
    private static List<Session> _sessions = new();

    private readonly MongoDbService _mongoDbService;
    private readonly SemanticKernelService _semanticKernelService;
    private readonly ILogger _logger;

    private readonly Tokenizer tokenizer = Tokenizer.CreateTiktokenForModel("gpt-3.5-turbo");

    public ChatService(MongoDbService mongoDbService, SemanticKernelService semanticKernelService, ILogger logger)
    {

        _mongoDbService = mongoDbService;
        _semanticKernelService = semanticKernelService;

        _logger = logger;
    }

    private async Task<string> GetCollectionNameFromSelection(string selectedCollectionName,string prompt)
    {
        string collectionName = "none"; // default source collection context

        switch (selectedCollectionName)
        {
            case "<none>":
                collectionName = "none";
                break;

            case "<auto>":
                collectionName = "none";
                (string sourceCompletionText, int sourcePromptTokens, int sourceCompletionTokens)
                        = await _semanticKernelService.GetPreferredSourceAsync(prompt);

                if (sourceCompletionText == "salesOrders") collectionName = "salesOrders";
                if (sourceCompletionText == "products") collectionName = "products";
                if (sourceCompletionText == "customers") collectionName = "customers";
                break;

            default:
                collectionName = selectedCollectionName;
                break;
        }
        return collectionName;
    }

    private string GetConversationStringFromMessages(List<Message> conversationContext, string prompt)
    {
        var conversationString = string
            .Join(Environment.NewLine,
                conversationContext.Select(m => m.Prompt + Environment.NewLine + m.Completion)
                .ToArray() + Environment.NewLine + prompt);

        return conversationString;
    }

    public async Task<string> ProcessUserPrompt(string? sessionId, string prompt, string selectedCollectionName, string selectedCacheEnable)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(sessionId);

            // Initialize variables
            string completion = "";
            string dataContext = "";
            List<Message> conversationContext = new();
            int promptTokens = 0;
            int completionTokens = 0;
            bool cacheHit = false;


            // Handle UI input.
            string collectionName = await GetCollectionNameFromSelection(selectedCollectionName, prompt);
            bool cacheEnabled = (selectedCacheEnable == "yes") ? true : false;

            /// Check if  cache  enabled and get a chache hit/ Check cache if enabled
            if (cacheEnabled)
            {
                completion = await _semanticKernelService.CheckCache(prompt);
                cacheHit = (completion != string.Empty);
            }

            if (!cacheHit)  // Prompt processing block
            {

                // Get conversation context
                conversationContext = GetConversationContext(sessionId,_semanticKernelService.MaxConversationTokens);

                // Get conversation embeddings
                (float[] promptConversationVectors, int promptConversationTokens)
                    = await _semanticKernelService.GetEmbeddingsAsync(
                        GetConversationStringFromMessages(conversationContext, prompt));

                // Get RAG data context
                if (collectionName != "none")
                    dataContext  = await _mongoDbService.VectorSearchAsync(
                        collectionName, "embedding",
                        promptConversationVectors, _semanticKernelService.MaxContextTokens);

                // Get completion
                ( completion,  promptTokens,  completionTokens) =
                    await _semanticKernelService.GetChatCompletionAsync(
                        conversationContext, dataContext, prompt);

                //Add entry to cache
                await _semanticKernelService.AddCachedMemory(prompt, completion);
            }
          
            //Create message with all prompt, response and meta data
            Message message = new Message(
                    sessionId: sessionId,
                    prompt: prompt,
                    promptTokens: promptTokens,
                    completion: completion,
                    completionTokens: completionTokens,
                    sourceSelected: selectedCollectionName,
                    sourceCollection: collectionName,
                    selectedCacheEnable, cacheHit);
            
            //Commit message 
            await AddPromptCompletionMessagesAsync(sessionId, message);

            return completion;
        }
        catch (Exception ex)
        {
            string message = $"ChatService.GetChatCompletionAsync(): {ex.Message}";
            _logger.LogError(message);
            throw;
        }
    }

    /// <summary>
    /// Receive a prompt from a user, Vectorize it from _openAIService Get a completion from _openAiService
    /// </summary>
       /// <summary>
    /// Estimate the token usage for OpenAI completion to prevent exceeding the OpenAI model's maximum token limit. This function estimates the
    /// amount of tokens the vector search result data and the user prompt will consume. If the search result data exceeds the configured amount
    /// the function reduces the number of vectors, reducing the amount of data sent.
    /// </summary>
    
    /// <summary>
    /// Get the most recent conversation history to provide additional context for the completion LLM
    /// </summary>
    /// 
    
    private string GetConversationHistoryString(string sessionId, int turns)
    {

        int index = _sessions.FindIndex(s => s.SessionId == sessionId);

        List<Message> conversationMessages = _sessions[index].Messages.ToList(); //make a full copy

        var trimmedMessages = conversationMessages
            .OrderByDescending(m => m.TimeStamp)
            .Take(turns)
            .Select(m => m.Prompt + Environment.NewLine + m.Completion)
            .ToList();

        trimmedMessages.Reverse();

        string conversation = string.Join(Environment.NewLine, trimmedMessages.ToArray());

        return conversation;
    }

    private List<Message> GetConversationContext(string sessionId, int maxConverstionTokens)
    {

        int index = _sessions.FindIndex(s => s.SessionId == sessionId);
        List<Message> conversationMessages = _sessions[index]
            .Messages
            .OrderByDescending(m => m.TimeStamp)
            .ToList();
       
        List<Message> trimmedMessages = new List<Message>();

        //Tokenize and trim conversation history based on tokens
        int totalTokens = 0;
        foreach ( var message in conversationMessages)
        {
            var messageTokens = tokenizer.CountTokens(message.Prompt) + tokenizer.CountTokens(message.Completion);
            if ((totalTokens+ messageTokens)> maxConverstionTokens)
                break;
            totalTokens= totalTokens + messageTokens;
            trimmedMessages.Add(message);
        }

        return trimmedMessages.Reverse<Message>().ToList();

    }


    /// <summary>
    /// Returns list of chat session ids and names for left-hand nav to bind to (display Name and ChatSessionId as hidden)
    /// </summary>
    public async Task<List<Session>> GetAllChatSessionsAsync()
    {
        return _sessions = await _mongoDbService.GetSessionsAsync();
    }

    /// <summary>
    /// Returns the chat messages to display on the main web page when the user selects a chat from the left-hand nav
    /// </summary>
    public async Task<List<Message>> GetChatSessionMessagesAsync(string? sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        List<Message> chatMessages = new();

        if (_sessions.Count == 0)
        {
            return Enumerable.Empty<Message>().ToList();
        }

        int index = _sessions.FindIndex(s => s.SessionId == sessionId);

        if (_sessions[index].Messages.Count == 0)
        {
            // Messages are not cached, go read from database
            chatMessages = await _mongoDbService.GetSessionMessagesAsync(sessionId);

            // Cache results
            _sessions[index].Messages = chatMessages;
        }
        else
        {
            // Load from cache
            chatMessages = _sessions[index].Messages;
        }

        return chatMessages;
    }

    /// <summary>
    /// User creates a new Chat Session.
    /// </summary>
    public async Task CreateNewChatSessionAsync()
    {
        Session session = new();
        _sessions.Add(session);
        await _mongoDbService.InsertSessionAsync(session);
    }

    /// <summary>
    /// Rename the Chat Ssssion from "New Chat" to the summary provided by OpenAI
    /// </summary>
    public async Task RenameChatSessionAsync(string? sessionId, string newChatSessionName)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        int index = _sessions.FindIndex(s => s.SessionId == sessionId);
        _sessions[index].Name = newChatSessionName;
        await _mongoDbService.UpdateSessionAsync(_sessions[index]);
    }

    /// <summary>
    /// User deletes a chat session
    /// </summary>
    public async Task DeleteChatSessionAsync(string? sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        int index = _sessions.FindIndex(s => s.SessionId == sessionId);
        _sessions.RemoveAt(index);
        await _mongoDbService.DeleteSessionAndMessagesAsync(sessionId);
    }



    public async Task<string> SummarizeChatSessionNameAsync(string? sessionId, string sessionText)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        string response = await _semanticKernelService.SummarizeConversationAsync(sessionText);

        await RenameChatSessionAsync(sessionId, response);

        return response;
    }

   
    /// <summary>
    /// Add user prompt and AI assistance response to the chat session message list object and insert into the data service as a transaction.
    /// </summary>
    private async Task AddPromptCompletionMessagesAsync(string sessionId, Message message)
    {

        int index = _sessions.FindIndex(s => s.SessionId == sessionId);

        //Add prompt and completion to the cache
        _sessions[index].AddMessage(message);

        //Update session cache with tokens used
        _sessions[index].TokensUsed += message.PromptTokens + message.CompletionTokens;

        await _mongoDbService.UpsertSessionBatchAsync(session: _sessions[index], message: message);
    }

    public async Task ClearCacheAsync()
    {
        await _semanticKernelService.ClearCacheAsync();
    }
    

}