using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Search.Options;
using Search.Services;

#pragma warning disable  CS8600, CS8602, CS8604 

var builder = WebApplication.CreateBuilder(args);

builder.RegisterConfiguration();
builder.Services.AddRazorPages();
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddServerSideBlazor();
builder.Services.RegisterServices();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

await app.RunAsync();

static class ProgramExtensions
{
    public static void RegisterConfiguration(this WebApplicationBuilder builder)
    {
     
        builder.Services.AddOptions<OpenAi>()
            .Bind(builder.Configuration.GetSection(nameof(OpenAi)));

        builder.Services.AddOptions<MongoDb>()
            .Bind(builder.Configuration.GetSection(nameof(MongoDb)));
    }

    public static void RegisterServices(this IServiceCollection services)
    {
        
        services.AddSingleton<SemanticKernelService, SemanticKernelService>((provider) =>
        {
            var semanticKernalOptions = provider.GetRequiredService<IOptions<OpenAi>>();
            var mongoDbOptions = provider.GetRequiredService<IOptions<MongoDb>>();
            if (semanticKernalOptions is null | mongoDbOptions is null)
            {
                throw new ArgumentException($"{nameof(IOptions<OpenAi>)} was not resolved through dependency injection.");
            }
            else
            {
                return new SemanticKernelService(
                    new OpenAi
                    {
                        Endpoint = semanticKernalOptions.Value?.Endpoint ?? string.Empty,
                        EmbeddingsDeployment = semanticKernalOptions.Value?.EmbeddingsDeployment ?? string.Empty,   
                        CompletionsDeployment = semanticKernalOptions.Value?.CompletionsDeployment ?? string.Empty, 
                        MaxEmbeddingTokens = semanticKernalOptions.Value?.MaxEmbeddingTokens ?? string.Empty,
                        MaxConversationTokens = semanticKernalOptions.Value?.MaxEmbeddingTokens ?? string.Empty,   
                        MaxCompletionTokens = semanticKernalOptions.Value?.MaxCompletionTokens ?? string.Empty,
                        MaxContextTokens = semanticKernalOptions.Value?.MaxContextTokens ?? string.Empty
                    },
                    new MongoDb
                    {
                         Connection = mongoDbOptions.Value?.Connection ?? String.Empty,
                         DatabaseName = mongoDbOptions.Value?.DatabaseName ?? String.Empty,
                         CollectionNames = mongoDbOptions.Value?.CollectionNames ?? String.Empty,
                         MaxVectorSearchResults = mongoDbOptions.Value?.MaxVectorSearchResults ?? String.Empty,
                         VectorIndexType = mongoDbOptions.Value?.VectorIndexType ?? String.Empty,
                    },
                    logger: provider.GetRequiredService<ILogger<SemanticKernelService>>()
                );
            }
        });

        services.AddSingleton<MongoDbService, MongoDbService>((provider) =>
        {
            var mongoDbOptions = provider.GetRequiredService<IOptions<MongoDb>>();
            if (mongoDbOptions is null)
            {
                throw new ArgumentException($"{nameof(IOptions<MongoDb>)} was not resolved through dependency injection.");
            }
            else
            {
                return new MongoDbService(
                    connection: mongoDbOptions.Value?.Connection ?? String.Empty,
                    databaseName: mongoDbOptions.Value?.DatabaseName ?? String.Empty,
                    collectionNames: mongoDbOptions.Value?.CollectionNames ?? String.Empty,
                    maxVectorSearchResults: mongoDbOptions.Value?.MaxVectorSearchResults ?? String.Empty,
                    vectorIndexType: mongoDbOptions.Value?.VectorIndexType ?? String.Empty,
                    semanticKernelService: provider.GetRequiredService<SemanticKernelService>(),
                    logger: provider.GetRequiredService<ILogger<MongoDbService>>()
                );
            }
        });

        services.AddSingleton<ChatService, ChatService>((provider) =>
        {
            var chatOptions = provider.GetRequiredService<IOptions<Chat>>();
            if (chatOptions is null)
            {
                throw new ArgumentException($"{nameof(IOptions<Chat>)} was not resolved through dependency injection");
            }
            else
            {
                return new ChatService(
                    mongoDbService: provider.GetRequiredService<MongoDbService>(),
                    semanticKernelService: provider.GetRequiredService<SemanticKernelService>(),
                    logger: provider.GetRequiredService<ILogger<ChatService>>()
                );
            }
        });
    }
}
