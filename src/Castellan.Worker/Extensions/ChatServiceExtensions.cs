using Castellan.Worker.Abstractions;
using Castellan.Worker.Services.Chat;

namespace Castellan.Worker.Extensions;

/// <summary>
/// Extension methods for registering chat services with dependency injection.
/// </summary>
public static class ChatServiceExtensions
{
    /// <summary>
    /// Registers all chat-related services for conversational AI interface.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddCastellanChat(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register chat services
        services.AddScoped<IIntentClassifier, IntentClassifier>();
        services.AddScoped<IContextRetriever, ContextRetriever>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IConversationManager, ConversationManager>();

        // Register LLM client for chat services
        // The ILlmClient is already registered in AIServiceExtensions
        // This ensures chat services can access the configured LLM client

        return services;
    }
}
