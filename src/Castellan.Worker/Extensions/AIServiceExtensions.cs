using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
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

        // Add connection pools
        services.AddSingleton<QdrantConnectionPool>();
        services.AddSingleton<IQdrantConnectionPool>(provider =>
            provider.GetRequiredService<QdrantConnectionPool>());

        // Register embedding provider based on configuration
        var embedProvider = configuration["Embeddings:Provider"] ?? "Ollama";
        if (embedProvider.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IEmbedder, OllamaEmbedder>();
        else if (embedProvider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IEmbedder, OpenAIEmbedder>();
        else if (embedProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IEmbedder, MockEmbedder>();
        else
            services.AddSingleton<IEmbedder, OllamaEmbedder>(); // Default fallback

        // Register LLM provider based on configuration
        var llmProvider = configuration["LLM:Provider"] ?? "Ollama";
        if (llmProvider.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<ILlmClient, OllamaLlm>();
        else
            services.AddSingleton<ILlmClient, OpenAILlm>();

        // Use QdrantPooledVectorStore with connection pooling
        services.AddSingleton<IVectorStore, QdrantPooledVectorStore>();

        return services;
    }
}
