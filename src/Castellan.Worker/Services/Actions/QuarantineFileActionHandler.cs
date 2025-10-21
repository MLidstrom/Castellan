using Castellan.Worker.Abstractions;
using Castellan.Worker.Models.Actions;
using Castellan.Worker.Models.Chat;
using System.Security.Cryptography;
using System.Text.Json;

namespace Castellan.Worker.Services.Actions;

/// <summary>
/// Handler for quarantining suspicious files by moving them to a quarantine directory
/// </summary>
public class QuarantineFileActionHandler : IActionHandler
{
    private readonly ILogger<QuarantineFileActionHandler> _logger;
    private readonly string _quarantineDirectory;

    public ActionType ActionType => ActionType.QuarantineFile;

    public QuarantineFileActionHandler(ILogger<QuarantineFileActionHandler> logger)
    {
        _logger = logger;
        _quarantineDirectory = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "data",
            "quarantine");

        // Ensure quarantine directory exists
        Directory.CreateDirectory(_quarantineDirectory);
    }

    public async Task<ActionExecutionResult> ExecuteAsync(object actionData, CancellationToken cancellationToken = default)
    {
        var data = DeserializeActionData(actionData);
        var logs = new List<string>();

        try
        {
            logs.Add($"Quarantining file: {data.FilePath}");
            logs.Add($"Reason: {data.Reason}");

            if (!File.Exists(data.FilePath))
            {
                logs.Add($"ERROR: File not found: {data.FilePath}");
                var actionResult1 = ActionExecutionResult.FailureResult(
                    $"File not found: {data.FilePath}",
                    "Cannot quarantine a file that does not exist"); actionResult1.Logs = logs; return actionResult1;
            }

            // Calculate file hash if not provided
            string fileHash = data.FileHash ?? await CalculateFileHashAsync(data.FilePath, cancellationToken);
            logs.Add($"File hash (SHA256): {fileHash}");

            // Create quarantine metadata
            var quarantineInfo = new
            {
                OriginalPath = data.FilePath,
                FileHash = fileHash,
                Reason = data.Reason,
                QuarantinedAt = DateTime.UtcNow,
                YaraRule = data.YaraRuleName,
                EventId = data.EventId,
                OriginalSize = new FileInfo(data.FilePath).Length
            };

            // Generate quarantine filename (hash + timestamp)
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var quarantineFileName = $"{fileHash}_{timestamp}";
            var quarantinePath = Path.Combine(_quarantineDirectory, quarantineFileName);
            var metadataPath = $"{quarantinePath}.json";

            logs.Add($"Quarantine location: {quarantinePath}");

            // Save metadata
            await File.WriteAllTextAsync(
                metadataPath,
                JsonSerializer.Serialize(quarantineInfo, new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);

            logs.Add("Saved quarantine metadata");

            // Move file to quarantine (use Move instead of Copy for security)
            File.Move(data.FilePath, quarantinePath, overwrite: false);
            logs.Add("File moved to quarantine successfully");

            _logger.LogWarning(
                "Quarantined file {FilePath} to {QuarantinePath}. Hash: {FileHash}. Reason: {Reason}",
                data.FilePath, quarantinePath, fileHash, data.Reason);

            var afterState = JsonSerializer.Serialize(new
            {
                OriginalPath = data.FilePath,
                QuarantinePath = quarantinePath,
                MetadataPath = metadataPath,
                FileHash = fileHash,
                QuarantineInfo = quarantineInfo
            });

            var actionResult2 = ActionExecutionResult.SuccessResult(
                $"Successfully quarantined file {Path.GetFileName(data.FilePath)}",
                afterState); actionResult2.Logs = logs; return actionResult2;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while quarantining file {FilePath}", data.FilePath);
            var actionResult3 = ActionExecutionResult.FailureResult(
                $"Exception while quarantining file: {ex.Message}",
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
            logs.Add($"Restoring file from quarantine: {data.FilePath}");

            // Parse after state to get quarantine paths
            var stateDoc = JsonDocument.Parse(beforeState);
            var quarantinePath = stateDoc.RootElement.GetProperty("QuarantinePath").GetString();
            var metadataPath = stateDoc.RootElement.GetProperty("MetadataPath").GetString();
            var originalPath = stateDoc.RootElement.GetProperty("OriginalPath").GetString();

            if (string.IsNullOrEmpty(quarantinePath) || string.IsNullOrEmpty(originalPath))
            {
                var actionResult1 = ActionExecutionResult.FailureResult(
                    "Invalid rollback state: missing quarantine or original path"); actionResult1.Logs = logs; return actionResult1;
            }

            // Check if quarantined file exists
            if (!File.Exists(quarantinePath))
            {
                logs.Add($"WARNING: Quarantined file not found at {quarantinePath}");
                var actionResult2 = ActionExecutionResult.FailureResult(
                    "Quarantined file not found, cannot restore"); actionResult2.Logs = logs; return actionResult2;
            }

            // Check if destination already has a file
            if (File.Exists(originalPath))
            {
                logs.Add($"WARNING: File already exists at original location");
                // Create backup name
                var backupPath = $"{originalPath}.backup_{DateTime.UtcNow:yyyyMMddHHmmss}";
                File.Move(originalPath, backupPath);
                logs.Add($"Moved existing file to {backupPath}");
            }

            // Restore file from quarantine
            File.Move(quarantinePath, originalPath);
            logs.Add($"File restored to original location: {originalPath}");

            // Delete metadata file
            if (!string.IsNullOrEmpty(metadataPath) && File.Exists(metadataPath))
            {
                File.Delete(metadataPath);
                logs.Add("Deleted quarantine metadata");
            }

            _logger.LogInformation(
                "Restored file from quarantine: {QuarantinePath} → {OriginalPath}",
                quarantinePath, originalPath);

            var actionResult3 = ActionExecutionResult.SuccessResult(
                $"Successfully restored file to {originalPath}"); actionResult3.Logs = logs; return actionResult3;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while restoring file from quarantine");
            var actionResult4 = ActionExecutionResult.FailureResult(
                $"Exception during rollback: {ex.Message}",
                ex.ToString()); actionResult4.Logs = logs; return actionResult4;
        }
    }

    public async Task<string> CaptureBeforeStateAsync(object actionData, CancellationToken cancellationToken = default)
    {
        var data = DeserializeActionData(actionData);

        var fileExists = File.Exists(data.FilePath);
        FileInfo? fileInfo = fileExists ? new FileInfo(data.FilePath) : null;

        var beforeState = new
        {
            FilePath = data.FilePath,
            FileExists = fileExists,
            FileSize = fileInfo?.Length,
            LastModified = fileInfo?.LastWriteTimeUtc,
            Attributes = fileInfo?.Attributes.ToString(),
            QuarantinePath = (string?)null, // Will be filled after execution
            MetadataPath = (string?)null // Will be filled after execution
        };

        return await Task.FromResult(JsonSerializer.Serialize(beforeState));
    }

    public Task<ValidationResult> ValidateAsync(object actionData)
    {
        try
        {
            var data = DeserializeActionData(actionData);
            var errors = new Dictionary<string, string>();

            if (string.IsNullOrWhiteSpace(data.FilePath))
            {
                errors[nameof(data.FilePath)] = "File path is required";
            }
            else
            {
                try
                {
                    var fullPath = Path.GetFullPath(data.FilePath);
                    // Check for path traversal attempts
                    if (fullPath.Contains(".."))
                    {
                        errors[nameof(data.FilePath)] = "Invalid file path (path traversal detected)";
                    }
                }
                catch
                {
                    errors[nameof(data.FilePath)] = "Invalid file path format";
                }
            }

            if (string.IsNullOrWhiteSpace(data.Reason))
            {
                errors[nameof(data.Reason)] = "Reason is required";
            }

            // Validate file hash format if provided
            if (!string.IsNullOrWhiteSpace(data.FileHash))
            {
                if (data.FileHash.Length != 64 || !data.FileHash.All(c => "0123456789abcdefABCDEF".Contains(c)))
                {
                    errors[nameof(data.FileHash)] = "Invalid file hash format (expected SHA256)";
                }
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

    private QuarantineFileActionData DeserializeActionData(object actionData)
    {
        if (actionData is QuarantineFileActionData data)
        {
            return data;
        }

        if (actionData is JsonElement jsonElement)
        {
            return JsonSerializer.Deserialize<QuarantineFileActionData>(jsonElement.GetRawText())
                ?? throw new InvalidOperationException("Failed to deserialize action data");
        }

        var json = JsonSerializer.Serialize(actionData);
        return JsonSerializer.Deserialize<QuarantineFileActionData>(json)
            ?? throw new InvalidOperationException("Failed to deserialize action data");
    }

    private async Task<string> CalculateFileHashAsync(string filePath, CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}
