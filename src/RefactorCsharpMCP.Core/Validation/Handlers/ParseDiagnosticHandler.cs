using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RefactorCsharpMCP.Core.Infrastructure.FrameworkSupport;

namespace RefactorCsharpMCP.Core.Validation.Handlers;

/// <summary>
/// Handles parse-time diagnostic errors (syntax errors at language level).
/// Implements Strategy Pattern for diagnostic handling.
/// </summary>
/// <remarks>
/// Processes Roslyn diagnostics from parsing phase and classifies them as:
/// - Language version mismatches (C# features not supported by target framework)
/// - Genuine syntax errors
/// </remarks>
public class ParseDiagnosticHandler : IParseDiagnosticHandler
{
    /// <summary>
    /// Handles parse diagnostics and returns appropriate validation result.
    /// </summary>
    /// <param name="diagnostics">Parse-time diagnostics from syntax tree.</param>
    /// <param name="targetFramework">Target framework moniker for error context.</param>
    /// <param name="syntaxTree">Syntax tree (not used for parse diagnostics, but required by interface).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>ValidationResult indicating success or describing the error.</returns>
    public Task<ValidationResult> HandleAsync(
        IEnumerable<Diagnostic> diagnostics,
        string targetFramework,
        SyntaxTree syntaxTree,
        CancellationToken cancellationToken = default)
    {
        var parseDiagnostics = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (!parseDiagnostics.Any())
        {
            return Task.FromResult(ValidationResult.Success());
        }

        // Syntax errors found - determine if it's a language version issue or genuine syntax error
        var errorMessages = parseDiagnostics.Select(d => d.GetMessage()).ToList();

        // Check if this is a language version error by looking for Roslyn's standard message
        var isLanguageVersionError = errorMessages.Any(msg =>
            msg.Contains("is not available in C#") ||
            msg.Contains("language version"));

        if (isLanguageVersionError)
        {
            // This is a language version mismatch
            var firstDiagnostic = parseDiagnostics.First();
            var detectedFeature = ExtractFeatureFromError(firstDiagnostic);
            var requiredVersion = DetectRequiredVersion(firstDiagnostic);

            // Get language version from target framework for display
            var languageVersion = FrameworkMoniker.GetLanguageVersion(targetFramework);
            var supportedVersion = FormatLanguageVersion(languageVersion);

            return Task.FromResult(ValidationResult.InputSyntaxMismatch(
                detectedFeature,
                requiredVersion,
                targetFramework,
                supportedVersion));
        }
        else
        {
            // Genuine syntax errors
            var errorDetails = string.Join(", ", errorMessages.Take(3));
            if (errorMessages.Count > 3)
            {
                errorDetails += $" (and {errorMessages.Count - 3} more)";
            }

            return Task.FromResult(ValidationResult.SyntaxError(errorDetails));
        }
    }

    /// <summary>
    /// Extracts the C# feature name from a compiler error message.
    /// </summary>
    private static string ExtractFeatureFromError(Diagnostic diagnostic)
    {
        var message = diagnostic.GetMessage();

        // Common patterns in Roslyn error messages
        if (message.Contains("collection expression")) return "collection expressions";
        if (message.Contains("nullable reference type")) return "nullable reference types";
        if (message.Contains("tuple")) return "tuple types";
        if (message.Contains("pattern matching")) return "pattern matching";
        if (message.Contains("init-only")) return "init-only properties";
        if (message.Contains("record")) return "record types";
        if (message.Contains("primary constructor")) return "primary constructors";
        if (message.Contains("file-scoped namespace")) return "file-scoped namespaces";
        if (message.Contains("global using")) return "global using directives";
        if (message.Contains("required member")) return "required members";

        // Default: use diagnostic ID
        return $"C# language feature ({diagnostic.Id})";
    }

    /// <summary>
    /// Detects the required C# version from a compiler error using diagnostic ID mapping.
    /// Uses comprehensive diagnostic ID table to avoid locale-dependent string matching.
    /// Switch expression ensures compile-time checking and prevents duplicate diagnostic IDs.
    /// </summary>
    private static string DetectRequiredVersion(Diagnostic diagnostic)
    {
        var id = diagnostic.Id;

        // Use switch expression for unambiguous, compile-time-checked mapping
        // Note: Some diagnostic IDs cover multiple features from different versions
        var version = id switch
        {
            // C# 13 features
            "CS9257" => "C# 13", // Params collections
            "CS9258" => "C# 13", // Params span

            // C# 12 features
            "CS8652" => "C# 12", // Collection expressions
            "CS9113" => "C# 12", // Primary constructors (class)
            "CS8866" => "C# 12", // Inline arrays
            "CS9175" => "C# 12", // Using alias for any type

            // C# 11 features
            "CS9058" => "C# 11", // Required members
            "CS8936" => "C# 11", // File-scoped types
            "CS8773" => "C# 11", // UTF-8 string literals / file-scoped namespaces (used by both C# 10 and 11)
            "CS8981" => "C# 11", // Generic attributes

            // C# 10 features
            "CS8805" => "C# 10", // Global using directives / record types (maps to highest version)
            "CS8869" => "C# 10", // Record structs
            "CS8910" => "C# 10", // Extended property patterns

            // C# 9 features
            "CS8870" => "C# 9",  // Init-only setters
            "CS8794" => "C# 9",  // Target-typed new

            // C# 8.0 features
            "CS8400" => "C# 8.0", // Using declarations / top-level statements (multiple features share this ID)
            "CS8321" => "C# 8.0", // Default interface members
            "CS8370" => "C# 8.0", // Async streams
            "CS8302" => "C# 8.0", // Nullable reference types / in parameters / default literal (multiple features)
            "CS8625" => "C# 8.0", // Nullable reference types (non-nullable to nullable conversion)
            "CS8632" => "C# 8.0", // Nullable reference types (possible null reference)

            // C# 7.3 features
            "CS8107" => "C# 7.3", // Ref structs / ref readonly / async main (multiple features, maps to latest)
            "CS8350" => "C# 7.3", // Unmanaged constraint

            // C# 7.0 features
            "CS8059" => "C# 7.0", // Tuples / local functions (multiple features share this ID)
            "CS8070" => "C# 7.0", // Pattern matching
            "CS8058" => "C# 7.0", // Out variables

            _ => null
        };

        if (version != null)
        {
            return version;
        }

        // Check diagnostic properties for RequiredLanguageVersion (Roslyn standard)
        if (diagnostic.Properties.TryGetValue("RequiredLanguageVersion", out var requiredVersion))
        {
            return $"C# {requiredVersion}";
        }

        // Fallback: use diagnostic ID ranges for broad categorization
        if (id.StartsWith("CS9")) return "C# 11+"; // CS9xxx typically C# 11+
        if (id.StartsWith("CS8") && id.CompareTo("CS8600") >= 0 && id.CompareTo("CS8699") <= 0) return "C# 8.0+";
        if (id.StartsWith("CS8") && id.CompareTo("CS8000") >= 0 && id.CompareTo("CS8599") <= 0) return "C# 7.0+";

        return "a newer C# version";
    }

    /// <summary>
    /// Formats a Roslyn LanguageVersion enum to a human-readable string.
    /// </summary>
    private static string FormatLanguageVersion(LanguageVersion version)
    {
        return version switch
        {
            LanguageVersion.CSharp13 => "C# 13",
            LanguageVersion.CSharp12 => "C# 12",
            LanguageVersion.CSharp11 => "C# 11",
            LanguageVersion.CSharp10 => "C# 10",
            LanguageVersion.CSharp9 => "C# 9",
            LanguageVersion.CSharp8 => "C# 8.0",
            LanguageVersion.CSharp7_3 => "C# 7.3",
            LanguageVersion.CSharp7_2 => "C# 7.2",
            LanguageVersion.CSharp7_1 => "C# 7.1",
            LanguageVersion.CSharp7 => "C# 7.0",
            LanguageVersion.CSharp6 => "C# 6.0",
            LanguageVersion.CSharp5 => "C# 5.0",
            LanguageVersion.CSharp4 => "C# 4.0",
            LanguageVersion.CSharp3 => "C# 3.0",
            _ => version.ToString()
        };
    }
}
