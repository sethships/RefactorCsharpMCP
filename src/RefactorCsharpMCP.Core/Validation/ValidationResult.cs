using System.Text.Json.Serialization;

namespace RefactorCsharpMCP.Core.Validation;

/// <summary>
/// Represents the result of a validation operation using the Result Pattern.
/// Provides structured error information without throwing exceptions.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Gets a value indicating whether the validation succeeded.
    /// </summary>
    [JsonPropertyName("success")]
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the error code if validation failed; otherwise, null.
    /// </summary>
    [JsonIgnore] // Internal use only, not serialized to MCP
    public ErrorCode? ErrorCode { get; init; }

    /// <summary>
    /// Gets the human-readable error message if validation failed; otherwise, null.
    /// Serializes as "message" for MCP compatibility.
    /// </summary>
    [JsonPropertyName("message")]
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the brief error description.
    /// For MCP tool validation, this returns the same as ErrorMessage.
    /// For framework validation, this can be customized.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error => ErrorMessage;

    /// <summary>
    /// Gets the suggested action to resolve the error; otherwise, null.
    /// Example: "Update targetFramework to net8.0 or modify input code to use compatible syntax."
    /// </summary>
    [JsonIgnore] // For framework validation only, not needed in MCP tool responses
    public string? SuggestedAction { get; init; }

    /// <summary>
    /// Gets additional contextual information about the error.
    /// May include detected C# features, framework versions, line numbers, etc.
    /// </summary>
    [JsonIgnore] // For framework validation only, not needed in MCP tool responses
    public Dictionary<string, object>? ErrorContext { get; init; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success()
    {
        return new ValidationResult
        {
            IsValid = true,
            ErrorCode = null,
            ErrorMessage = null,
            SuggestedAction = null,
            ErrorContext = null
        };
    }

    /// <summary>
    /// Creates a failed validation result with error details.
    /// </summary>
    /// <param name="errorCode">The error code categorizing the failure.</param>
    /// <param name="errorMessage">Human-readable error description.</param>
    /// <param name="suggestedAction">Actionable guidance for resolving the error (optional).</param>
    /// <param name="errorContext">Additional contextual data (optional).</param>
    public static ValidationResult Failure(
        ErrorCode errorCode,
        string errorMessage,
        string? suggestedAction = null,
        Dictionary<string, object>? errorContext = null)
    {
        return new ValidationResult
        {
            IsValid = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            SuggestedAction = suggestedAction,
            ErrorContext = errorContext
        };
    }

    /// <summary>
    /// Creates a validation result for input syntax mismatch errors.
    /// </summary>
    /// <param name="detectedFeature">The C# feature detected (e.g., "collection expressions").</param>
    /// <param name="requiredVersion">The minimum C# version required (e.g., "C# 12").</param>
    /// <param name="targetFramework">The target framework specified by user.</param>
    /// <param name="supportedVersion">The C# version supported by target framework.</param>
    public static ValidationResult InputSyntaxMismatch(
        string detectedFeature,
        string requiredVersion,
        string targetFramework,
        string supportedVersion)
    {
        var message = $"Input code uses {detectedFeature} ({requiredVersion}), but target framework {targetFramework} supports {supportedVersion}.";
        var action = $"Either update targetFramework to a version supporting {requiredVersion} or modify input code to use compatible syntax.";

        var context = new Dictionary<string, object>
        {
            { "detectedFeature", detectedFeature },
            { "requiredVersion", requiredVersion },
            { "targetFramework", targetFramework },
            { "supportedVersion", supportedVersion }
        };

        return Failure(Validation.ErrorCode.INPUT_SYNTAX_MISMATCH, message, action, context);
    }

    /// <summary>
    /// Creates a validation result for framework syntax mismatch errors.
    /// </summary>
    /// <param name="generatedFeature">The C# feature that would be generated.</param>
    /// <param name="requiredVersion">The minimum C# version required for the feature.</param>
    /// <param name="targetFramework">The target framework specified by user.</param>
    /// <param name="supportedVersion">The C# version supported by target framework.</param>
    public static ValidationResult FrameworkSyntaxMismatch(
        string generatedFeature,
        string requiredVersion,
        string targetFramework,
        string supportedVersion)
    {
        var message = $"This refactoring would generate {generatedFeature} ({requiredVersion}), but target framework {targetFramework} supports {supportedVersion}.";
        var action = $"Either update targetFramework to a version supporting {requiredVersion} or manually refactor using compatible patterns.";

        var context = new Dictionary<string, object>
        {
            { "generatedFeature", generatedFeature },
            { "requiredVersion", requiredVersion },
            { "targetFramework", targetFramework },
            { "supportedVersion", supportedVersion }
        };

        return Failure(Validation.ErrorCode.FRAMEWORK_SYNTAX_MISMATCH, message, action, context);
    }

    /// <summary>
    /// Creates a validation result for syntax errors.
    /// </summary>
    /// <param name="errorDetails">Roslyn diagnostic error messages.</param>
    public static ValidationResult SyntaxError(string errorDetails)
    {
        var message = $"Source code contains syntax errors: {errorDetails}";
        var action = "Fix syntax errors in source code before attempting refactoring.";

        return Failure(Validation.ErrorCode.SYNTAX_ERROR, message, action);
    }

    /// <summary>
    /// Creates a simple tool input validation failure.
    /// Simplified factory method for MCP tool input validation (Issue #122).
    /// </summary>
    /// <param name="errorCode">The error code for the validation failure.</param>
    /// <param name="error">Brief error description.</param>
    /// <param name="operationName">Name of the operation that failed.</param>
    /// <returns>A ValidationResult indicating tool input validation failure.</returns>
    public static ValidationResult ToolInputError(ErrorCode errorCode, string error, string operationName)
    {
        var message = $"{operationName} failed: {error}";
        return Failure(errorCode, message);
    }
}
