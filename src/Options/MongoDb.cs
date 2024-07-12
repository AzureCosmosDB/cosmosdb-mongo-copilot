using Microsoft.Extensions.Logging;
using Search.Services;

namespace Search.Options
{
    public record MongoDb
    {
        public string? Connection { get; set; }
        public string? DatabaseName { get; set; } 

        public string? CollectionNames { get; set; }

        public string? MaxVectorSearchResults { get; set; }

        public string? VectorIndexType { get; set; }

    }
}
