using Microsoft.CodeAnalysis.CSharp;

namespace RefactorCsharpMCP.Core.Framework;

/// <summary>
/// Maps Target Framework Monikers to their corresponding C# language versions.
/// Provides fast O(1) lookups using the FrameworkRegistry.
/// </summary>
public class LanguageVersionMapper
{
    private readonly FrameworkValidator _validator;

    /// <summary>
    /// Initializes a new instance of LanguageVersionMapper.
    /// </summary>
    /// <param name="validator">Optional validator instance for TFM validation (creates new if null)</param>
    public LanguageVersionMapper(FrameworkValidator? validator = null)
    {
        _validator = validator ?? new FrameworkValidator();
    }

    /// <summary>
    /// Gets the C# language version for a given target framework.
    /// Returns null if the framework is not supported or invalid.
    /// </summary>
    /// <param name="targetFramework">The TFM to map (e.g., "net8.0", "net48")</param>
    /// <returns>LanguageVersion if framework is supported, null otherwise</returns>
    public LanguageVersion? GetLanguageVersion(string? targetFramework)
    {
        if (string.IsNullOrWhiteSpace(targetFramework))
            return null;

        // Normalize and validate the TFM
        var normalizedTfm = _validator.NormalizeTfm(targetFramework);

        // Fast O(1) lookup in the registry
        if (FrameworkRegistry.SupportedFrameworks.TryGetValue(normalizedTfm, out var frameworkInfo))
        {
            return frameworkInfo.LanguageVersion;
        }

        return null;
    }

    /// <summary>
    /// Gets the C# language version for a validated framework.
    /// Assumes the framework has already been validated and is supported.
    /// </summary>
    /// <param name="frameworkInfo">Validated framework information</param>
    /// <returns>The language version for the framework</returns>
    public LanguageVersion GetLanguageVersion(FrameworkInfo frameworkInfo)
    {
        if (frameworkInfo == null)
            throw new ArgumentNullException(nameof(frameworkInfo));

        return frameworkInfo.LanguageVersion;
    }

    /// <summary>
    /// Tries to get the language version for a target framework.
    /// Returns true and outputs the language version if successful.
    /// </summary>
    /// <param name="targetFramework">The TFM to map</param>
    /// <param name="languageVersion">The output language version if found</param>
    /// <returns>True if language version was found, false otherwise</returns>
    public bool TryGetLanguageVersion(string? targetFramework, out LanguageVersion languageVersion)
    {
        var version = GetLanguageVersion(targetFramework);
        if (version.HasValue)
        {
            languageVersion = version.Value;
            return true;
        }

        languageVersion = LanguageVersion.Default;
        return false;
    }

    /// <summary>
    /// Gets the language version with fallback to a default if not found.
    /// Useful for scenarios where a safe default is acceptable.
    /// </summary>
    /// <param name="targetFramework">The TFM to map</param>
    /// <param name="fallback">Fallback language version if TFM not found (defaults to C# 12)</param>
    /// <returns>Language version for the framework or the fallback</returns>
    public LanguageVersion GetLanguageVersionOrDefault(string? targetFramework, LanguageVersion fallback = LanguageVersion.CSharp12)
    {
        return GetLanguageVersion(targetFramework) ?? fallback;
    }
}
