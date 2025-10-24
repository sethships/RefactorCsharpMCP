using Microsoft.CodeAnalysis.Text;

namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Provides structured context for refactoring errors, enabling detailed logging and diagnostics
/// while returning sanitized user-facing messages.
/// </summary>
public class RefactoringErrorContext
{
    /// <summary>
    /// Gets or sets the error category for classification.
    /// </summary>
    public ErrorCategory Category { get; set; }

    /// <summary>
    /// Gets or sets the refactoring phase where the error occurred.
    /// </summary>
    public string Phase { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source location where the error occurred, if available.
    /// </summary>
    public LinePosition? SourceLocation { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the error occurred.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the full exception message for logging purposes.
    /// This should NOT be exposed to end users.
    /// </summary>
    public string FullExceptionMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the exception type name for telemetry.
    /// </summary>
    public string ExceptionType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional context data for debugging.
    /// </summary>
    public Dictionary<string, string> AdditionalContext { get; set; } = new();

    /// <summary>
    /// Generates a sanitized, user-friendly error message suitable for display.
    /// </summary>
    /// <returns>A user-facing error message.</returns>
    public string ToUserMessage()
    {
        var categoryMessage = Category switch
        {
            ErrorCategory.InvalidInput => "Invalid input provided",
            ErrorCategory.InvalidState => "Invalid operation state",
            ErrorCategory.ParseError => "Code parsing error",
            ErrorCategory.SymbolResolution => "Symbol resolution failed",
            ErrorCategory.ValidationFailure => "Validation failed",
            ErrorCategory.UnexpectedError => "Unexpected error occurred",
            _ => "Error occurred"
        };

        if (SourceLocation.HasValue)
        {
            return $"{categoryMessage} at line {SourceLocation.Value.Line + 1}, column {SourceLocation.Value.Character + 1} during {Phase}. Please check the code and try again.";
        }

        return $"{categoryMessage} during {Phase}. Please check the code and try again.";
    }

    /// <summary>
    /// Generates a detailed message for logging purposes (should not be shown to users).
    /// </summary>
    /// <returns>A detailed log message with full context.</returns>
    public string ToLogMessage()
    {
        var locationInfo = SourceLocation.HasValue
            ? $"Line {SourceLocation.Value.Line + 1}, Column {SourceLocation.Value.Character + 1}"
            : "Unknown location";

        var contextInfo = AdditionalContext.Any()
            ? string.Join(", ", AdditionalContext.Select(kv => $"{kv.Key}={kv.Value}"))
            : "No additional context";

        return $"[{Timestamp:O}] Error in {Phase} - Category: {Category}, " +
               $"Type: {ExceptionType}, Location: {locationInfo}, " +
               $"Message: {FullExceptionMessage}, Context: {contextInfo}";
    }

    /// <summary>
    /// Creates an error context from an exception.
    /// </summary>
    /// <param name="exception">The exception to create context from.</param>
    /// <param name="phase">The refactoring phase where the error occurred.</param>
    /// <param name="sourceLocation">Optional source location.</param>
    /// <returns>A RefactoringErrorContext instance.</returns>
    public static RefactoringErrorContext FromException(
        Exception exception,
        string phase,
        LinePosition? sourceLocation = null)
    {
        var category = exception switch
        {
            ArgumentException or ArgumentNullException => ErrorCategory.InvalidInput,
            InvalidOperationException => ErrorCategory.InvalidState,
            FormatException => ErrorCategory.ParseError,
            _ => ErrorCategory.UnexpectedError
        };

        return new RefactoringErrorContext
        {
            Category = category,
            Phase = phase,
            SourceLocation = sourceLocation,
            FullExceptionMessage = exception.Message,
            ExceptionType = exception.GetType().Name,
            Timestamp = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Categorizes refactoring errors for better classification and handling.
/// </summary>
public enum ErrorCategory
{
    /// <summary>
    /// Error caused by invalid input parameters.
    /// </summary>
    InvalidInput,

    /// <summary>
    /// Error caused by invalid operation state.
    /// </summary>
    InvalidState,

    /// <summary>
    /// Error during code parsing.
    /// </summary>
    ParseError,

    /// <summary>
    /// Error during symbol resolution.
    /// </summary>
    SymbolResolution,

    /// <summary>
    /// Error during validation.
    /// </summary>
    ValidationFailure,

    /// <summary>
    /// Unexpected error not fitting other categories.
    /// </summary>
    UnexpectedError
}
