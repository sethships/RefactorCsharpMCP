using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RefactorCsharpMCP.Core.Infrastructure.FrameworkSupport;

namespace RefactorCsharpMCP.Core.Validation;

/// <summary>
/// Validates C# source code syntax compatibility with target .NET frameworks.
/// Performs both pre-refactoring (input) and post-refactoring (output) validation.
/// Implements IDisposable to properly clean up reference assembly resolver resources.
/// </summary>
public class SyntaxValidator : IDisposable
{
    private readonly ReferenceAssemblyResolver _referenceResolver;
    private readonly bool _ownsResolver;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyntaxValidator"/> class.
    /// </summary>
    /// <param name="referenceResolver">Optional reference assembly resolver for testing.</param>
    public SyntaxValidator(ReferenceAssemblyResolver? referenceResolver = null)
    {
        _referenceResolver = referenceResolver ?? new ReferenceAssemblyResolver();
        _ownsResolver = referenceResolver == null;
    }

    /// <summary>
    /// Validates that input source code is compatible with the target framework.
    /// Checks if the code uses C# features supported by the framework's language version.
    /// </summary>
    /// <param name="sourceCode">The C# source code to validate.</param>
    /// <param name="targetFramework">The target framework moniker (e.g., "net8.0", "net48").</param>
    /// <returns>A ValidationResult indicating success or describing the incompatibility.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the validator has been disposed.</exception>
    public async Task<ValidationResult> ValidateInputAsync(string sourceCode, string targetFramework)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return await ValidateCompilationAsync(
            sourceCode,
            targetFramework,
            ErrorCode.INPUT_SYNTAX_MISMATCH,
            "Input code");
    }

    /// <summary>
    /// Validates that refactored output code is compatible with the target framework.
    /// Checks if the generated code uses C# features supported by the framework's language version.
    /// </summary>
    /// <param name="sourceCode">The refactored C# source code to validate.</param>
    /// <param name="targetFramework">The target framework moniker (e.g., "net8.0", "net48").</param>
    /// <returns>A ValidationResult indicating success or describing the incompatibility.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the validator has been disposed.</exception>
    public async Task<ValidationResult> ValidateOutputAsync(string sourceCode, string targetFramework)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return await ValidateCompilationAsync(
            sourceCode,
            targetFramework,
            ErrorCode.FRAMEWORK_SYNTAX_MISMATCH,
            "Refactored output");
    }

    /// <summary>
    /// Core validation logic: attempts to compile source code with target framework's language version.
    /// </summary>
    private async Task<ValidationResult> ValidateCompilationAsync(
        string sourceCode,
        string targetFramework,
        ErrorCode mismatchErrorCode,
        string codeDescription)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return ValidationResult.Failure(
                ErrorCode.SYNTAX_ERROR,
                "Source code cannot be empty.",
                "Provide valid C# source code.");
        }

        try
        {
            // Normalize framework moniker
            targetFramework = FrameworkMoniker.Normalize(targetFramework);

            // Validate framework is supported
            if (!FrameworkMoniker.IsSupported(targetFramework))
            {
                return ValidationResult.Failure(
                    ErrorCode.UNKNOWN_FRAMEWORK,
                    $"Unsupported framework: {targetFramework}",
                    "Use a Microsoft-supported framework version.");
            }

            // Get language version for target framework
            var languageVersion = FrameworkMoniker.GetLanguageVersion(targetFramework);
            var preprocessorSymbols = FrameworkMoniker.GetPreprocessorSymbols(targetFramework);

            // Create parse options with framework-specific language version
            var parseOptions = new CSharpParseOptions(
                languageVersion: languageVersion,
                kind: SourceCodeKind.Regular,
                documentationMode: DocumentationMode.None,
                preprocessorSymbols: preprocessorSymbols);

            // Parse source code
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, parseOptions);

            // Check for parse errors (syntax errors at language level)
            var parseDiagnostics = syntaxTree.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            if (parseDiagnostics.Any())
            {
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
                    var supportedVersion = FormatLanguageVersion(languageVersion);

                    return mismatchErrorCode == ErrorCode.INPUT_SYNTAX_MISMATCH
                        ? ValidationResult.InputSyntaxMismatch(
                            detectedFeature,
                            requiredVersion,
                            targetFramework,
                            supportedVersion)
                        : ValidationResult.FrameworkSyntaxMismatch(
                            detectedFeature,
                            requiredVersion,
                            targetFramework,
                            supportedVersion);
                }
                else
                {
                    // Genuine syntax errors
                    var errorDetails = string.Join(", ", errorMessages.Take(3));
                    if (errorMessages.Count > 3)
                    {
                        errorDetails += $" (and {errorMessages.Count - 3} more)";
                    }

                    return ValidationResult.SyntaxError(errorDetails);
                }
            }

            // Get framework-specific metadata references for semantic validation
            var references = await _referenceResolver.GetReferenceAssembliesAsync(targetFramework);

            // Create compilation options
            var compilationOptions = new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: FrameworkMoniker.GetNullableContextOptions(targetFramework),
                allowUnsafe: false,
                optimizationLevel: OptimizationLevel.Debug);

            // Create compilation for semantic analysis
            var compilation = CSharpCompilation.Create(
                "ValidationAssembly",
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: compilationOptions);

            // Get semantic diagnostics
            var semanticDiagnostics = compilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            if (semanticDiagnostics.Any())
            {
                // Check if these are API availability errors (type/member not found in framework)
                var apiErrors = semanticDiagnostics.Where(d =>
                    d.Id == "CS0246" || // Type not found
                    d.Id == "CS0103" || // Name does not exist
                    d.Id == "CS0234" || // Type/namespace not found
                    d.Id == "CS1061").ToList(); // Member not found

                if (apiErrors.Any())
                {
                    // Distinguish between typos and framework API unavailability using heuristics
                    var (frameworkErrors, likelyTypos) = ClassifyApiErrors(apiErrors, syntaxTree);

                    if (frameworkErrors.Any())
                    {
                        var apiErrorDetails = string.Join(", ", frameworkErrors.Select(d => d.GetMessage()).Take(3));
                        if (frameworkErrors.Count > 3)
                        {
                            apiErrorDetails += $" (and {frameworkErrors.Count - 3} more)";
                        }

                        return ValidationResult.Failure(
                            ErrorCode.FRAMEWORK_API_UNAVAILABLE,
                            $"Code references types or members not available in {targetFramework}: {apiErrorDetails}",
                            "Either target a newer framework version or replace with APIs available in the target framework. " +
                            "Run validation against multiple frameworks to confirm compatibility.");
                    }

                    if (likelyTypos.Any())
                    {
                        var typoDetails = string.Join(", ", likelyTypos.Select(d => d.GetMessage()).Take(3));
                        if (likelyTypos.Count > 3)
                        {
                            typoDetails += $" (and {likelyTypos.Count - 3} more)";
                        }

                        return ValidationResult.SyntaxError(typoDetails);
                    }
                }

                // Other semantic errors (not API availability)
                var semanticErrorDetails = string.Join(", ", semanticDiagnostics.Select(d => d.GetMessage()).Take(3));
                if (semanticDiagnostics.Count > 3)
                {
                    semanticErrorDetails += $" (and {semanticDiagnostics.Count - 3} more)";
                }
                return ValidationResult.SyntaxError(semanticErrorDetails);
            }

            // Validation succeeded
            return ValidationResult.Success();
        }
        catch (ArgumentException ex)
        {
            return ValidationResult.Failure(
                ErrorCode.MISSING_PARAMETER,
                "Invalid parameter provided",
                ex.ParamName != null ? $"Check parameter: {ex.ParamName}" : "Check input parameters are valid.");
        }
        catch (NotSupportedException ex)
        {
            return ValidationResult.Failure(
                ErrorCode.UNKNOWN_FRAMEWORK,
                ex.Message,
                "Use a supported .NET framework version.");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("reference assembl", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Failure(
                ErrorCode.REFACTORING_FAILED,
                "Unable to resolve reference assemblies for target framework",
                "Ensure the target framework is properly installed or try clearing the assembly cache.");
        }
        catch (InvalidOperationException)
        {
            return ValidationResult.Failure(
                ErrorCode.REFACTORING_FAILED,
                "Validation encountered an unexpected state",
                "Check source code syntax and ensure target framework is valid.");
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

    /// <summary>
    /// Classifies API errors (CS0246, CS0103, CS0234, CS1061) as either framework API unavailability or likely typos.
    /// Uses three-stage heuristic approach to distinguish between legitimate framework compatibility issues
    /// and user typos in code.
    /// </summary>
    /// <remarks>
    /// <para><b>Classification Strategy:</b></para>
    /// <list type="number">
    /// <item>
    /// <term>BCL Namespace Detection</term>
    /// <description>Identifiers starting with System.*, Microsoft.*, Windows.*, etc. are classified as framework API issues.</description>
    /// </item>
    /// <item>
    /// <term>Typo Pattern Detection</term>
    /// <description>Identifiers with obvious typo indicators (triple chars, all lowercase, mixed case anomalies) are classified as likely typos.</description>
    /// </item>
    /// <item>
    /// <term>Conservative Default</term>
    /// <description>Ambiguous cases default to framework API issue (safer for framework-aware validation context).</description>
    /// </item>
    /// </list>
    /// <para><b>Design Rationale:</b></para>
    /// <para>
    /// This method operates in a framework-aware validation context where users have explicitly specified a target framework.
    /// The conservative default (ambiguous → framework API) is appropriate because:
    /// - User typos would typically be caught by IDE/compiler before reaching this tool
    /// - False negatives (typos classified as framework issues) are safer than false positives
    /// - Error messages include the specific identifier, allowing users to recognize typos
    /// - Users can test against multiple frameworks to confirm actual compatibility issues
    /// </para>
    /// </remarks>
    /// <param name="apiErrors">List of diagnostic errors with IDs CS0246, CS0103, CS0234, or CS1061 (type/member not found).</param>
    /// <param name="syntaxTree">The syntax tree being validated, used for locale-independent identifier extraction.</param>
    /// <returns>
    /// A tuple containing:
    /// - frameworkErrors: Diagnostics classified as framework API unavailability
    /// - likelyTypos: Diagnostics classified as probable user typos
    /// </returns>
    private static (List<Diagnostic> frameworkErrors, List<Diagnostic> likelyTypos) ClassifyApiErrors(
        List<Diagnostic> apiErrors,
        SyntaxTree syntaxTree)
    {
        var frameworkErrors = new List<Diagnostic>();
        var likelyTypos = new List<Diagnostic>();

        foreach (var error in apiErrors)
        {
            var message = error.GetMessage();
            var identifier = ExtractIdentifierFromError(error, syntaxTree);

            if (string.IsNullOrEmpty(identifier))
            {
                // Cannot extract identifier - default to framework error (safer assumption)
                frameworkErrors.Add(error);
                continue;
            }

            // Heuristic 1: Check for known BCL namespace prefixes
            if (IsKnownBclNamespace(identifier))
            {
                frameworkErrors.Add(error);
                continue;
            }

            // Heuristic 2: Check for improper naming conventions (likely typos)
            if (HasObviousTypo(identifier))
            {
                likelyTypos.Add(error);
                continue;
            }

            // Heuristic 3: Check for very short identifiers (often typos)
            if (identifier.Length <= 2)
            {
                likelyTypos.Add(error);
                continue;
            }

            // Default: Classify as framework error (conservative approach)
            // If uncertain, treat as framework issue since validation is framework-specific
            frameworkErrors.Add(error);
        }

        return (frameworkErrors, likelyTypos);
    }

    /// <summary>
    /// Extracts the identifier name from a diagnostic error using syntax tree location (locale-independent)
    /// with fallback to message parsing for edge cases.
    /// </summary>
    /// <param name="diagnostic">The diagnostic containing the error.</param>
    /// <param name="syntaxTree">The syntax tree to extract identifier from.</param>
    /// <returns>The extracted identifier, or empty string if extraction fails.</returns>
    private static string ExtractIdentifierFromError(Diagnostic diagnostic, SyntaxTree syntaxTree)
    {
        // Strategy 1 (PRIMARY): Use diagnostic location to extract identifier from syntax tree
        // This is locale-independent and version-resilient
        if (diagnostic.Location.IsInSource)
        {
            try
            {
                var node = syntaxTree.GetRoot().FindNode(diagnostic.Location.SourceSpan);
                if (node != null)
                {
                    var identifierText = node.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(identifierText))
                    {
                        return identifierText;
                    }
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                // Location span is invalid, fall through to message parsing
            }
        }

        // Strategy 2 (FALLBACK): Extract from error message (locale-dependent)
        // Support multiple quote characters for different locales
        var message = diagnostic.GetMessage();

        // Try various quote characters: single quote (English), double quote, Unicode quotes
        foreach (var quoteChar in new[] { '\'', '"', '\u2018', '\u2019', '\u201C', '\u201D' })
        {
            var startQuote = message.IndexOf(quoteChar);
            if (startQuote >= 0)
            {
                var endQuote = message.IndexOf(quoteChar, startQuote + 1);
                if (endQuote > startQuote)
                {
                    var extracted = message.Substring(startQuote + 1, endQuote - startQuote - 1);
                    if (!string.IsNullOrWhiteSpace(extracted))
                    {
                        return extracted;
                    }
                }
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Known BCL and Microsoft framework namespace prefixes.
    /// Static readonly for efficient reuse across calls.
    /// All entries have trailing dots for precise matching.
    /// </summary>
    private static readonly string[] KnownBclPrefixes = new[]
    {
        // Core BCL namespaces
        "System.",
        "Microsoft.",
        "Windows.",
        "Internal.",

        // Common System.* sub-namespaces
        "System.Collections.",
        "System.Collections.Concurrent.",
        "System.Collections.Generic.",
        "System.Collections.Immutable.",
        "System.Threading.",
        "System.Threading.Tasks.",
        "System.Linq.",
        "System.Text.",
        "System.Text.Json.",
        "System.Text.RegularExpressions.",
        "System.IO.",
        "System.Net.",
        "System.Net.Http.",
        "System.Diagnostics.",
        "System.Reflection.",
        "System.Runtime.",
        "System.Runtime.CompilerServices.",
        "System.Runtime.InteropServices.",
        "System.Security.",
        "System.Security.Cryptography.",
        "System.Data.",
        "System.Xml.",
        "System.Xml.Linq.",
        "System.ComponentModel.",
        "System.ComponentModel.DataAnnotations.",
        "System.Drawing.",
        "System.Web.",

        // Additional framework namespaces
        "NuGet.",
        "FSharp."
    };

    /// <summary>
    /// Checks if an identifier belongs to a known BCL namespace.
    /// </summary>
    /// <param name="identifier">The identifier to check.</param>
    /// <returns>True if the identifier starts with a known BCL namespace prefix.</returns>
    private static bool IsKnownBclNamespace(string identifier)
    {
        return KnownBclPrefixes.Any(prefix => identifier.StartsWith(prefix, StringComparison.Ordinal));
    }

    /// <summary>
    /// Checks if an identifier has obvious typo indicators using multiple heuristics.
    /// Allows common legitimate patterns like triple 's' in "ProcessSucceeded" or acronyms.
    /// </summary>
    /// <param name="identifier">The identifier to check for typo indicators.</param>
    /// <returns>True if the identifier likely contains a typo; false otherwise.</returns>
    private static bool HasObviousTypo(string identifier)
    {
        // Check for consecutive repeated characters (often typos, but allow common patterns)
        for (int i = 0; i < identifier.Length - 2; i++)
        {
            if (identifier[i] == identifier[i + 1] && identifier[i] == identifier[i + 2])
            {
                var repeatedChar = identifier[i];

                // Allow triple lowercase 's' (common in English: Process, Success, Address, etc.)
                // Allow triple uppercase letters (acronyms: XMLLLMProvider, HTTPSSL, etc.)
                if ((repeatedChar == 's' && char.IsLower(repeatedChar)) ||
                    char.IsUpper(repeatedChar))
                {
                    continue;
                }

                // Other triple characters are likely typos (e.g., "Striiing", "Boook")
                return true;
            }
        }

        // Check for all lowercase (uncommon for types in C#, but common for variables)
        // Since we're classifying types/namespaces, flag identifiers > 3 chars that are all lowercase
        if (identifier.Length > 3 && identifier.All(char.IsLower))
        {
            return true;
        }

        // Check for mixed case inconsistency (e.g., "sYstem" instead of "System")
        // Starts lowercase but has uppercase later - unusual for type names
        if (identifier.Length > 1 && char.IsLower(identifier[0]) && identifier.Skip(1).Any(char.IsUpper))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Releases resources used by the SyntaxValidator.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and optionally managed resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing && _ownsResolver)
        {
            _referenceResolver?.Dispose();
        }

        _disposed = true;
    }
}
