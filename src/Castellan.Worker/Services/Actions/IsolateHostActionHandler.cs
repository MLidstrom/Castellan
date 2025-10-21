using Castellan.Worker.Abstractions;
using Castellan.Worker.Models.Actions;
using Castellan.Worker.Models.Chat;
using System.Diagnostics;
using System.Text.Json;

namespace Castellan.Worker.Services.Actions;

/// <summary>
/// Handler for isolating a host from the network by disabling network adapters
/// </summary>
public class IsolateHostActionHandler : IActionHandler
{
    private readonly ILogger<IsolateHostActionHandler> _logger;

    public ActionType ActionType => ActionType.IsolateHost;

    public IsolateHostActionHandler(ILogger<IsolateHostActionHandler> logger)
    {
        _logger = logger;
    }

    public async Task<ActionExecutionResult> ExecuteAsync(object actionData, CancellationToken cancellationToken = default)
    {
        var data = DeserializeActionData(actionData);
        var logs = new List<string>();

        try
        {
            logs.Add($"Isolating host: {data.Hostname}");
            logs.Add($"Reason: {data.Reason}");
            logs.Add($"Disable all adapters: {data.DisableAllAdapters}");

            // Get current machine name
            var currentMachine = Environment.MachineName;

            if (!data.Hostname.Equals(currentMachine, StringComparison.OrdinalIgnoreCase))
            {
                // Remote machine isolation would require WMI/PowerShell remoting
                logs.Add($"WARNING: Remote host isolation not fully implemented");
                logs.Add($"Would isolate remote host '{data.Hostname}' via WMI/PowerShell remoting");

                // For demonstration, simulate the action
                _logger.LogWarning(
                    "Remote host isolation requested for {Hostname} but not fully implemented",
                    data.Hostname);

                var afterState = JsonSerializer.Serialize(new
                {
                    Hostname = data.Hostname,
                    IsLocal = false,
                    Simulated = true,
                    Adapters = new[] { "Simulated Adapter" }
                });

                var actionResult1 = ActionExecutionResult.SuccessResult(
                    $"Simulated isolation of remote host {data.Hostname}",
                    afterState); actionResult1.Logs = logs; return actionResult1;
            }

            // Local machine - disable network adapters
            logs.Add("Isolating local machine");

            var adapters = await GetNetworkAdaptersAsync(cancellationToken);
            logs.Add($"Found {adapters.Count} network adapter(s)");

            var disabledAdapters = new List<string>();

            foreach (var adapter in adapters)
            {
                // Skip loopback and tunnel adapters
                if (adapter.Contains("Loopback") || adapter.Contains("Tunnel"))
                {
                    logs.Add($"Skipping adapter: {adapter}");
                    continue;
                }

                var result = await DisableAdapterAsync(adapter, cancellationToken);
                if (result.Success)
                {
                    disabledAdapters.Add(adapter);
                    logs.Add($"Disabled adapter: {adapter}");
                }
                else
                {
                    logs.Add($"Failed to disable adapter {adapter}: {result.Error}");
                }
            }

            if (disabledAdapters.Any())
            {
                _logger.LogWarning(
                    "Isolated local host by disabling {Count} network adapter(s)",
                    disabledAdapters.Count);

                var afterState = JsonSerializer.Serialize(new
                {
                    Hostname = currentMachine,
                    IsLocal = true,
                    DisabledAdapters = disabledAdapters,
                    Timestamp = DateTime.UtcNow
                });

                var actionResult2 = ActionExecutionResult.SuccessResult(
                    $"Successfully isolated host by disabling {disabledAdapters.Count} adapter(s)",
                    afterState); actionResult2.Logs = logs; return actionResult2;
            }
            else
            {
                var actionResult3 = ActionExecutionResult.FailureResult(
                    "No adapters were disabled",
                    "Could not find any network adapters to disable"); actionResult3.Logs = logs; return actionResult3;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while isolating host {Hostname}", data.Hostname);
            var actionResult4 = ActionExecutionResult.FailureResult(
                $"Exception while isolating host: {ex.Message}",
                ex.ToString()); actionResult4.Logs = logs; return actionResult4;
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
            logs.Add($"Re-enabling network adapters for host: {data.Hostname}");

            // Parse before state to get disabled adapters
            var stateDoc = JsonDocument.Parse(beforeState);
            var disabledAdapters = stateDoc.RootElement
                .GetProperty("DisabledAdapters")
                .EnumerateArray()
                .Select(e => e.GetString() ?? "")
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            logs.Add($"Re-enabling {disabledAdapters.Count} adapter(s)");

            var enabledAdapters = new List<string>();

            foreach (var adapter in disabledAdapters)
            {
                var result = await EnableAdapterAsync(adapter, cancellationToken);
                if (result.Success)
                {
                    enabledAdapters.Add(adapter);
                    logs.Add($"Enabled adapter: {adapter}");
                }
                else
                {
                    logs.Add($"Failed to enable adapter {adapter}: {result.Error}");
                }
            }

            _logger.LogInformation(
                "Re-enabled {Count} network adapter(s) for host {Hostname}",
                enabledAdapters.Count, data.Hostname);

            var actionResult1 = ActionExecutionResult.SuccessResult(
                $"Successfully re-enabled {enabledAdapters.Count} adapter(s)"); actionResult1.Logs = logs; return actionResult1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while rolling back host isolation for {Hostname}", data.Hostname);
            var actionResult2 = ActionExecutionResult.FailureResult(
                $"Exception during rollback: {ex.Message}",
                ex.ToString()); actionResult2.Logs = logs; return actionResult2;
        }
    }

    public async Task<string> CaptureBeforeStateAsync(object actionData, CancellationToken cancellationToken = default)
    {
        var data = DeserializeActionData(actionData);

        var adapters = await GetNetworkAdaptersAsync(cancellationToken);
        var enabledAdapters = new List<string>();

        foreach (var adapter in adapters)
        {
            var status = await GetAdapterStatusAsync(adapter, cancellationToken);
            if (status.Contains("Enabled", StringComparison.OrdinalIgnoreCase))
            {
                enabledAdapters.Add(adapter);
            }
        }

        var beforeState = new
        {
            Hostname = data.Hostname,
            CapturedAt = DateTime.UtcNow,
            AllAdapters = adapters,
            EnabledAdapters = enabledAdapters,
            DisabledAdapters = new List<string>() // Will be filled after execution
        };

        return JsonSerializer.Serialize(beforeState);
    }

    public Task<ValidationResult> ValidateAsync(object actionData)
    {
        try
        {
            var data = DeserializeActionData(actionData);
            var errors = new Dictionary<string, string>();

            if (string.IsNullOrWhiteSpace(data.Hostname))
            {
                errors[nameof(data.Hostname)] = "Hostname is required";
            }

            if (string.IsNullOrWhiteSpace(data.Reason))
            {
                errors[nameof(data.Reason)] = "Reason is required";
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

    private IsolateHostActionData DeserializeActionData(object actionData)
    {
        if (actionData is IsolateHostActionData data)
        {
            return data;
        }

        if (actionData is JsonElement jsonElement)
        {
            return JsonSerializer.Deserialize<IsolateHostActionData>(jsonElement.GetRawText())
                ?? throw new InvalidOperationException("Failed to deserialize action data");
        }

        var json = JsonSerializer.Serialize(actionData);
        return JsonSerializer.Deserialize<IsolateHostActionData>(json)
            ?? throw new InvalidOperationException("Failed to deserialize action data");
    }

    private async Task<List<string>> GetNetworkAdaptersAsync(CancellationToken cancellationToken)
    {
        var command = "netsh interface show interface";
        var result = await ExecuteCommandAsync(command, cancellationToken);

        if (!result.Success)
        {
            return new List<string>();
        }

        // Parse output to extract adapter names
        var adapters = result.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Skip(3) // Skip header lines
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        return adapters!;
    }

    private async Task<string> GetAdapterStatusAsync(string adapterName, CancellationToken cancellationToken)
    {
        var command = $"netsh interface show interface name=\"{adapterName}\"";
        var result = await ExecuteCommandAsync(command, cancellationToken);
        return result.Success ? result.Output : "Unknown";
    }

    private async Task<CommandResult> DisableAdapterAsync(string adapterName, CancellationToken cancellationToken)
    {
        var command = $"netsh interface set interface name=\"{adapterName}\" admin=disabled";
        return await ExecuteCommandAsync(command, cancellationToken);
    }

    private async Task<CommandResult> EnableAdapterAsync(string adapterName, CancellationToken cancellationToken)
    {
        var command = $"netsh interface set interface name=\"{adapterName}\" admin=enabled";
        return await ExecuteCommandAsync(command, cancellationToken);
    }

    private async Task<CommandResult> ExecuteCommandAsync(string command, CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken);

            var output = await outputTask;
            var error = await errorTask;

            return new CommandResult
            {
                Success = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                Output = output,
                Error = error
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute command: {Command}", command);
            return new CommandResult
            {
                Success = false,
                ExitCode = -1,
                Output = "",
                Error = ex.Message
            };
        }
    }

    private class CommandResult
    {
        public bool Success { get; set; }
        public int ExitCode { get; set; }
        public string Output { get; set; } = "";
        public string Error { get; set; } = "";
    }
}
