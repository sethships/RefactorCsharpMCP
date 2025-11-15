using System.Text.RegularExpressions;

namespace RefactorCsharpMCP.Core.Framework;

/// <summary>
/// Validates Target Framework Monikers (TFMs) and provides framework metadata lookups.
/// Thread-safe with pre-compiled regex and fast O(1) dictionary lookups.
/// </summary>
public class FrameworkValidator
{
    // Pre-compiled regex for TFM format validation (net8.0, net48, netstandard2.0, etc.)
    private static readonly Regex TfmFormatRegex = new(
        @"^(net\d+\.\d+|net\d+|netstandard\d+\.\d+|netcoreapp\d+\.\d+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// Validates a target framework moniker and returns structured result.
    /// Performs normalization, format validation, EOL detection, and support verification.
    /// </summary>
    /// <param name="targetFramework">The TFM to validate (e.g., "net8.0", "net48")</param>
    /// <returns>FrameworkValidationResult with success/error details</returns>
    public FrameworkValidationResult Validate(string? targetFramework)
    {
        // Step 1: Check for missing parameter
        if (string.IsNullOrWhiteSpace(targetFramework))
        {
            return FrameworkValidationResult.MissingParameterError();
        }

        // Step 2: Normalize TFM (handle alternative formats)
        var normalizedTfm = NormalizeTfm(targetFramework);

        // Step 3: Validate TFM format using pre-compiled regex
        if (!TfmFormatRegex.IsMatch(normalizedTfm))
        {
            return FrameworkValidationResult.InvalidFormatError(targetFramework);
        }

        // Step 4: Check if framework is supported (fast O(1) lookup)
        if (FrameworkRegistry.SupportedFrameworks.TryGetValue(normalizedTfm, out var frameworkInfo))
        {
            return FrameworkValidationResult.Success(frameworkInfo);
        }

        // Step 5: Check if framework is EOL (fast O(1) lookup)
        if (FrameworkRegistry.EOLFrameworks.TryGetValue(normalizedTfm, out var eolInfo))
        {
            return FrameworkValidationResult.EOLError(
                normalizedTfm,
                eolInfo.SuggestedTfm,
                eolInfo.DisplayName,
                eolInfo.EOLDate);
        }

        // Step 6: Unknown framework (valid format but not in our registry)
        // Try to suggest a nearest match based on prefix
        var suggestedFramework = TryFindNearestMatch(normalizedTfm);
        return FrameworkValidationResult.UnknownFrameworkError(normalizedTfm, suggestedFramework);
    }

    /// <summary>
    /// Checks if a framework is currently supported by Microsoft.
    /// </summary>
    /// <param name="targetFramework">The TFM to check</param>
    /// <returns>True if supported, false otherwise</returns>
    public bool IsSupportedFramework(string? targetFramework)
    {
        if (string.IsNullOrWhiteSpace(targetFramework))
            return false;

        var normalizedTfm = NormalizeTfm(targetFramework);
        return FrameworkRegistry.SupportedFrameworks.ContainsKey(normalizedTfm);
    }

    /// <summary>
    /// Checks if a framework has reached end-of-life.
    /// </summary>
    /// <param name="targetFramework">The TFM to check</param>
    /// <returns>True if EOL, false otherwise</returns>
    public bool IsEOLFramework(string? targetFramework)
    {
        if (string.IsNullOrWhiteSpace(targetFramework))
            return false;

        var normalizedTfm = NormalizeTfm(targetFramework);
        return FrameworkRegistry.EOLFrameworks.ContainsKey(normalizedTfm);
    }

    /// <summary>
    /// Normalizes a TFM by applying known format conversions.
    /// Handles alternative formats like "v4.8", ".NETFramework,Version=v4.8", "framework48".
    /// </summary>
    /// <param name="targetFramework">The TFM to normalize</param>
    /// <returns>Normalized TFM in standard format</returns>
    public string NormalizeTfm(string targetFramework)
    {
        if (string.IsNullOrWhiteSpace(targetFramework))
            return targetFramework;

        var trimmed = targetFramework.Trim();

        // Check if normalization mapping exists (O(1) lookup)
        if (FrameworkRegistry.TfmNormalizations.TryGetValue(trimmed, out var normalized))
        {
            return normalized;
        }

        // Return as-is if no normalization needed
        return trimmed;
    }

    /// <summary>
    /// Attempts to find the nearest matching framework based on prefix similarity.
    /// Used for providing helpful suggestions when an unknown framework is requested.
    /// </summary>
    /// <param name="tfm">The unrecognized TFM</param>
    /// <returns>Suggested framework TFM or null if no match found</returns>
    private string? TryFindNearestMatch(string tfm)
    {
        // Extract prefix (e.g., "net10.0" → "net", "netstandard3.0" → "netstandard")
        var match = Regex.Match(tfm, @"^([a-z]+)");
        if (!match.Success)
            return null;

        var prefix = match.Groups[1].Value.ToLowerInvariant();

        // Find first supported framework with matching prefix
        return FrameworkRegistry.SupportedFrameworks.Keys
            .FirstOrDefault(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
