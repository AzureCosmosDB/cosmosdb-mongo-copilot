using Search.Services;

namespace Search.Options;

public record Chat
{
    public required MongoDbService MongoDbService { get; set; }
    
    public required ILogger Logger { get; init; }
}
