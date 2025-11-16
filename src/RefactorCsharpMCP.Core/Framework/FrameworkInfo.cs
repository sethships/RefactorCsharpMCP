using Microsoft.CodeAnalysis.CSharp;

namespace RefactorCsharpMCP.Core.Framework;

/// <summary>
/// Immutable value object containing complete framework metadata.
/// </summary>
/// <param name="Tfm">Target Framework Moniker (e.g., "net8.0", "net48")</param>
/// <param name="DisplayName">Human-readable name (e.g., ".NET 8", ".NET Framework 4.8")</param>
/// <param name="LanguageVersion">Roslyn LanguageVersion enumeration value</param>
/// <param name="Family">FrameworkFamily categorization</param>
/// <param name="SupportStatus">Current support status description</param>
/// <param name="ReleaseDate">Framework release date (optional)</param>
/// <param name="EndOfSupport">End of support date (optional, if known)</param>
public record FrameworkInfo(
    string Tfm,
    string DisplayName,
    LanguageVersion LanguageVersion,
    FrameworkFamily Family,
    string SupportStatus,
    DateTime? ReleaseDate = null,
    DateTime? EndOfSupport = null)
{
    /// <summary>
    /// Creates a new builder for constructing FrameworkInfo instances.
    /// </summary>
    public static FrameworkInfoBuilder Builder() => new();
}
