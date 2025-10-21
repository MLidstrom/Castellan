using Castellan.Worker.Abstractions;
using Castellan.Worker.Data;
using Castellan.Worker.Models;
using Castellan.Worker.Models.Actions;
using Castellan.Worker.Models.Chat;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Castellan.Worker.Services.Actions;

/// <summary>
/// Handler for adding entities (IPs, users, hosts, hashes) to monitoring watchlist
/// </summary>
public class AddToWatchlistActionHandler : IActionHandler
{
    private readonly IDbContextFactory<CastellanDbContext> _contextFactory;
    private readonly ILogger<AddToWatchlistActionHandler> _logger;

    public ActionType ActionType => ActionType.AddToWatchlist;

    public AddToWatchlistActionHandler(
        IDbContextFactory<CastellanDbContext> contextFactory,
        ILogger<AddToWatchlistActionHandler> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<ActionExecutionResult> ExecuteAsync(object actionData, CancellationToken cancellationToken = default)
    {
        var data = DeserializeActionData(actionData);
        var logs = new List<string>();

        try
        {
            logs.Add($"Adding {data.EntityType} '{data.EntityValue}' to watchlist");
            logs.Add($"Reason: {data.Reason}");
            logs.Add($"Severity: {data.Severity}");
            logs.Add($"Duration: {(data.DurationHours == 0 ? "Permanent" : $"{data.DurationHours} hours")}");

            // TODO: In production, integrate with actual watchlist storage
            // For now, we'll use SystemConfiguration table to store watchlist entries
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var watchlistKey = $"Watchlist_{data.EntityType}_{data.EntityValue}";
            var watchlistEntry = new
            {
                EntityType = data.EntityType.ToString(),
                EntityValue = data.EntityValue,
                Reason = data.Reason,
                Severity = data.Severity,
                AddedAt = DateTime.UtcNow,
                ExpiresAt = data.DurationHours == 0 ? (DateTime?)null : DateTime.UtcNow.AddHours(data.DurationHours),
                EventId = data.EventId
            };

            var existingEntry = await context.SystemConfiguration
                .FirstOrDefaultAsync(sc => sc.Key == watchlistKey, cancellationToken);

            if (existingEntry != null)
            {
                logs.Add($"Entity already on watchlist, updating entry");
                existingEntry.Value = JsonSerializer.Serialize(watchlistEntry);
                existingEntry.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                logs.Add($"Creating new watchlist entry");
                context.SystemConfiguration.Add(new SystemConfiguration
                {
                    Key = watchlistKey,
                    Value = JsonSerializer.Serialize(watchlistEntry),
                    Description = $"Watchlist entry for {data.EntityType}: {data.EntityValue}",
                    UpdatedAt = DateTime.UtcNow
                });
            }

            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Added {EntityType} '{EntityValue}' to watchlist",
                data.EntityType, data.EntityValue);

            var afterState = JsonSerializer.Serialize(new
            {
                WatchlistKey = watchlistKey,
                Entry = watchlistEntry,
                Action = "Added"
            });

            var actionResult = ActionExecutionResult.SuccessResult(
                $"Successfully added {data.EntityType} '{data.EntityValue}' to watchlist",
                afterState);
            actionResult.Logs = logs;
            return actionResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add entity to watchlist");
            var actionResult = ActionExecutionResult.FailureResult(
                $"Failed to add entity to watchlist: {ex.Message}",
                ex.ToString());
            actionResult.Logs = logs;
            return actionResult;
        }
    }

    public async Task<ActionExecutionResult> RollbackAsync(
        object actionData,
        string beforeState,
        CancellationToken cancellationToken = default)
    {
        var data = DeserializeActionData(actionData);
        var logs = new List<string>();

        try
        {
            logs.Add($"Removing {data.EntityType} '{data.EntityValue}' from watchlist");

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var watchlistKey = $"Watchlist_{data.EntityType}_{data.EntityValue}";
            var existingEntry = await context.SystemConfiguration
                .FirstOrDefaultAsync(sc => sc.Key == watchlistKey, cancellationToken);

            if (existingEntry != null)
            {
                context.SystemConfiguration.Remove(existingEntry);
                await context.SaveChangesAsync(cancellationToken);
                logs.Add($"Successfully removed watchlist entry");

                _logger.LogInformation(
                    "Removed {EntityType} '{EntityValue}' from watchlist during rollback",
                    data.EntityType, data.EntityValue);
            }
            else
            {
                logs.Add($"Watchlist entry not found, may have been already removed");
            }

            var actionResult = ActionExecutionResult.SuccessResult(
                $"Successfully removed {data.EntityType} '{data.EntityValue}' from watchlist");
            actionResult.Logs = logs;
            return actionResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rollback watchlist addition");
            var actionResult = ActionExecutionResult.FailureResult(
                $"Failed to remove entity from watchlist: {ex.Message}",
                ex.ToString());
            actionResult.Logs = logs;
            return actionResult;
        }
    }

    public async Task<string> CaptureBeforeStateAsync(object actionData, CancellationToken cancellationToken = default)
    {
        var data = DeserializeActionData(actionData);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var watchlistKey = $"Watchlist_{data.EntityType}_{data.EntityValue}";
        var existingEntry = await context.SystemConfiguration
            .FirstOrDefaultAsync(sc => sc.Key == watchlistKey, cancellationToken);

        var beforeState = new
        {
            WatchlistKey = watchlistKey,
            Existed = existingEntry != null,
            PreviousValue = existingEntry?.Value
        };

        return JsonSerializer.Serialize(beforeState);
    }

    public Task<ValidationResult> ValidateAsync(object actionData)
    {
        try
        {
            var data = DeserializeActionData(actionData);
            var errors = new Dictionary<string, string>();

            if (string.IsNullOrWhiteSpace(data.EntityValue))
            {
                errors[nameof(data.EntityValue)] = "Entity value is required";
            }

            if (string.IsNullOrWhiteSpace(data.Reason))
            {
                errors[nameof(data.Reason)] = "Reason is required";
            }

            if (data.DurationHours < 0)
            {
                errors[nameof(data.DurationHours)] = "Duration cannot be negative";
            }

            // Validate entity value format based on type
            switch (data.EntityType)
            {
                case WatchlistEntityType.IpAddress:
                    if (!System.Net.IPAddress.TryParse(data.EntityValue, out _))
                    {
                        errors[nameof(data.EntityValue)] = "Invalid IP address format";
                    }
                    break;
                case WatchlistEntityType.FileHash:
                    if (data.EntityValue.Length != 64) // SHA256
                    {
                        errors[nameof(data.EntityValue)] = "Invalid file hash format (expected SHA256)";
                    }
                    break;
            }

            return Task.FromResult(errors.Any()
                ? ValidationResult.Invalid(errors)
                : ValidationResult.Valid());
        }
        catch (Exception ex)
        {
            return Task.FromResult(ValidationResult.Invalid($"Validation error: {ex.Message}"));
        }
    }

    private AddToWatchlistActionData DeserializeActionData(object actionData)
    {
        if (actionData is AddToWatchlistActionData data)
        {
            return data;
        }

        if (actionData is JsonElement jsonElement)
        {
            return JsonSerializer.Deserialize<AddToWatchlistActionData>(jsonElement.GetRawText())
                ?? throw new InvalidOperationException("Failed to deserialize action data");
        }

        var json = JsonSerializer.Serialize(actionData);
        return JsonSerializer.Deserialize<AddToWatchlistActionData>(json)
            ?? throw new InvalidOperationException("Failed to deserialize action data");
    }
}
