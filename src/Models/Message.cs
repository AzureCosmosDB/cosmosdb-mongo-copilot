using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace Search.Models;

public record Message
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; }

    public string Type { get; set; }

    public string SessionId { get; set; }

    public DateTime TimeStamp { get; set; }

    public string Prompt { get; set; }

    public int PromptTokens { get; set; }

    public string Completion { get; set; }

    public int CompletionTokens { get; set; }

    public string SourceSelected { get; set; }
    
    public string SourceCollection  { get; set; }

    public string CacheSelected { get; set; }

    public bool CacheHit { get; set; }

    public Message(string sessionId,  string prompt, int promptTokens
            , string completion = "", int completionTokens = 0
            , string sourceSelected = "", string sourceCollection = ""
            , string cacheSelected= "", bool cacheHit = false  )
    {
        Id = Guid.NewGuid().ToString();
        Type = nameof(Message);
        SessionId = sessionId;
        Prompt = prompt;
        PromptTokens = promptTokens;
        Completion = completion;
        CompletionTokens = completionTokens;
        SourceSelected = sourceSelected;
        SourceCollection = sourceCollection;
        CacheSelected = cacheSelected;
        CacheHit = cacheHit;
        TimeStamp = DateTime.UtcNow;
    }

}