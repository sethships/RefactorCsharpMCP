namespace RefactorCsharpMCP.Core.Validation;

/// <summary>
/// Standardized error codes for validation and execution failures.
/// Uses HTTP status code analogues for categorization.
/// </summary>
public enum ErrorCode
{
    /// <summary>
    /// No error occurred.
    /// </summary>
    None = 0,

    // Validation Errors (400-series: Client input errors)

    /// <summary>
    /// End-of-life framework specified.
    /// User requested a framework version no longer supported by Microsoft.
    /// </summary>
    EOL_FRAMEWORK = 400,

    /// <summary>
    /// Malformed target framework moniker.
    /// Format does not match expected patterns (net8.0, net48, netstandard2.0).
    /// </summary>
    INVALID_TFM_FORMAT = 401,

    /// <summary>
    /// Required parameter not provided.
    /// A mandatory parameter (e.g., targetFramework) was null or empty.
    /// </summary>
    MISSING_PARAMETER = 402,

    /// <summary>
    /// Valid TFM format but unrecognized framework version.
    /// Framework moniker is well-formed but not in supported frameworks list.
    /// </summary>
    UNKNOWN_FRAMEWORK = 403,

    /// <summary>
    /// Input source code uses syntax incompatible with target framework.
    /// User's code contains C# features not supported by the specified framework.
    /// Example: C# 12 collection expressions in code targeting net48 (C# 7.3).
    /// </summary>
    INPUT_SYNTAX_MISMATCH = 404,

    // Execution Errors (422-series: Semantic/processing errors)

    /// <summary>
    /// Generic refactoring operation failure.
    /// Used when refactoring fails for reasons not covered by specific error codes.
    /// </summary>
    REFACTORING_FAILED = 422,

    /// <summary>
    /// Source code contains syntax errors.
    /// Code cannot be parsed successfully by Roslyn.
    /// </summary>
    SYNTAX_ERROR = 423,

    /// <summary>
    /// Target method not found in source code.
    /// Specified method name or line range does not match any method.
    /// </summary>
    NO_METHOD_FOUND = 424,

    /// <summary>
    /// Target class not found in source code.
    /// Specified class name does not exist in the provided source.
    /// </summary>
    NO_CLASS_FOUND = 425,

    /// <summary>
    /// Data flow analysis could not complete.
    /// Semantic analysis failed to determine variable scope or dependencies.
    /// </summary>
    DATA_FLOW_ANALYSIS_FAILED = 426,

    /// <summary>
    /// Refactored output would generate syntax incompatible with target framework.
    /// Refactoring would produce code using C# features not supported by target framework.
    /// Example: Refactoring generates tuple returns (C# 7.0) for net35 target (C# 3.0).
    /// </summary>
    FRAMEWORK_SYNTAX_MISMATCH = 427,

    /// <summary>
    /// Code uses types or members unavailable in target framework's BCL.
    /// Code compiles syntactically but references APIs not present in target framework.
    /// Example: Using System.Text.Json (net6.0+) when targeting net48.
    /// This differs from typos (SYNTAX_ERROR) - these are real APIs just not available in the target.
    /// </summary>
    FRAMEWORK_API_UNAVAILABLE = 428,

    // Tool Input Validation Errors (410-series: MCP tool input validation)

    /// <summary>
    /// Source code parameter is null or whitespace.
    /// </summary>
    EMPTY_SOURCE_CODE = 410,

    /// <summary>
    /// Source code exceeds maximum size limit (1MB).
    /// </summary>
    SOURCE_CODE_TOO_LARGE = 411,

    /// <summary>
    /// Identifier is not a valid C# identifier.
    /// Identifier is null, whitespace, or doesn't match C# identifier pattern.
    /// </summary>
    INVALID_IDENTIFIER = 412,

    /// <summary>
    /// Line number is out of valid range.
    /// Line number is less than 1 or greater than maximum (typically 100,000).
    /// </summary>
    INVALID_LINE_NUMBER = 413,

    /// <summary>
    /// Column number is out of valid range.
    /// Column number is less than 1 or greater than maximum (typically 10,000).
    /// </summary>
    INVALID_COLUMN_NUMBER = 414,

    /// <summary>
    /// Target framework parameter is null or whitespace.
    /// </summary>
    EMPTY_TARGET_FRAMEWORK = 415
}
