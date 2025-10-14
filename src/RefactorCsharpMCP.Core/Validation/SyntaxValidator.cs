using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RefactorCsharpMCP.Core.Infrastructure.FrameworkSupport;

namespace RefactorCsharpMCP.Core.Validation;

/// <summary>
/// Validates C# source code syntax compatibility with target .NET frameworks.
/// Performs both pre-refactoring (input) and post-refactoring (output) validation.
/// </summary>
public class SyntaxValidator
{
    private readonly ReferenceAssemblyResolver _referenceResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyntaxValidator"/> class.
    /// </summary>
    /// <param name="referenceResolver">Optional reference assembly resolver for testing.</param>
    public SyntaxValidator(ReferenceAssemblyResolver? referenceResolver = null)
    {
        _referenceResolver = referenceResolver ?? new ReferenceAssemblyResolver();
    }

    /// <summary>
    /// Validates that input source code is compatible with the target framework.
    /// Checks if the code uses C# features supported by the framework's language version.
    /// </summary>
    /// <param name="sourceCode">The C# source code to validate.</param>
    /// <param name="targetFramework">The target framework moniker (e.g., "net8.0", "net48").</param>
    /// <returns>A ValidationResult indicating success or describing the incompatibility.</returns>
    public async Task<ValidationResult> ValidateInputAsync(string sourceCode, string targetFramework)
    {
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
    public async Task<ValidationResult> ValidateOutputAsync(string sourceCode, string targetFramework)
    {
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
                // Check if these are API availability errors (type/member not found)
                var apiErrors = semanticDiagnostics.Where(d =>
                    d.Id == "CS0246" || // Type not found
                    d.Id == "CS0103" || // Name does not exist
                    d.Id == "CS0234" || // Type/namespace not found
                    d.Id == "CS1061").ToList(); // Member not found

                if (apiErrors.Any())
                {
                    // API compatibility issue - not a syntax issue
                    var apiErrorDetails = string.Join(", ", apiErrors.Select(d => d.GetMessage()).Take(3));
                    return ValidationResult.Failure(
                        ErrorCode.REFACTORING_FAILED,
                        $"API compatibility errors: {apiErrorDetails}",
                        "Ensure all types and members used are available in the target framework.");
                }

                // Other semantic errors
                var semanticErrorDetails = string.Join(", ", semanticDiagnostics.Select(d => d.GetMessage()).Take(3));
                return ValidationResult.SyntaxError(semanticErrorDetails);
            }

            // Validation succeeded
            return ValidationResult.Success();
        }
        catch (Exception ex)
        {
            return ValidationResult.Failure(
                ErrorCode.REFACTORING_FAILED,
                $"Validation failed: {ex.Message}",
                "Check source code and target framework are valid.");
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
    /// Detects the required C# version from a compiler error.
    /// </summary>
    private static string DetectRequiredVersion(Diagnostic diagnostic)
    {
        var id = diagnostic.Id;

        // Map diagnostic IDs to C# versions
        if (id == "CS8652") return "C# 12"; // Collection expressions
        if (id.StartsWith("CS8") && id.CompareTo("CS8600") >= 0 && id.CompareTo("CS8699") <= 0) return "C# 8.0"; // Nullable reference types
        if (id.StartsWith("CS8") && id.CompareTo("CS8370") >= 0 && id.CompareTo("CS8399") <= 0) return "C# 7.0"; // Tuples
        if (id.StartsWith("CS8") && id.CompareTo("CS8400") >= 0 && id.CompareTo("CS8499") <= 0) return "C# 8.0"; // C# 8 features

        // Check message content for version hints
        var message = diagnostic.GetMessage();
        if (message.Contains("12")) return "C# 12";
        if (message.Contains("11")) return "C# 11";
        if (message.Contains("10")) return "C# 10";
        if (message.Contains("9")) return "C# 9";
        if (message.Contains("8")) return "C# 8.0";
        if (message.Contains("7")) return "C# 7.0";

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
