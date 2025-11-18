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

    /// <summary>
    /// Invalid target framework specified.
    /// For IntroduceParameterObject: Framework validation failed.
    /// </summary>
    INVALID_FRAMEWORK = 405,

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
    EMPTY_TARGET_FRAMEWORK = 415,

    /// <summary>
    /// Line range is invalid (startLine > endLine or < 1).
    /// For ExtractMethod: Invalid line range specified for code extraction.
    /// </summary>
    INVALID_LINE_RANGE = 416,

    // Additional Execution Errors (429-434: Operation-specific failures)

    /// <summary>
    /// No statements found in specified line range.
    /// For ExtractMethod: Selected code range contains no extractable statements.
    /// </summary>
    NO_STATEMENTS_FOUND = 429,

    /// <summary>
    /// Variable cannot be inlined (used in multiple locations, complex initializer, etc.).
    /// For InlineVariable: Variable has multiple references or non-trivial initialization that prevents inlining.
    /// </summary>
    VARIABLE_NOT_INLINABLE = 430,

    /// <summary>
    /// Method cannot be inlined (multiple callers, complex body, etc.).
    /// For InlineMethod: Method is called from multiple locations or has complex logic that prevents inlining.
    /// </summary>
    METHOD_NOT_INLINABLE = 431,

    /// <summary>
    /// Method has no callers and cannot be inlined.
    /// For InlineMethod: Cannot inline a method that is never called.
    /// </summary>
    METHOD_HAS_NO_CALLERS = 432,

    /// <summary>
    /// Specified parameter not found in method signature.
    /// For ConstructorInjection: Parameter name does not match any method parameter.
    /// </summary>
    PARAMETER_NOT_FOUND = 433,

    /// <summary>
    /// Field cannot be made readonly (assigned outside constructor).
    /// For MakeFieldReadonly: Field is assigned in locations other than constructors.
    /// </summary>
    FIELD_NOT_ASSIGNABLE = 434,

    /// <summary>
    /// Duplicate class name conflict.
    /// For IntroduceParameterObject: Parameter object name already exists in source code.
    /// </summary>
    DUPLICATE_CLASS_NAME = 435,

    /// <summary>
    /// Semantic model error.
    /// For IntroduceParameterObject: Unable to resolve symbol using semantic model.
    /// </summary>
    SEMANTIC_MODEL_ERROR = 436,

    /// <summary>
    /// Ref or out parameters cannot be grouped into parameter objects.
    /// For IntroduceParameterObject: Records/classes don't support ref/out parameters.
    /// </summary>
    REF_OUT_PARAMETER_UNSUPPORTED = 437,

    /// <summary>
    /// Optional parameters cannot be grouped into parameter objects.
    /// For IntroduceParameterObject: Default values would be lost in parameter object.
    /// </summary>
    OPTIONAL_PARAMETER_UNSUPPORTED = 438,

    /// <summary>
    /// Params parameters cannot be grouped into parameter objects.
    /// For IntroduceParameterObject: Params modifier not supported in parameter objects.
    /// </summary>
    PARAMS_PARAMETER_UNSUPPORTED = 439,

    // File System Errors (440-449: File and directory operations)

    /// <summary>
    /// Invalid file or directory path.
    /// For ProjectFiles: Path is malformed, contains invalid characters, or fails validation.
    /// </summary>
    INVALID_PATH = 440,

    /// <summary>
    /// Project file (.csproj) not found at specified path.
    /// For ProjectFiles: The .csproj file does not exist at the given location.
    /// </summary>
    PROJECT_FILE_NOT_FOUND = 441,

    /// <summary>
    /// No projects found in specified directory or solution.
    /// For ProjectFiles: Directory contains no .csproj files or solution has no projects.
    /// </summary>
    PROJECT_NOT_FOUND = 442,

    /// <summary>
    /// File is not a valid C# project.
    /// For ProjectFiles: File exists but is not a recognized C# project format.
    /// </summary>
    INVALID_PROJECT_TYPE = 443,

    // Package Management Errors (450-459: NuGet and package operations)

    /// <summary>
    /// No package references found in project(s).
    /// For ProjectFiles: Project has no NuGet package references to manage.
    /// </summary>
    NO_PACKAGES_FOUND = 450,

    /// <summary>
    /// Package version conflict detected.
    /// For ProjectFiles: Multiple projects reference different versions of the same package.
    /// </summary>
    PACKAGE_CONFLICT = 451,

    /// <summary>
    /// Could not resolve package version conflicts.
    /// For ProjectFiles: Automatic conflict resolution failed, manual intervention required.
    /// </summary>
    CONFLICT_RESOLUTION_FAILED = 452,

    /// <summary>
    /// Central Package Management already enabled.
    /// For ProjectFiles: Solution already has CPM configuration (Directory.Packages.props exists).
    /// </summary>
    ALREADY_CPM_ENABLED = 453,

    // Build/Validation Errors (460-469: Build and compilation validation)

    /// <summary>
    /// Build validation failed after refactoring.
    /// For ProjectFiles: Project does not build after applying changes.
    /// </summary>
    BUILD_VALIDATION_FAILED = 460,

    /// <summary>
    /// Project is already in SDK-style format.
    /// For ProjectFiles: Conversion skipped because project already uses SDK-style format.
    /// </summary>
    ALREADY_SDK_STYLE = 461,

    // Lock/Concurrency Errors (470-479: File locking and concurrency)

    /// <summary>
    /// Could not acquire solution lock within timeout period.
    /// For ProjectFiles: Another process holds the solution lock or lock acquisition timed out.
    /// </summary>
    LOCK_ACQUISITION_FAILED = 470,

    /// <summary>
    /// Stale lock file detected from terminated process.
    /// For ProjectFiles: Found orphaned lock file from crashed process, will attempt cleanup.
    /// </summary>
    STALE_LOCK_DETECTED = 471
}
