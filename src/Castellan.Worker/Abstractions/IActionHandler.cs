using Castellan.Worker.Models.Chat;

namespace Castellan.Worker.Abstractions;

/// <summary>
/// Interface for implementing specific security action handlers.
/// Each action type (BlockIP, IsolateHost, etc.) has a concrete implementation.
/// </summary>
public interface IActionHandler
{
    /// <summary>
    /// The type of action this handler supports
    /// </summary>
    ActionType ActionType { get; }

    /// <summary>
    /// Executes the security action.
    /// </summary>
    /// <param name="actionData">Action-specific data (deserialized from JSON)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the action execution</returns>
    Task<ActionExecutionResult> ExecuteAsync(object actionData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back a previously executed action, restoring the system to its previous state.
    /// </summary>
    /// <param name="actionData">Original action data</param>
    /// <param name="beforeState">System state before action was executed (JSON)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the rollback operation</returns>
    Task<ActionExecutionResult> RollbackAsync(
        object actionData,
        string beforeState,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Captures the current system state before executing an action.
    /// This snapshot is used to restore state during rollback.
    /// </summary>
    /// <param name="actionData">Action data to capture state for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JSON string representing system state</returns>
    Task<string> CaptureBeforeStateAsync(object actionData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that the action data is correct and the action can be executed.
    /// </summary>
    /// <param name="actionData">Action data to validate</param>
    /// <returns>Validation result with error message if invalid</returns>
    Task<ValidationResult> ValidateAsync(object actionData);
}

/// <summary>
/// Result of an action execution or rollback
/// </summary>
public class ActionExecutionResult
{
    /// <summary>
    /// Whether the operation succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Human-readable message about the result
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    /// System state after the operation (JSON), if applicable
    /// </summary>
    public string? AfterState { get; set; }

    /// <summary>
    /// Detailed log entries from the operation
    /// </summary>
    public List<string> Logs { get; set; } = new();

    /// <summary>
    /// Exception details if operation failed
    /// </summary>
    public string? ErrorDetails { get; set; }

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static ActionExecutionResult SuccessResult(string message, string? afterState = null)
    {
        return new ActionExecutionResult
        {
            Success = true,
            Message = message,
            AfterState = afterState
        };
    }

    /// <summary>
    /// Creates a failure result
    /// </summary>
    public static ActionExecutionResult FailureResult(string message, string? errorDetails = null)
    {
        return new ActionExecutionResult
        {
            Success = false,
            Message = message,
            ErrorDetails = errorDetails
        };
    }
}

/// <summary>
/// Result of action data validation
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Whether the validation passed
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Error message if validation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Field-specific validation errors
    /// </summary>
    public Dictionary<string, string> FieldErrors { get; set; } = new();

    /// <summary>
    /// Creates a successful validation result
    /// </summary>
    public static ValidationResult Valid()
    {
        return new ValidationResult { IsValid = true };
    }

    /// <summary>
    /// Creates a failed validation result
    /// </summary>
    public static ValidationResult Invalid(string errorMessage)
    {
        return new ValidationResult
        {
            IsValid = false,
            ErrorMessage = errorMessage
        };
    }

    /// <summary>
    /// Creates a failed validation result with field-specific errors
    /// </summary>
    public static ValidationResult Invalid(Dictionary<string, string> fieldErrors)
    {
        return new ValidationResult
        {
            IsValid = false,
            ErrorMessage = "Validation failed for one or more fields",
            FieldErrors = fieldErrors
        };
    }
}
