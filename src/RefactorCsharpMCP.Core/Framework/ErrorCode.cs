namespace RefactorCsharpMCP.Core.Framework;

/// <summary>
/// Standardized error codes for programmatic handling by AI agents and clients.
/// Error codes are organized by category, similar to HTTP status codes.
/// </summary>
public enum ErrorCode
{
    /// <summary>
    /// No error occurred.
    /// </summary>
    None = 0,

    // Validation Errors (400-series HTTP analogues)

    /// <summary>
    /// End-of-life framework specified.
    /// </summary>
    EOL_FRAMEWORK = 400,

    /// <summary>
    /// Malformed TFM string.
    /// </summary>
    INVALID_TFM_FORMAT = 401,

    /// <summary>
    /// Required parameter not provided.
    /// </summary>
    MISSING_PARAMETER = 402,

    /// <summary>
    /// Valid format but unrecognized version.
    /// </summary>
    UNKNOWN_FRAMEWORK = 403,

    // Execution Errors (422-series HTTP analogues)

    /// <summary>
    /// Generic refactoring failure.
    /// </summary>
    REFACTORING_FAILED = 422,

    /// <summary>
    /// Source code has syntax errors.
    /// </summary>
    SYNTAX_ERROR = 423,

    /// <summary>
    /// Target method not found in source.
    /// </summary>
    NO_METHOD_FOUND = 424,

    /// <summary>
    /// Target class not found in source.
    /// </summary>
    NO_CLASS_FOUND = 425,

    /// <summary>
    /// Data flow analysis unsuccessful.
    /// </summary>
    DATA_FLOW_ANALYSIS_FAILED = 426
}
