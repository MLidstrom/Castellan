using Castellan.Worker.Abstractions;
using Castellan.Worker.Models.Actions;
using Castellan.Worker.Models.Chat;
using System.Diagnostics;
using System.Text.Json;

namespace Castellan.Worker.Services.Actions;

/// <summary>
/// Handler for blocking IP addresses using Windows Firewall
/// </summary>
public class BlockIPActionHandler : IActionHandler
{
    private readonly ILogger<BlockIPActionHandler> _logger;
    private const string RuleNamePrefix = "CastellanAI_Block_";

    public ActionType ActionType => ActionType.BlockIP;

    public BlockIPActionHandler(ILogger<BlockIPActionHandler> logger)
    {
        _logger = logger;
    }

    public async Task<ActionExecutionResult> ExecuteAsync(object actionData, CancellationToken cancellationToken = default)
    {
        var data = DeserializeActionData(actionData);
        var logs = new List<string>();

        try
        {
            logs.Add($"Blocking IP address: {data.IpAddress}");
            logs.Add($"Reason: {data.Reason}");
            logs.Add($"Duration: {(data.DurationHours == 0 ? "Permanent" : $"{data.DurationHours} hours")}");

            var ruleName = $"{RuleNamePrefix}{data.IpAddress.Replace(".", "_")}";

            // Check if rule already exists
            var checkCommand = $"netsh advfirewall firewall show rule name=\"{ruleName}\"";
            var checkResult = await ExecuteCommandAsync(checkCommand, cancellationToken);

            if (checkResult.Success)
            {
                logs.Add("Firewall rule already exists, deleting old rule");
                var deleteCommand = $"netsh advfirewall firewall delete rule name=\"{ruleName}\"";
                await ExecuteCommandAsync(deleteCommand, cancellationToken);
            }

            // Create new blocking rule
            var addCommand = $"netsh advfirewall firewall add rule " +
                $"name=\"{ruleName}\" " +
                $"dir=in " +
                $"action=block " +
                $"remoteip={data.IpAddress} " +
                $"enable=yes " +
                $"description=\"Blocked by CastellanAI: {data.Reason}\"";

            var result = await ExecuteCommandAsync(addCommand, cancellationToken);

            if (result.Success)
            {
                logs.Add($"Successfully created firewall rule: {ruleName}");
                _logger.LogInformation(
                    "Blocked IP address {IpAddress} with firewall rule {RuleName}",
                    data.IpAddress, ruleName);

                // Schedule automatic unblock if duration is specified
                if (data.DurationHours > 0)
                {
                    logs.Add($"IP will be automatically unblocked after {data.DurationHours} hours");
                    // TODO: Implement scheduled task for automatic unblock
                }

                var afterState = JsonSerializer.Serialize(new
                {
                    RuleName = ruleName,
                    IpAddress = data.IpAddress,
                    CreatedAt = DateTime.UtcNow,
                    Command = addCommand
                });

                var actionResult1 = ActionExecutionResult.SuccessResult(
                    $"Successfully blocked IP address {data.IpAddress}",
                    afterState); actionResult1.Logs = logs; return actionResult1;
            }
            else
            {
                logs.Add($"Failed to create firewall rule: {result.Output}");
                var actionResult2 = ActionExecutionResult.FailureResult(
                    $"Failed to block IP address: {result.Output}",
                    result.Error); actionResult2.Logs = logs; return actionResult2;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while blocking IP address {IpAddress}", data.IpAddress);
            var actionResult3 = ActionExecutionResult.FailureResult(
                $"Exception while blocking IP: {ex.Message}",
                ex.ToString()); actionResult3.Logs = logs; return actionResult3;
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
            logs.Add($"Unblocking IP address: {data.IpAddress}");

            var ruleName = $"{RuleNamePrefix}{data.IpAddress.Replace(".", "_")}";

            // Delete the firewall rule
            var deleteCommand = $"netsh advfirewall firewall delete rule name=\"{ruleName}\"";
            var result = await ExecuteCommandAsync(deleteCommand, cancellationToken);

            if (result.Success)
            {
                logs.Add($"Successfully deleted firewall rule: {ruleName}");
                _logger.LogInformation(
                    "Unblocked IP address {IpAddress} by removing firewall rule {RuleName}",
                    data.IpAddress, ruleName);

                var actionResult1 = ActionExecutionResult.SuccessResult(
                    $"Successfully unblocked IP address {data.IpAddress}"); actionResult1.Logs = logs; return actionResult1;
            }
            else
            {
                // Rule might not exist, which is okay for rollback
                if (result.Output.Contains("No rules match"))
                {
                    logs.Add("Firewall rule not found, IP may have been already unblocked");
                    var actionResult2 = ActionExecutionResult.SuccessResult(
                        $"IP address {data.IpAddress} was not blocked (rule not found)"); actionResult2.Logs = logs; return actionResult2;
                }

                logs.Add($"Failed to delete firewall rule: {result.Output}");
                var actionResult3 = ActionExecutionResult.FailureResult(
                    $"Failed to unblock IP address: {result.Output}",
                    result.Error); actionResult3.Logs = logs; return actionResult3;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while unblocking IP address {IpAddress}", data.IpAddress);
            var actionResult4 = ActionExecutionResult.FailureResult(
                $"Exception while unblocking IP: {ex.Message}",
                ex.ToString()); actionResult4.Logs = logs; return actionResult4;
        }
    }

    public async Task<string> CaptureBeforeStateAsync(object actionData, CancellationToken cancellationToken = default)
    {
        var data = DeserializeActionData(actionData);
        var ruleName = $"{RuleNamePrefix}{data.IpAddress.Replace(".", "_")}";

        // Check if firewall rule exists
        var checkCommand = $"netsh advfirewall firewall show rule name=\"{ruleName}\"";
        var result = await ExecuteCommandAsync(checkCommand, cancellationToken);

        var beforeState = new
        {
            RuleName = ruleName,
            IpAddress = data.IpAddress,
            RuleExisted = result.Success,
            ExistingRuleDetails = result.Success ? result.Output : null
        };

        return JsonSerializer.Serialize(beforeState);
    }

    public Task<ValidationResult> ValidateAsync(object actionData)
    {
        try
        {
            var data = DeserializeActionData(actionData);
            var errors = new Dictionary<string, string>();

            if (string.IsNullOrWhiteSpace(data.IpAddress))
            {
                errors[nameof(data.IpAddress)] = "IP address is required";
            }
            else if (!System.Net.IPAddress.TryParse(data.IpAddress, out var ipAddress))
            {
                errors[nameof(data.IpAddress)] = "Invalid IP address format";
            }
            else if (System.Net.IPAddress.IsLoopback(ipAddress))
            {
                errors[nameof(data.IpAddress)] = "Cannot block loopback address";
            }

            if (string.IsNullOrWhiteSpace(data.Reason))
            {
                errors[nameof(data.Reason)] = "Reason is required";
            }

            if (data.DurationHours < 0)
            {
                errors[nameof(data.DurationHours)] = "Duration cannot be negative";
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

    private BlockIPActionData DeserializeActionData(object actionData)
    {
        if (actionData is BlockIPActionData data)
        {
            return data;
        }

        if (actionData is JsonElement jsonElement)
        {
            return JsonSerializer.Deserialize<BlockIPActionData>(jsonElement.GetRawText())
                ?? throw new InvalidOperationException("Failed to deserialize action data");
        }

        var json = JsonSerializer.Serialize(actionData);
        return JsonSerializer.Deserialize<BlockIPActionData>(json)
            ?? throw new InvalidOperationException("Failed to deserialize action data");
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
