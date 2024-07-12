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
    
    public async Task<string> ProcessUserPrompt(string? sessionId, string prompt, string selectedCollectionName, string selectedCacheEnable)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(sessionId);

            // Initialize variables
            string completion = "";                      // the output from our call to the LLM            
            List<Message> conversationContext = new();   // chat context to provide to the LLM
            string dataContext = "";                     // data context to provide to the LLM
            int promptTokens = 0;                        // the number of tokens for the prompt
            int completionTokens = 0;                    // the number of tokens for the completion
            bool cacheHit = false;


            // Handle UI input.
            string collectionName = await GetCollectionNameFromSelection(selectedCollectionName, prompt);
            bool cacheEnabled = (selectedCacheEnable == "yes") ? true : false;

            // Check if  cache enabled and get a chache hit
            // add cache check code here

            if (true) // Prompt processing block
            {
                // Get conversation context
                //   code to get conversation context goes here

                // Get conversation embeddings
                //  code to get conversation embeddings goes here

                // Get RAG data context
                //  code to get RAG data context goes here

                // Get completion
                (completion, promptTokens, completionTokens) =
                    await _semanticKernelService.GetChatCompletionAsync(
                        conversationContext, dataContext, prompt);

                // Add entry to cache
                //  code for adding entry to cache goes here

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

    private List<Message> GetConversationContext(
       string sessionId, int maxConverstionTokens)
    {
        // conversationMessages contains an ordered list of all conversation messsages for a session
        int index = _sessions.FindIndex(s => s.SessionId == sessionId);
        List<Message> conversationMessages = _sessions[index]
            .Messages
            .OrderByDescending(m => m.TimeStamp)
            .ToList();

        List<Message> trimmedMessages = new List<Message>();

        //Tokenize and trim conversation history based on tokens
        // add code to tokenize and trim context

        return trimmedMessages.Reverse<Message>().ToList();

    }

    private async Task<string> GetCollectionNameFromSelection(string selectedCollectionName, string prompt)
    {
        string collectionName = "none"; // default source collection context

        switch (selectedCollectionName)
        {
            case "<none>":
                collectionName = "none";
                break;

            case "<auto>":
                collectionName = "none";
                // Perform source selection lookup using LLM
                // Add code to perform source selection using LLM 
                await Task.Delay(0);
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

   


    /// <summary>
    /// Returns list of chat session ids and names for left-hand nav to bind to (display Name and ChatSessionId as hidden)
    /// </summary>
    public async Task<List<Session>> GetAllChatSessionsAsync()
    {
        return _sessions = await _mongoDbService.GetSessionsAsync();
    }

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


    public async Task CreateNewChatSessionAsync()
    {
        Session session = new();
        _sessions.Add(session);
        await _mongoDbService.InsertSessionAsync(session);
    }

    public async Task RenameChatSessionAsync(string? sessionId, string newChatSessionName)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        int index = _sessions.FindIndex(s => s.SessionId == sessionId);
        _sessions[index].Name = newChatSessionName;
        await _mongoDbService.UpdateSessionAsync(_sessions[index]);
    }

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

        // replace with code to perform LLM summary 
        string response = $"Chat {sessionId.Substring(sessionId.Length-12)}";

        await RenameChatSessionAsync(sessionId, response);

        return response;
    }

   
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