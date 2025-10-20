using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Castellan.Worker.Services;
using Castellan.Worker.Services.Interfaces;
using Castellan.Worker.Services.NotificationChannels;
using Castellan.Worker.Services.Notifications;
using Castellan.Worker.Configuration;
using Castellan.Worker.Models.Notifications;
using Castellan.Worker.Abstractions;

namespace Castellan.Worker.Extensions;

/// <summary>
/// Service collection extensions for notification services
/// </summary>
public static class NotificationServiceExtensions
{
    /// <summary>
    /// Adds notification services including Teams, Slack, and notification management
    /// </summary>
    public static IServiceCollection AddCastellanNotifications(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure notification options
        services.Configure<NotificationOptions>(configuration.GetSection("Notifications"));
        services.Configure<TeamsNotificationOptions>(configuration.GetSection("Notifications:Teams"));
        services.Configure<SlackNotificationOptions>(configuration.GetSection("Notifications:Slack"));

        // Register core notification services
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<INotificationManager, NotificationManager>();

        // Register notification channels
        services.AddSingleton<INotificationChannel, TeamsNotificationChannel>();
        services.AddSingleton<INotificationChannel, SlackNotificationChannel>();

        // Register notification configuration store
        services.AddSingleton<INotificationConfigurationStore, FileBasedNotificationConfigurationStore>();

        // Configure notification template options
        services.Configure<NotificationTemplateConfig>(configuration.GetSection("NotificationTemplates"));

        // Register notification template services
        services.AddSingleton<ITemplateRenderer, TemplateRenderer>();
        services.AddSingleton<INotificationTemplateStore, FileBasedNotificationTemplateStore>();

        // Register template initialization service to ensure templates exist on startup
        services.AddHostedService<TemplateInitializationService>();

        return services;
    }
}
