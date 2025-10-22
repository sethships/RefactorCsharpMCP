using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using RefactorCsharpMCP.Core.Infrastructure.FrameworkSupport;

namespace RefactorCsharpMCP.Core.Diagnostics;

/// <summary>
/// Analyzes C# source code for diagnostics using Roslyn's built-in analyzers.
/// Provides framework-aware analysis that respects target framework capabilities.
/// </summary>
public class DiagnosticAnalyzer
{
    private readonly ReferenceAssemblyResolver _referenceResolver;
    private readonly ILogger? _logger;

    /// <summary>
    /// Creates a new DiagnosticAnalyzer instance.
    /// </summary>
    /// <param name="referenceResolver">Optional reference assembly resolver for framework-specific analysis.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public DiagnosticAnalyzer(ReferenceAssemblyResolver? referenceResolver = null, ILogger? logger = null)
    {
        _referenceResolver = referenceResolver ?? new ReferenceAssemblyResolver();
        _logger = logger;
    }

    /// <summary>
    /// Analyzes C# source code and returns diagnostics found by Roslyn.
    /// </summary>
    /// <param name="sourceCode">The C# source code to analyze.</param>
    /// <param name="targetFramework">The target framework moniker (e.g., "net8.0", "net48").</param>
    /// <param name="minSeverity">The minimum severity level to report (default: Warning).</param>
    /// <returns>A DiagnosticResult containing the list of diagnostics or an error message.</returns>
    public async Task<DiagnosticResult> AnalyzeCodeAsync(
        string sourceCode,
        string targetFramework,
        DiagnosticSeverity minSeverity = DiagnosticSeverity.Warning)
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(sourceCode))
            {
                return DiagnosticResult.CreateFailure("Source code cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(targetFramework))
            {
                return DiagnosticResult.CreateFailure("Target framework cannot be empty.");
            }

            // Normalize and validate framework
            var normalizedFramework = FrameworkMoniker.Normalize(targetFramework);
            if (!FrameworkMoniker.IsSupported(normalizedFramework))
            {
                return DiagnosticResult.CreateFailure($"Unsupported framework: {targetFramework}");
            }

            _logger?.LogDebug("Analyzing code for framework {Framework} with minimum severity {Severity}",
                normalizedFramework, minSeverity);

            // Create framework-aware compilation
            var compilation = await CreateCompilationAsync(sourceCode, normalizedFramework);

            // Get diagnostics from compilation
            var diagnostics = compilation.GetDiagnostics()
                .Where(d => d.Severity >= minSeverity)
                .OrderBy(d => d.Location.SourceSpan.Start)
                .ToList();

            _logger?.LogDebug("Found {Count} diagnostics with severity >= {Severity}",
                diagnostics.Count, minSeverity);

            // Convert Roslyn diagnostics to DiagnosticInfo
            var diagnosticInfos = diagnostics.Select(d => ConvertDiagnostic(d)).ToList();

            return DiagnosticResult.CreateSuccess(diagnosticInfos);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error analyzing code: {Message}", ex.Message);
            return DiagnosticResult.CreateFailure($"Analysis failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a framework-aware CSharpCompilation for diagnostic analysis.
    /// </summary>
    private async Task<CSharpCompilation> CreateCompilationAsync(string sourceCode, string targetFramework)
    {
        // Create parse options with framework-specific language version
        var languageVersion = FrameworkMoniker.GetLanguageVersion(targetFramework);
        var preprocessorSymbols = FrameworkMoniker.GetPreprocessorSymbols(targetFramework);

        var parseOptions = new CSharpParseOptions(
            languageVersion: languageVersion,
            kind: SourceCodeKind.Regular,
            documentationMode: DocumentationMode.None,
            preprocessorSymbols: preprocessorSymbols);

        // Parse source code
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, parseOptions);

        // Get framework-specific metadata references
        var references = await _referenceResolver.GetReferenceAssembliesAsync(targetFramework);

        // Create compilation options
        var compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            nullableContextOptions: FrameworkMoniker.GetNullableContextOptions(targetFramework),
            allowUnsafe: false,
            optimizationLevel: OptimizationLevel.Debug);

        // Create compilation
        var compilation = CSharpCompilation.Create(
            "DiagnosticAnalysis",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: compilationOptions);

        return compilation;
    }

    /// <summary>
    /// Converts a Roslyn Diagnostic to a DiagnosticInfo.
    /// </summary>
    private DiagnosticInfo ConvertDiagnostic(Diagnostic diagnostic)
    {
        var location = diagnostic.Location.GetLineSpan();
        var diagnosticLocation = new DiagnosticLocation(
            line: location.StartLinePosition.Line + 1, // Convert to 1-based
            column: location.StartLinePosition.Character + 1, // Convert to 1-based
            spanStart: diagnostic.Location.SourceSpan.Start,
            spanLength: diagnostic.Location.SourceSpan.Length);

        var category = MapDiagnosticCategory(diagnostic.Id);
        var applicableRefactorings = MapDiagnosticToRefactorings(diagnostic.Id);

        return new DiagnosticInfo(
            id: diagnostic.Id,
            severity: diagnostic.Severity.ToString(),
            message: diagnostic.GetMessage(),
            location: diagnosticLocation,
            category: category,
            applicableRefactorings: applicableRefactorings);
    }

    /// <summary>
    /// Maps a diagnostic ID to a category.
    /// </summary>
    private string MapDiagnosticCategory(string diagnosticId)
    {
        // IDE diagnostics
        if (diagnosticId.StartsWith("IDE"))
        {
            return diagnosticId switch
            {
                // Style diagnostics
                "IDE0001" or "IDE0002" or "IDE0003" or "IDE0004" or "IDE0005" => "Style",
                "IDE0007" or "IDE0008" or "IDE0009" or "IDE0010" or "IDE0011" => "Style",

                // Code quality diagnostics
                "IDE0044" or "IDE0051" or "IDE0052" or "IDE0058" or "IDE0059" => "Quality",

                // Naming diagnostics
                "IDE1006" => "Naming",

                // Performance diagnostics
                "IDE0022" => "Performance",

                _ => "CodeStyle"
            };
        }

        // Compiler diagnostics
        if (diagnosticId.StartsWith("CS"))
        {
            return diagnosticId switch
            {
                "CS8019" => "Style", // Unnecessary using directive
                _ => "Compiler"
            };
        }

        // Code analysis diagnostics
        if (diagnosticId.StartsWith("CA"))
        {
            return "CodeAnalysis";
        }

        return "Other";
    }

    /// <summary>
    /// Maps a diagnostic ID to applicable refactoring tools.
    /// </summary>
    private List<string> MapDiagnosticToRefactorings(string diagnosticId)
    {
        return diagnosticId switch
        {
            // Unused using directives
            "IDE0005" or "CS8019" => new List<string> { "remove_unused_usings" },

            // Can be made readonly
            "IDE0044" => new List<string> { "make_field_readonly" },

            // Expression value is never used / unused value
            "IDE0058" or "IDE0059" => new List<string> { "inline_variable" },

            // Use expression body
            "IDE0022" => new List<string> { "inline_method" },

            // No applicable refactorings
            _ => new List<string>()
        };
    }
}
