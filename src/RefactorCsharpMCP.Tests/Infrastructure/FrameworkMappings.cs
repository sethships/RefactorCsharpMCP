using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace RefactorCsharpMCP.Tests.Infrastructure;

/// <summary>
/// Provides static mappings for framework-specific Roslyn compiler settings.
/// Maps framework monikers to language versions, preprocessor symbols, and nullable context options.
/// </summary>
public static class FrameworkMappings
{
    /// <summary>
    /// Maps framework monikers to C# language versions.
    /// Delegates to Core.Infrastructure.FrameworkSupport.FrameworkMoniker for consistency.
    /// </summary>
    public static LanguageVersion GetLanguageVersion(string targetFramework)
    {
        return Core.Infrastructure.FrameworkSupport.FrameworkMoniker.GetLanguageVersion(targetFramework);
    }

    /// <summary>
    /// Gets preprocessor symbols for a target framework.
    /// Used to define symbols like NET48, NETFRAMEWORK, NET35 in Roslyn compilation.
    /// </summary>
    public static IEnumerable<string> GetPreprocessorSymbols(string targetFramework)
    {
        var normalized = targetFramework.ToLowerInvariant();
        var symbols = new List<string>();

        // Framework-specific symbols
        switch (normalized)
        {
            case "net9.0":
                symbols.Add("NET9_0");
                symbols.Add("NET9_0_OR_GREATER");
                symbols.Add("NET8_0_OR_GREATER");
                symbols.Add("NET7_0_OR_GREATER");
                symbols.Add("NET6_0_OR_GREATER");
                symbols.Add("NET5_0_OR_GREATER");
                break;

            case "net8.0":
                symbols.Add("NET8_0");
                symbols.Add("NET8_0_OR_GREATER");
                symbols.Add("NET7_0_OR_GREATER");
                symbols.Add("NET6_0_OR_GREATER");
                symbols.Add("NET5_0_OR_GREATER");
                break;

            case "net481":
                symbols.Add("NET481");
                symbols.Add("NETFRAMEWORK");
                symbols.Add("NET48_OR_GREATER");
                symbols.Add("NET47_OR_GREATER");
                symbols.Add("NET46_OR_GREATER");
                break;

            case "net48":
                symbols.Add("NET48");
                symbols.Add("NETFRAMEWORK");
                symbols.Add("NET48_OR_GREATER");
                symbols.Add("NET47_OR_GREATER");
                symbols.Add("NET46_OR_GREATER");
                break;

            case "net472":
                symbols.Add("NET472");
                symbols.Add("NETFRAMEWORK");
                symbols.Add("NET47_OR_GREATER");
                symbols.Add("NET46_OR_GREATER");
                break;

            case "net471":
                symbols.Add("NET471");
                symbols.Add("NETFRAMEWORK");
                symbols.Add("NET47_OR_GREATER");
                symbols.Add("NET46_OR_GREATER");
                break;

            case "net47":
                symbols.Add("NET47");
                symbols.Add("NETFRAMEWORK");
                symbols.Add("NET47_OR_GREATER");
                symbols.Add("NET46_OR_GREATER");
                break;

            case "net462":
                symbols.Add("NET462");
                symbols.Add("NETFRAMEWORK");
                symbols.Add("NET46_OR_GREATER");
                break;

            case "net35":
                symbols.Add("NET35");
                symbols.Add("NETFRAMEWORK");
                break;

            case "netstandard2.1":
                symbols.Add("NETSTANDARD2_1");
                symbols.Add("NETSTANDARD2_1_OR_GREATER");
                symbols.Add("NETSTANDARD2_0_OR_GREATER");
                symbols.Add("NETSTANDARD");
                break;

            case "netstandard2.0":
                symbols.Add("NETSTANDARD2_0");
                symbols.Add("NETSTANDARD2_0_OR_GREATER");
                symbols.Add("NETSTANDARD");
                break;

            default:
                // Unknown framework - add minimal symbol for identification
                symbols.Add($"UNKNOWN_{normalized.ToUpperInvariant().Replace(".", "_")}");
                break;
        }

        return symbols;
    }

    /// <summary>
    /// Gets nullable context options for a target framework.
    /// C# 8.0+ frameworks enable nullable reference types, older frameworks disable them.
    /// </summary>
    public static NullableContextOptions GetNullableContextOptions(string targetFramework)
    {
        var langVersion = GetLanguageVersion(targetFramework);

        // C# 8.0 and later support nullable reference types
        return langVersion >= LanguageVersion.CSharp8
            ? NullableContextOptions.Enable
            : NullableContextOptions.Disable;
    }

    /// <summary>
    /// Checks if a framework supports nullable reference types (C# 8.0+).
    /// </summary>
    public static bool HasNullableTypes(string targetFramework)
    {
        return GetLanguageVersion(targetFramework) >= LanguageVersion.CSharp8;
    }

    /// <summary>
    /// Checks if a framework supports tuple types (C# 7.0+).
    /// </summary>
    public static bool HasTuples(string targetFramework)
    {
        return GetLanguageVersion(targetFramework) >= LanguageVersion.CSharp7;
    }

    /// <summary>
    /// Checks if a framework supports collection expressions (C# 12+).
    /// </summary>
    public static bool HasCollectionExpressions(string targetFramework)
    {
        return GetLanguageVersion(targetFramework) >= LanguageVersion.CSharp12;
    }

    /// <summary>
    /// Checks if a framework supports pattern matching (C# 7.0+).
    /// </summary>
    public static bool HasPatternMatching(string targetFramework)
    {
        return GetLanguageVersion(targetFramework) >= LanguageVersion.CSharp7;
    }

    /// <summary>
    /// Checks if a framework supports async streams (C# 8.0+).
    /// </summary>
    public static bool HasAsyncStreams(string targetFramework)
    {
        return GetLanguageVersion(targetFramework) >= LanguageVersion.CSharp8;
    }

    /// <summary>
    /// Checks if a framework supports records (C# 9.0+).
    /// </summary>
    public static bool HasRecords(string targetFramework)
    {
        return GetLanguageVersion(targetFramework) >= LanguageVersion.CSharp9;
    }

    /// <summary>
    /// Checks if a framework supports init-only setters (C# 9.0+).
    /// </summary>
    public static bool HasInitOnlySetters(string targetFramework)
    {
        return GetLanguageVersion(targetFramework) >= LanguageVersion.CSharp9;
    }

    /// <summary>
    /// Gets a human-readable description of C# features available for a framework.
    /// </summary>
    public static string GetFeatureDescription(string targetFramework)
    {
        var langVersion = GetLanguageVersion(targetFramework);
        var friendlyName = Core.Infrastructure.FrameworkSupport.FrameworkMoniker.GetFriendlyName(targetFramework);

        return $"{friendlyName} supports {langVersion}";
    }
}
