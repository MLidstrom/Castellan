using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Embeddings;
using Castellan.Worker.Llms;
using Castellan.Worker.VectorStores;
using Castellan.Worker.Options;
using Castellan.Worker.Services.ConnectionPools;
using Castellan.Worker.Services.ConnectionPools.Interfaces;

namespace Castellan.Worker.Extensions;

/// <summary>
/// Service collection extensions for AI/ML services
/// </summary>
public static class AIServiceExtensions
{
    /// <summary>
    /// Adds AI services including embeddings, LLM providers, and vector stores
    /// </summary>
    public static IServiceCollection AddCastellanAI(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure AI options
        services.Configure<QdrantOptions>(configuration.GetSection("Qdrant"));
        services.Configure<EmbeddingOptions>(configuration.GetSection("Embeddings"));
        services.Configure<LlmOptions>(configuration.GetSection("LLM"));
        services.Configure<EmbeddingCacheOptions>(configuration.GetSection("EmbeddingCache"));
        services.Configure<ResilienceOptions>(configuration.GetSection("Resilience"));
        services.Configure<StrictJsonOptions>(configuration.GetSection("StrictJson"));
        services.Configure<HybridSearchOptions>(configuration.GetSection("HybridSearch"));
        services.Configure<OpenTelemetryOptions>(configuration.GetSection("OpenTelemetry"));
        services.Configure<EnsembleOptions>(configuration.GetSection("Ensemble"));

        // Add memory cache for embedding cache
        services.AddMemoryCache(options =>
        {
            var maxEntries = configuration.GetValue<int>("EmbeddingCache:MaxEntries", 50000);
            options.SizeLimit = maxEntries;
        });

        // Add connection pools
        services.AddSingleton<QdrantConnectionPool>();
        services.AddSingleton<IQdrantConnectionPool>(provider =>
            provider.GetRequiredService<QdrantConnectionPool>());

        // Register embedding provider with layered decorators: Base -> Resilience -> Cache -> Telemetry
        var embedProvider = configuration["Embeddings:Provider"] ?? "Ollama";
        var cacheEnabled = configuration.GetValue<bool>("EmbeddingCache:Enabled", true);
        var resilienceEnabled = configuration.GetValue<bool>("Resilience:Embedding:Enabled", true);
        var telemetryEnabled = configuration.GetValue<bool>("OpenTelemetry:Enabled", true);

        // Register base embedder
        if (embedProvider.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<OllamaEmbedder>();
        else if (embedProvider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<OpenAIEmbedder>();
        else if (embedProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<MockEmbedder>();
        else
            services.AddSingleton<OllamaEmbedder>();

        // Build decorator chain: Base -> Resilience -> Cache -> Telemetry -> IEmbedder
        services.AddSingleton<IEmbedder>(provider =>
        {
            // Get base embedder
            IEmbedder embedder;
            if (embedProvider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
                embedder = provider.GetRequiredService<OpenAIEmbedder>();
            else if (embedProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
                embedder = provider.GetRequiredService<MockEmbedder>();
            else
                embedder = provider.GetRequiredService<OllamaEmbedder>();

            // Layer 1: Add resilience (if enabled)
            if (resilienceEnabled)
            {
                var resilienceOptions = provider.GetRequiredService<IOptions<ResilienceOptions>>();
                var resilienceLogger = provider.GetService<ILogger<ResilientEmbedder>>();
                embedder = new ResilientEmbedder(embedder, resilienceOptions, resilienceLogger);
            }

            // Layer 2: Add caching (if enabled)
            if (cacheEnabled)
            {
                var cache = provider.GetRequiredService<IMemoryCache>();
                var cacheOptions = provider.GetRequiredService<IOptions<EmbeddingCacheOptions>>();
                var cacheLogger = provider.GetService<ILogger<CachingEmbedder>>();
                embedder = new CachingEmbedder(embedder, cache, cacheOptions, cacheLogger);
            }

            // Layer 3: Add telemetry (if enabled)
            if (telemetryEnabled)
            {
                var telemetryOptions = provider.GetRequiredService<IOptions<OpenTelemetryOptions>>();
                var telemetryLogger = provider.GetService<ILogger<TelemetryEmbedder>>();
                embedder = new TelemetryEmbedder(embedder, telemetryOptions, telemetryLogger);
            }

            return embedder;
        });

        // Register LLM client factory for creating model instances
        services.AddSingleton<ILlmClientFactory, LlmClientFactory>();

        // Register LLM provider with full decorator chain including ensemble support
        var llmProvider = configuration["LLM:Provider"] ?? "Ollama";
        var ensembleEnabled = configuration.GetValue<bool>("Ensemble:Enabled", false);

        services.AddSingleton<ILlmClient>(provider =>
        {
            var factory = provider.GetRequiredService<ILlmClientFactory>();
            var ensembleOptions = provider.GetRequiredService<IOptions<EnsembleOptions>>();
            var logger = provider.GetRequiredService<ILogger<EnsembleLlmClient>>();

            // Create default client for single-model or fallback scenarios
            var defaultModel = configuration["LLM:Model"] ?? "llama3.1:8b-instruct-q8_0";
            var defaultClient = factory.CreateClient(defaultModel, llmProvider);

            // Wrap with ensemble decorator if enabled
            if (ensembleEnabled)
            {
                return new EnsembleLlmClient(factory, defaultClient, ensembleOptions, logger);
            }

            return defaultClient;
        });

        // Register vector store with hybrid search decorator: QdrantPooled -> Hybrid -> IVectorStore
        var hybridSearchEnabled = configuration.GetValue<bool>("HybridSearch:Enabled", true);

        if (hybridSearchEnabled)
        {
            // Register base vector store
            services.AddSingleton<QdrantPooledVectorStore>();

            // Wrap with hybrid search decorator
            services.AddSingleton<IVectorStore>(provider =>
            {
                var baseStore = provider.GetRequiredService<QdrantPooledVectorStore>();
                var hybridOptions = provider.GetRequiredService<IOptions<HybridSearchOptions>>();
                var logger = provider.GetRequiredService<ILogger<HybridVectorStore>>();
                return new HybridVectorStore(baseStore, hybridOptions, logger);
            });
        }
        else
        {
            // Use QdrantPooledVectorStore directly without hybrid search
            services.AddSingleton<IVectorStore, QdrantPooledVectorStore>();
        }

        return services;
    }
}
