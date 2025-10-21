using Castellan.Worker.Abstractions;
using Castellan.Worker.Models.Actions;
using Castellan.Worker.Models.Chat;
using System.Text.Json;

namespace Castellan.Worker.Services.Actions;

/// <summary>
/// Handler for creating incident tickets in external ticketing systems
/// </summary>
public class CreateTicketActionHandler : IActionHandler
{
    private readonly ILogger<CreateTicketActionHandler> _logger;

    public ActionType ActionType => ActionType.CreateTicket;

    public CreateTicketActionHandler(ILogger<CreateTicketActionHandler> logger)
    {
        _logger = logger;
    }

    public async Task<ActionExecutionResult> ExecuteAsync(object actionData, CancellationToken cancellationToken = default)
    {
        var data = DeserializeActionData(actionData);
        var logs = new List<string>();

        try
        {
            logs.Add($"Creating incident ticket: {data.Title}");
            logs.Add($"Priority: {data.Priority}");
            logs.Add($"Category: {data.Category}");
            logs.Add($"Ticket System: {data.TicketSystem ?? "Internal"}");

            if (!string.IsNullOrWhiteSpace(data.AssignedTo))
            {
                logs.Add($"Assigned to: {data.AssignedTo}");
            }

            if (data.RelatedEventIds.Any())
            {
                logs.Add($"Related events: {string.Join(", ", data.RelatedEventIds)}");
            }

            // Generate unique ticket ID
            var ticketId = $"INC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
            logs.Add($"Generated ticket ID: {ticketId}");

            // Create ticket object
            var ticket = new
            {
                TicketId = ticketId,
                Title = data.Title,
                Description = data.Description,
                Priority = data.Priority,
                Category = data.Category,
                Status = "Open",
                AssignedTo = data.AssignedTo,
                RelatedEventIds = data.RelatedEventIds,
                TicketSystem = data.TicketSystem ?? "CastellanAI Internal",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "CastellanAI",
                ExternalTicketId = (string?)null
            };

            // Integrate with external ticketing system if specified
            if (!string.IsNullOrWhiteSpace(data.TicketSystem))
            {
                var externalResult = await CreateExternalTicketAsync(data, ticketId, cancellationToken);
                if (externalResult.Success)
                {
                    logs.Add($"Successfully created ticket in {data.TicketSystem}");
                    logs.Add($"External ticket ID: {externalResult.ExternalId}");
                    ticket = ticket with { ExternalTicketId = externalResult.ExternalId };
                }
                else
                {
                    logs.Add($"WARNING: Failed to create external ticket: {externalResult.Error}");
                    logs.Add("Ticket created internally only");
                }
            }

            // Store ticket internally (for demonstration, storing in file system)
            var ticketPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "data",
                "tickets",
                $"{ticketId}.json");

            Directory.CreateDirectory(Path.GetDirectoryName(ticketPath)!);

            await File.WriteAllTextAsync(
                ticketPath,
                JsonSerializer.Serialize(ticket, new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);

            logs.Add($"Ticket saved internally: {ticketPath}");

            _logger.LogInformation(
                "Created incident ticket {TicketId}: {Title}",
                ticketId, data.Title);

            var afterState = JsonSerializer.Serialize(new
            {
                TicketId = ticketId,
                InternalTicketPath = ticketPath,
                ExternalTicketId = ticket.ExternalTicketId,
                TicketSystem = ticket.TicketSystem,
                Ticket = ticket
            });

            var actionResult1 = ActionExecutionResult.SuccessResult(
                $"Successfully created ticket {ticketId}",
                afterState); actionResult1.Logs = logs; return actionResult1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while creating ticket");
            var actionResult2 = ActionExecutionResult.FailureResult(
                $"Exception while creating ticket: {ex.Message}",
                ex.ToString()); actionResult2.Logs = logs; return actionResult2;
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
            logs.Add("Closing/canceling ticket");

            // Parse after state to get ticket details
            var stateDoc = JsonDocument.Parse(beforeState);
            var ticketId = stateDoc.RootElement.GetProperty("TicketId").GetString();
            var ticketPath = stateDoc.RootElement.GetProperty("InternalTicketPath").GetString();
            var externalTicketId = stateDoc.RootElement.TryGetProperty("ExternalTicketId", out var extId) && extId.ValueKind != JsonValueKind.Null
                ? extId.GetString()
                : null;

            if (string.IsNullOrEmpty(ticketId))
            {
                var actionResult1 = ActionExecutionResult.FailureResult(
                    "Invalid rollback state: missing ticket ID"); actionResult1.Logs = logs; return actionResult1;
            }

            logs.Add($"Ticket ID: {ticketId}");

            // Mark ticket as canceled/closed internally
            if (!string.IsNullOrEmpty(ticketPath) && File.Exists(ticketPath))
            {
                var ticketJson = await File.ReadAllTextAsync(ticketPath, cancellationToken);
                var ticketDoc = JsonDocument.Parse(ticketJson);
                var ticketData = JsonSerializer.Deserialize<Dictionary<string, object>>(ticketJson) ?? new Dictionary<string, object>();

                ticketData["Status"] = "Canceled";
                ticketData["CanceledAt"] = DateTime.UtcNow;
                ticketData["CanceledReason"] = "Action rolled back";

                await File.WriteAllTextAsync(
                    ticketPath,
                    JsonSerializer.Serialize(ticketData, new JsonSerializerOptions { WriteIndented = true }),
                    cancellationToken);

                logs.Add("Internal ticket marked as canceled");
            }

            // Cancel external ticket if it exists
            if (!string.IsNullOrEmpty(externalTicketId) && !string.IsNullOrEmpty(data.TicketSystem))
            {
                var cancelResult = await CancelExternalTicketAsync(data.TicketSystem, externalTicketId, cancellationToken);
                if (cancelResult.Success)
                {
                    logs.Add($"Successfully canceled external ticket in {data.TicketSystem}");
                }
                else
                {
                    logs.Add($"WARNING: Failed to cancel external ticket: {cancelResult.Error}");
                    logs.Add("Ticket may need manual closure");
                }
            }

            _logger.LogInformation(
                "Canceled ticket {TicketId} during rollback",
                ticketId);

            var actionResult2 = ActionExecutionResult.SuccessResult(
                $"Successfully canceled ticket {ticketId}"); actionResult2.Logs = logs; return actionResult2;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while rolling back ticket creation");
            var actionResult3 = ActionExecutionResult.FailureResult(
                $"Exception during rollback: {ex.Message}",
                ex.ToString()); actionResult3.Logs = logs; return actionResult3;
        }
    }

    public Task<string> CaptureBeforeStateAsync(object actionData, CancellationToken cancellationToken = default)
    {
        var beforeState = new
        {
            TicketId = (string?)null, // Will be generated during execution
            InternalTicketPath = (string?)null, // Will be determined during execution
            ExternalTicketId = (string?)null, // Will be created during execution if applicable
            TicketSystem = (string?)null
        };

        return Task.FromResult(JsonSerializer.Serialize(beforeState));
    }

    public Task<ValidationResult> ValidateAsync(object actionData)
    {
        try
        {
            var data = DeserializeActionData(actionData);
            var errors = new Dictionary<string, string>();

            if (string.IsNullOrWhiteSpace(data.Title))
            {
                errors[nameof(data.Title)] = "Title is required";
            }
            else if (data.Title.Length > 200)
            {
                errors[nameof(data.Title)] = "Title must be 200 characters or less";
            }

            if (string.IsNullOrWhiteSpace(data.Description))
            {
                errors[nameof(data.Description)] = "Description is required";
            }

            var validPriorities = new[] { "Low", "Medium", "High", "Critical" };
            if (!validPriorities.Contains(data.Priority, StringComparer.OrdinalIgnoreCase))
            {
                errors[nameof(data.Priority)] = $"Priority must be one of: {string.Join(", ", validPriorities)}";
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

    private CreateTicketActionData DeserializeActionData(object actionData)
    {
        if (actionData is CreateTicketActionData data)
        {
            return data;
        }

        if (actionData is JsonElement jsonElement)
        {
            return JsonSerializer.Deserialize<CreateTicketActionData>(jsonElement.GetRawText())
                ?? throw new InvalidOperationException("Failed to deserialize action data");
        }

        var json = JsonSerializer.Serialize(actionData);
        return JsonSerializer.Deserialize<CreateTicketActionData>(json)
            ?? throw new InvalidOperationException("Failed to deserialize action data");
    }

    /// <summary>
    /// Creates a ticket in an external ticketing system (Jira, ServiceNow, etc.)
    /// This is a placeholder implementation that would be replaced with actual API calls
    /// </summary>
    private async Task<ExternalTicketResult> CreateExternalTicketAsync(
        CreateTicketActionData data,
        string internalTicketId,
        CancellationToken cancellationToken)
    {
        // TODO: Implement actual integration with external ticketing systems
        // For now, simulate the call
        await Task.Delay(100, cancellationToken); // Simulate API call

        _logger.LogWarning(
            "External ticketing system '{System}' integration not implemented, simulating",
            data.TicketSystem);

        // Simulated external ticket ID
        var externalId = $"{data.TicketSystem?.ToUpper()}-{Random.Shared.Next(10000, 99999)}";

        return new ExternalTicketResult
        {
            Success = true,
            ExternalId = externalId,
            Error = null
        };
    }

    /// <summary>
    /// Cancels/closes a ticket in an external ticketing system
    /// This is a placeholder implementation that would be replaced with actual API calls
    /// </summary>
    private async Task<ExternalTicketResult> CancelExternalTicketAsync(
        string ticketSystem,
        string externalTicketId,
        CancellationToken cancellationToken)
    {
        // TODO: Implement actual integration with external ticketing systems
        await Task.Delay(100, cancellationToken); // Simulate API call

        _logger.LogWarning(
            "External ticketing system '{System}' integration not implemented, simulating cancel",
            ticketSystem);

        return new ExternalTicketResult
        {
            Success = true,
            ExternalId = externalTicketId,
            Error = null
        };
    }

    private class ExternalTicketResult
    {
        public bool Success { get; set; }
        public string? ExternalId { get; set; }
        public string? Error { get; set; }
    }
}
