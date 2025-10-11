using Microsoft.CodeAnalysis.CSharp;

namespace RefactorCsharpMCP.Core.Infrastructure.FrameworkSupport;

/// <summary>
/// Defines supported .NET framework monikers and their C# language versions.
/// Supports 11 framework monikers as specified in PRD v1.4.0.
/// </summary>
public static class FrameworkMoniker
{
    /// <summary>
    /// All supported framework monikers (11 total: 2 modern .NET + 7 .NET Framework + 2 .NET Standard).
    /// </summary>
    public static readonly HashSet<string> SupportedFrameworks = new(StringComparer.OrdinalIgnoreCase)
    {
        // Modern .NET (Currently Supported)
        "net9.0",
        "net8.0",

        // .NET Framework (Windows Component Lifecycle)
        "net481",
        "net48",
        "net472",
        "net471",
        "net47",
        "net462",
        "net35",

        // .NET Standard (Cross-Platform Compatibility)
        "netstandard2.1",
        "netstandard2.0"
    };

    /// <summary>
    /// End-of-life frameworks that are NOT supported.
    /// </summary>
    public static readonly HashSet<string> EolFrameworks = new(StringComparer.OrdinalIgnoreCase)
    {
        "net7.0", "net6.0", "net5.0",
        "netcoreapp3.1", "netcoreapp3.0", "netcoreapp2.2", "netcoreapp2.1", "netcoreapp2.0",
        "net461", "net46", "net452", "net451", "net45"
    };

    /// <summary>
    /// Maps framework monikers to C# language versions.
    /// </summary>
    private static readonly Dictionary<string, LanguageVersion> FrameworkToLanguageVersion = new(StringComparer.OrdinalIgnoreCase)
    {
        // Modern .NET
        ["net9.0"] = LanguageVersion.CSharp13,
        ["net8.0"] = LanguageVersion.CSharp12,

        // .NET Framework (all use C# 7.3)
        ["net481"] = LanguageVersion.CSharp7_3,
        ["net48"] = LanguageVersion.CSharp7_3,
        ["net472"] = LanguageVersion.CSharp7_3,
        ["net471"] = LanguageVersion.CSharp7_3,
        ["net47"] = LanguageVersion.CSharp7_3,
        ["net462"] = LanguageVersion.CSharp7_3,
        ["net35"] = LanguageVersion.CSharp3,

        // .NET Standard
        ["netstandard2.1"] = LanguageVersion.CSharp8,
        ["netstandard2.0"] = LanguageVersion.CSharp7_3
    };

    /// <summary>
    /// Maps framework monikers to NuGet package names for reference assemblies.
    /// </summary>
    private static readonly Dictionary<string, string> FrameworkToNuGetPackage = new(StringComparer.OrdinalIgnoreCase)
    {
        ["net481"] = "Microsoft.NETFramework.ReferenceAssemblies.net481",
        ["net48"] = "Microsoft.NETFramework.ReferenceAssemblies.net48",
        ["net472"] = "Microsoft.NETFramework.ReferenceAssemblies.net472",
        ["net471"] = "Microsoft.NETFramework.ReferenceAssemblies.net471",
        ["net47"] = "Microsoft.NETFramework.ReferenceAssemblies.net47",
        ["net462"] = "Microsoft.NETFramework.ReferenceAssemblies.net462",
        ["net35"] = "Microsoft.NETFramework.ReferenceAssemblies.net35"
    };

    /// <summary>
    /// Validates if a framework moniker is supported.
    /// </summary>
    public static bool IsSupported(string targetFramework)
    {
        return SupportedFrameworks.Contains(targetFramework);
    }

    /// <summary>
    /// Checks if a framework moniker is end-of-life (not supported).
    /// </summary>
    public static bool IsEndOfLife(string targetFramework)
    {
        return EolFrameworks.Contains(targetFramework);
    }

    /// <summary>
    /// Gets the C# language version for a framework moniker.
    /// </summary>
    public static LanguageVersion GetLanguageVersion(string targetFramework)
    {
        if (!IsSupported(targetFramework))
        {
            throw new ArgumentException($"Unsupported framework: {targetFramework}. Use IsSupported() to validate first.");
        }

        return FrameworkToLanguageVersion[targetFramework];
    }

    /// <summary>
    /// Gets the NuGet package name for a framework moniker (if applicable).
    /// Returns null for modern .NET and .NET Standard (use runtime assemblies).
    /// </summary>
    public static string? GetNuGetPackageName(string targetFramework)
    {
        return FrameworkToNuGetPackage.GetValueOrDefault(targetFramework);
    }

    /// <summary>
    /// Checks if a framework requires NuGet package download (.NET Framework only).
    /// </summary>
    public static bool RequiresNuGetPackage(string targetFramework)
    {
        return FrameworkToNuGetPackage.ContainsKey(targetFramework);
    }

    /// <summary>
    /// Gets a user-friendly framework name for error messages.
    /// </summary>
    public static string GetFriendlyName(string targetFramework)
    {
        return targetFramework.ToLowerInvariant() switch
        {
            "net9.0" => ".NET 9",
            "net8.0" => ".NET 8",
            "net481" => ".NET Framework 4.8.1",
            "net48" => ".NET Framework 4.8",
            "net472" => ".NET Framework 4.7.2",
            "net471" => ".NET Framework 4.7.1",
            "net47" => ".NET Framework 4.7",
            "net462" => ".NET Framework 4.6.2",
            "net35" => ".NET Framework 3.5 SP1",
            "netstandard2.1" => ".NET Standard 2.1",
            "netstandard2.0" => ".NET Standard 2.0",
            _ => targetFramework
        };
    }

    /// <summary>
    /// Suggests the nearest supported framework for an EOL framework.
    /// </summary>
    public static string? SuggestAlternative(string eolFramework)
    {
        return eolFramework.ToLowerInvariant() switch
        {
            "net7.0" or "net6.0" or "net5.0" => "net8.0",
            "netcoreapp3.1" or "netcoreapp3.0" => "net8.0",
            "netcoreapp2.2" or "netcoreapp2.1" or "netcoreapp2.0" => "net8.0",
            "net461" or "net46" => "net462",
            "net452" or "net451" or "net45" => "net462",
            _ => null
        };
    }

    /// <summary>
    /// Normalizes framework moniker format (handles variations like "net4.8" → "net48").
    /// </summary>
    public static string Normalize(string targetFramework)
    {
        var normalized = targetFramework.ToLowerInvariant().Trim();

        // Handle dotted .NET Framework versions (net4.8 → net48)
        normalized = normalized.Replace("net4.8.1", "net481")
                               .Replace("net4.8", "net48")
                               .Replace("net4.7.2", "net472")
                               .Replace("net4.7.1", "net471")
                               .Replace("net4.7", "net47")
                               .Replace("net4.6.2", "net462")
                               .Replace("net3.5", "net35");

        return normalized;
    }
}
