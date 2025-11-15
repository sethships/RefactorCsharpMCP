using Microsoft.CodeAnalysis.CSharp;

namespace RefactorCsharpMCP.Core.Framework;

/// <summary>
/// Central registry of .NET framework metadata, EOL frameworks, and TFM normalizations.
/// Provides static readonly dictionaries for fast O(1) lookups.
/// </summary>
public static class FrameworkRegistry
{
    /// <summary>
    /// Dictionary mapping Target Framework Monikers (TFMs) to complete framework metadata.
    /// Contains all 11 Microsoft-supported frameworks as of January 2025.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, FrameworkInfo> SupportedFrameworks =
        new Dictionary<string, FrameworkInfo>(StringComparer.OrdinalIgnoreCase)
        {
            // Modern .NET
            ["net9.0"] = FrameworkInfo.Builder()
                .WithTfm("net9.0")
                .WithDisplayName(".NET 9")
                .WithLanguageVersion(LanguageVersion.CSharp13)
                .WithFamily(FrameworkFamily.Modern)
                .WithSupportStatus("Supported until Nov 2026 (STS)")
                .WithReleaseDate(new DateTime(2024, 11, 12))
                .WithEndOfSupport(new DateTime(2026, 11, 10))
                .Build(),

            ["net8.0"] = FrameworkInfo.Builder()
                .WithTfm("net8.0")
                .WithDisplayName(".NET 8")
                .WithLanguageVersion(LanguageVersion.CSharp12)
                .WithFamily(FrameworkFamily.Modern)
                .WithSupportStatus("Supported until Nov 2026 (LTS)")
                .WithReleaseDate(new DateTime(2023, 11, 14))
                .WithEndOfSupport(new DateTime(2026, 11, 10))
                .Build(),

            // .NET Framework
            ["net481"] = FrameworkInfo.Builder()
                .WithTfm("net481")
                .WithDisplayName(".NET Framework 4.8.1")
                .WithLanguageVersion(LanguageVersion.CSharp7_3)
                .WithFamily(FrameworkFamily.Framework)
                .WithSupportStatus("Supported (tied to Windows lifecycle)")
                .WithReleaseDate(new DateTime(2022, 8, 9))
                .Build(),

            ["net48"] = FrameworkInfo.Builder()
                .WithTfm("net48")
                .WithDisplayName(".NET Framework 4.8")
                .WithLanguageVersion(LanguageVersion.CSharp7_3)
                .WithFamily(FrameworkFamily.Framework)
                .WithSupportStatus("Supported (tied to Windows lifecycle)")
                .WithReleaseDate(new DateTime(2019, 4, 18))
                .Build(),

            ["net472"] = FrameworkInfo.Builder()
                .WithTfm("net472")
                .WithDisplayName(".NET Framework 4.7.2")
                .WithLanguageVersion(LanguageVersion.CSharp7_3)
                .WithFamily(FrameworkFamily.Framework)
                .WithSupportStatus("Supported (tied to Windows lifecycle)")
                .WithReleaseDate(new DateTime(2018, 4, 30))
                .Build(),

            ["net471"] = FrameworkInfo.Builder()
                .WithTfm("net471")
                .WithDisplayName(".NET Framework 4.7.1")
                .WithLanguageVersion(LanguageVersion.CSharp7_3)
                .WithFamily(FrameworkFamily.Framework)
                .WithSupportStatus("Supported (tied to Windows lifecycle)")
                .WithReleaseDate(new DateTime(2017, 10, 17))
                .Build(),

            ["net47"] = FrameworkInfo.Builder()
                .WithTfm("net47")
                .WithDisplayName(".NET Framework 4.7")
                .WithLanguageVersion(LanguageVersion.CSharp7_3)
                .WithFamily(FrameworkFamily.Framework)
                .WithSupportStatus("Supported (tied to Windows lifecycle)")
                .WithReleaseDate(new DateTime(2017, 4, 5))
                .Build(),

            ["net462"] = FrameworkInfo.Builder()
                .WithTfm("net462")
                .WithDisplayName(".NET Framework 4.6.2")
                .WithLanguageVersion(LanguageVersion.CSharp7_3)
                .WithFamily(FrameworkFamily.Framework)
                .WithSupportStatus("Supported (tied to Windows lifecycle)")
                .WithReleaseDate(new DateTime(2016, 8, 2))
                .Build(),

            ["net35"] = FrameworkInfo.Builder()
                .WithTfm("net35")
                .WithDisplayName(".NET Framework 3.5 SP1")
                .WithLanguageVersion(LanguageVersion.CSharp3)
                .WithFamily(FrameworkFamily.Framework)
                .WithSupportStatus("Supported (tied to Windows lifecycle)")
                .WithReleaseDate(new DateTime(2008, 11, 18))
                .Build(),

            // .NET Standard
            ["netstandard2.1"] = FrameworkInfo.Builder()
                .WithTfm("netstandard2.1")
                .WithDisplayName(".NET Standard 2.1")
                .WithLanguageVersion(LanguageVersion.CSharp8)
                .WithFamily(FrameworkFamily.Standard)
                .WithSupportStatus("Supported via implementing .NET versions")
                .Build(),

            ["netstandard2.0"] = FrameworkInfo.Builder()
                .WithTfm("netstandard2.0")
                .WithDisplayName(".NET Standard 2.0")
                .WithLanguageVersion(LanguageVersion.CSharp7_3)
                .WithFamily(FrameworkFamily.Standard)
                .WithSupportStatus("Supported via implementing .NET versions")
                .Build()
        };

    /// <summary>
    /// Dictionary mapping end-of-life framework TFMs to their suggested replacement TFMs.
    /// Provides upgrade path for legacy projects.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, EOLFrameworkInfo> EOLFrameworks =
        new Dictionary<string, EOLFrameworkInfo>(StringComparer.OrdinalIgnoreCase)
        {
            // Modern .NET EOL → net8.0
            ["net7.0"] = new EOLFrameworkInfo(".NET 7", "net8.0", new DateTime(2024, 5, 14)),
            ["net6.0"] = new EOLFrameworkInfo(".NET 6", "net8.0", new DateTime(2024, 11, 12)),
            ["net5.0"] = new EOLFrameworkInfo(".NET 5", "net8.0", new DateTime(2022, 5, 10)),

            // .NET Core EOL → net8.0
            ["netcoreapp3.1"] = new EOLFrameworkInfo(".NET Core 3.1", "net8.0", new DateTime(2022, 12, 13)),
            ["netcoreapp3.0"] = new EOLFrameworkInfo(".NET Core 3.0", "net8.0", new DateTime(2020, 3, 3)),
            ["netcoreapp2.2"] = new EOLFrameworkInfo(".NET Core 2.2", "net8.0", new DateTime(2019, 12, 23)),
            ["netcoreapp2.1"] = new EOLFrameworkInfo(".NET Core 2.1", "net8.0", new DateTime(2021, 8, 21)),
            ["netcoreapp2.0"] = new EOLFrameworkInfo(".NET Core 2.0", "net8.0", new DateTime(2018, 10, 1)),

            // .NET Framework EOL → net462
            ["net461"] = new EOLFrameworkInfo(".NET Framework 4.6.1", "net462", new DateTime(2022, 4, 26)),
            ["net46"] = new EOLFrameworkInfo(".NET Framework 4.6", "net462", new DateTime(2022, 4, 26)),
            ["net452"] = new EOLFrameworkInfo(".NET Framework 4.5.2", "net462", new DateTime(2022, 4, 26)),
            ["net451"] = new EOLFrameworkInfo(".NET Framework 4.5.1", "net462", new DateTime(2022, 4, 26)),
            ["net45"] = new EOLFrameworkInfo(".NET Framework 4.5", "net462", new DateTime(2022, 4, 26))
        };

    /// <summary>
    /// Dictionary mapping alternative TFM formats to standard TFM strings.
    /// Enables flexible input handling while normalizing to canonical format.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> TfmNormalizations =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // .NET Framework version-style formats
            ["v4.8.1"] = "net481",
            ["v4.8"] = "net48",
            ["v4.7.2"] = "net472",
            ["v4.7.1"] = "net471",
            ["v4.7"] = "net47",
            ["v4.6.2"] = "net462",
            ["v3.5"] = "net35",

            // MSBuild-style framework names
            [".NETFramework,Version=v4.8.1"] = "net481",
            [".NETFramework,Version=v4.8"] = "net48",
            [".NETFramework,Version=v4.7.2"] = "net472",
            [".NETFramework,Version=v4.7.1"] = "net471",
            [".NETFramework,Version=v4.7"] = "net47",
            [".NETFramework,Version=v4.6.2"] = "net462",
            [".NETFramework,Version=v3.5"] = "net35",

            // Common variations
            ["framework48"] = "net48",
            ["framework481"] = "net481",
            ["framework462"] = "net462",
            ["dotnet8.0"] = "net8.0",
            ["dotnet9.0"] = "net9.0"
        };
}

/// <summary>
/// Information about an end-of-life framework including suggested replacement.
/// </summary>
/// <param name="DisplayName">Human-readable framework name</param>
/// <param name="SuggestedTfm">Recommended replacement TFM</param>
/// <param name="EOLDate">Date the framework reached end-of-life</param>
public record EOLFrameworkInfo(string DisplayName, string SuggestedTfm, DateTime EOLDate);
