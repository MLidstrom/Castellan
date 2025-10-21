using Castellan.Worker.Abstractions;
using Castellan.Worker.Configuration;
using Castellan.Worker.Services.Actions;

namespace Castellan.Worker.Extensions;

/// <summary>
/// Extension methods for registering action execution services with dependency injection.
/// </summary>
public static class ActionServiceExtensions
{
    /// <summary>
    /// Registers all action execution services including action handlers and rollback service.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddCastellanActions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure action rollback options
        services.Configure<ActionRollbackOptions>(
            configuration.GetSection("ActionRollback"));

        // Register action rollback service
        services.AddScoped<IActionRollbackService, ActionRollbackService>();

        // Register all action handlers as IActionHandler
        services.AddSingleton<IActionHandler, AddToWatchlistActionHandler>();
        services.AddSingleton<IActionHandler, BlockIPActionHandler>();
        services.AddSingleton<IActionHandler, IsolateHostActionHandler>();
        services.AddSingleton<IActionHandler, QuarantineFileActionHandler>();
        services.AddSingleton<IActionHandler, CreateTicketActionHandler>();

        return services;
    }
}
