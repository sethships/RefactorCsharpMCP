namespace RefactorCsharpMCP.Core.Framework;

/// <summary>
/// Result object for framework validation operations using the Result Pattern.
/// Provides structured error information and recovery guidance.
/// </summary>
public class FrameworkValidationResult
{
    /// <summary>
    /// Gets whether the TFM format is syntactically correct.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets whether Microsoft currently supports this framework.
    /// </summary>
    public bool IsSupported { get; init; }

    /// <summary>
    /// Gets whether this framework has reached end-of-life.
    /// </summary>
    public bool IsEOL { get; init; }

    /// <summary>
    /// Gets the standardized error code for programmatic handling (null if successful).
    /// </summary>
    public ErrorCode? ErrorCode { get; init; }

    /// <summary>
    /// Gets the human-readable error description (null if successful).
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the recommended replacement TFM for EOL frameworks (null if not EOL).
    /// </summary>
    public string? SuggestedFramework { get; init; }

    /// <summary>
    /// Gets guidance for working around EOL framework issues (null if not EOL).
    /// </summary>
    public string? Workaround { get; init; }

    /// <summary>
    /// Gets complete framework metadata when validation succeeds (null if validation failed).
    /// </summary>
    public FrameworkInfo? FrameworkInfo { get; init; }

    /// <summary>
    /// Gets additional contextual data about the error (null if successful).
    /// </summary>
    public Dictionary<string, object>? ErrorContext { get; init; }

    /// <summary>
    /// Creates a successful validation result with framework metadata.
    /// </summary>
    public static FrameworkValidationResult Success(FrameworkInfo frameworkInfo)
    {
        return new FrameworkValidationResult
        {
            IsValid = true,
            IsSupported = true,
            IsEOL = false,
            FrameworkInfo = frameworkInfo
        };
    }

    /// <summary>
    /// Creates an EOL framework error with suggestion for replacement.
    /// </summary>
    public static FrameworkValidationResult EOLError(string tfm, string suggestedFramework, string displayName, DateTime? eolDate)
    {
        var eolDateStr = eolDate.HasValue ? eolDate.Value.ToString("MMMM dd, yyyy") : "an earlier date";
        return new FrameworkValidationResult
        {
            IsValid = true,
            IsSupported = false,
            IsEOL = true,
            ErrorCode = Framework.ErrorCode.EOL_FRAMEWORK,
            ErrorMessage = $"Unsupported framework: {displayName} reached end-of-life on {eolDateStr}. This version is not supported due to security risks and maintenance burden.",
            SuggestedFramework = suggestedFramework,
            Workaround = $"Specify '{suggestedFramework}' as targetFramework parameter and manually verify generated code compatibility.",
            ErrorContext = new Dictionary<string, object>
            {
                ["requested"] = tfm,
                ["isEOL"] = true,
                ["eolDate"] = eolDate?.ToString("yyyy-MM-dd") ?? "unknown"
            }
        };
    }

    /// <summary>
    /// Creates an invalid TFM format error with examples.
    /// </summary>
    public static FrameworkValidationResult InvalidFormatError(string tfm)
    {
        return new FrameworkValidationResult
        {
            IsValid = false,
            IsSupported = false,
            IsEOL = false,
            ErrorCode = Framework.ErrorCode.INVALID_TFM_FORMAT,
            ErrorMessage = $"Invalid framework moniker: '{tfm}'. Must be valid TFM like 'net8.0', 'net48', 'netstandard2.0'.",
            ErrorContext = new Dictionary<string, object>
            {
                ["requested"] = tfm,
                ["validExamples"] = new[] { "net8.0", "net48", "net462", "netstandard2.0" }
            }
        };
    }

    /// <summary>
    /// Creates a missing parameter error.
    /// </summary>
    public static FrameworkValidationResult MissingParameterError()
    {
        return new FrameworkValidationResult
        {
            IsValid = false,
            IsSupported = false,
            IsEOL = false,
            ErrorCode = Framework.ErrorCode.MISSING_PARAMETER,
            ErrorMessage = "Missing required parameter: 'targetFramework'. Specify the target .NET framework moniker (e.g., 'net8.0', 'net48').",
            ErrorContext = new Dictionary<string, object>
            {
                ["parameterName"] = "targetFramework"
            }
        };
    }

    /// <summary>
    /// Creates an unknown framework error (valid format but unrecognized version).
    /// </summary>
    public static FrameworkValidationResult UnknownFrameworkError(string tfm, string? nearestMatch = null)
    {
        return new FrameworkValidationResult
        {
            IsValid = true,
            IsSupported = false,
            IsEOL = false,
            ErrorCode = Framework.ErrorCode.UNKNOWN_FRAMEWORK,
            ErrorMessage = $"Unrecognized framework: '{tfm}'. Supported frameworks: .NET 8-9, .NET Framework 4.6.2-4.8.1, .NET Standard 2.0-2.1.",
            SuggestedFramework = nearestMatch,
            ErrorContext = new Dictionary<string, object>
            {
                ["requested"] = tfm
            }
        };
    }
}
