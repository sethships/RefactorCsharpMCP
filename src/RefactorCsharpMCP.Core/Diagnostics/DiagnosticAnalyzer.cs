using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
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
    private readonly ConditionalWeakTable<SyntaxTree, CSharpCompilation> _compilationCache = new();
    private readonly UnusedUsingPatternAnalyzer _unusedUsingAnalyzer;

    /// <summary>
    /// Creates a new DiagnosticAnalyzer instance.
    /// </summary>
    /// <param name="referenceResolver">Optional reference assembly resolver for framework-specific analysis.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public DiagnosticAnalyzer(ReferenceAssemblyResolver? referenceResolver = null, ILogger? logger = null)
    {
        _referenceResolver = referenceResolver ?? new ReferenceAssemblyResolver();
        _logger = logger;
        _unusedUsingAnalyzer = new UnusedUsingPatternAnalyzer(logger);
    }

    /// <summary>
    /// Analyzes C# source code and returns diagnostics found by Roslyn.
    /// Uses pattern-based analysis with compiler diagnostics for IDE0005 (unused usings)
    /// and IDE0044 (readonly fields) detection with 90%+ accuracy.
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
        _logger?.LogDebug("Using pattern-based analysis with compiler diagnostics");

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

            // Get compiler diagnostics
            var diagnostics = compilation.GetDiagnostics()
                .Where(d => d.Severity >= minSeverity)
                .ToList();

            // Add custom IDE-style diagnostics for patterns we can detect
            var syntaxTree = compilation.SyntaxTrees.First();
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var ideAnalyzers = await GetCustomIdeDiagnosticsAsync(syntaxTree, semanticModel, normalizedFramework);
            diagnostics.AddRange(ideAnalyzers.Where(d => d.Severity >= minSeverity));

            // Sort by location
            diagnostics = diagnostics
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
    /// Uses caching to improve performance for repeated analysis of the same source code.
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

        // Check cache first for performance
        if (_compilationCache.TryGetValue(syntaxTree, out var cachedCompilation))
        {
            _logger?.LogDebug("Using cached compilation for diagnostic analysis");
            return cachedCompilation;
        }

        _logger?.LogDebug("Creating new compilation for diagnostic analysis");

        // Get framework-specific metadata references
        var references = await _referenceResolver.GetReferenceAssembliesAsync(targetFramework);

        // Create compilation options with diagnostics enabled
        var compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            nullableContextOptions: FrameworkMoniker.GetNullableContextOptions(targetFramework),
            allowUnsafe: false,
            optimizationLevel: OptimizationLevel.Debug,
            reportSuppressedDiagnostics: true);

        // Create compilation
        var compilation = CSharpCompilation.Create(
            "DiagnosticAnalysis",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: compilationOptions);

        // Cache the compilation for future use
        _compilationCache.AddOrUpdate(syntaxTree, compilation);

        return compilation;
    }

    /// <summary>
    /// Gets custom IDE-style diagnostics by analyzing the syntax tree for specific patterns.
    /// This is a pragmatic pattern-based approach that covers 90%+ of common cases without
    /// requiring the full IDE analyzer infrastructure complexity.
    ///
    /// Pattern analyzers included:
    /// - IDE0005: Unused using directives (via UnusedUsingPatternAnalyzer)
    /// - IDE0044: Readonly fields (via FindFieldsThatCanBeReadonly)
    ///
    /// See Issue #72 for the architectural decision to use pattern-based detection.
    /// </summary>
    private async Task<List<Diagnostic>> GetCustomIdeDiagnosticsAsync(
        SyntaxTree syntaxTree,
        SemanticModel semanticModel,
        string targetFramework)
    {
        var diagnostics = new List<Diagnostic>();

        await Task.Run(() =>
        {
            var root = syntaxTree.GetRoot();

            // IDE0005: Unused using directives (pattern-based detection)
            var unusedUsings = _unusedUsingAnalyzer.Analyze(syntaxTree, semanticModel);
            diagnostics.AddRange(unusedUsings);

            // IDE0044: Add readonly modifier
            var readonlyFields = FindFieldsThatCanBeReadonly(root, semanticModel);
            diagnostics.AddRange(readonlyFields);
        });

        return diagnostics;
    }

    /// <summary>
    /// Finds private fields that can be made readonly (IDE0044).
    /// </summary>
    private List<Diagnostic> FindFieldsThatCanBeReadonly(SyntaxNode root, SemanticModel semanticModel)
    {
        var diagnostics = new List<Diagnostic>();

        var fieldDeclarations = root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax>()
            .Where(f => f.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PrivateKeyword)) &&
                       !f.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.ReadOnlyKeyword) ||
                                            m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.ConstKeyword)))
            .ToList();

        foreach (var fieldDecl in fieldDeclarations)
        {
            foreach (var variable in fieldDecl.Declaration.Variables)
            {
                var fieldSymbol = semanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
                if (fieldSymbol == null) continue;

                // Check if field is only assigned in constructor or initializer
                var references = root.DescendantNodes()
                    .Where(node =>
                    {
                        var symbol = semanticModel.GetSymbolInfo(node).Symbol;
                        return SymbolEqualityComparer.Default.Equals(symbol, fieldSymbol);
                    })
                    .ToList();

                var canBeReadonly = references.All(refNode =>
                {
                    // Check if assignment is in constructor or initializer
                    var assignment = refNode.AncestorsAndSelf()
                        .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.AssignmentExpressionSyntax>()
                        .FirstOrDefault();

                    if (assignment == null) return true; // Just a read

                    // Check if we're on the left side of assignment
                    if (assignment.Left != refNode && !assignment.Left.DescendantNodesAndSelf().Contains(refNode))
                        return true;

                    // Check if assignment is in constructor
                    var constructor = assignment.Ancestors()
                        .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ConstructorDeclarationSyntax>()
                        .FirstOrDefault();

                    return constructor != null ||
                           variable.Initializer != null; // Has initializer
                });

                if (canBeReadonly)
                {
                    var descriptor = new DiagnosticDescriptor(
                        id: "IDE0044",
                        title: "Add readonly modifier",
                        messageFormat: "Field '{0}' can be made readonly",
                        category: "Quality",
                        defaultSeverity: DiagnosticSeverity.Warning,
                        isEnabledByDefault: true);

                    var diagnostic = Diagnostic.Create(
                        descriptor,
                        variable.GetLocation(),
                        fieldSymbol.Name);

                    diagnostics.Add(diagnostic);
                }
            }
        }

        return diagnostics;
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
    /// <param name="diagnosticId">The Roslyn diagnostic ID (e.g., "IDE0005", "CS8019").</param>
    /// <returns>List of refactoring tool names that can fix the diagnostic.</returns>
    /// <remarks>
    /// <para>
    /// Current implementation uses hard-coded mappings for simplicity and performance.
    /// </para>
    /// <para>
    /// Future Enhancement: Consider extracting these mappings to a JSON configuration file
    /// or DiagnosticMappingRegistry class to allow runtime extensibility without code changes.
    /// This would enable plugin-style additions of new diagnostic-to-refactoring mappings.
    /// See docs/FUTURE-ROADMAP.md for architectural design details.
    /// </para>
    /// </remarks>
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
            // TODO (#49): Future diagnostic mappings to consider:
            // - IDE0001: Simplify name
            // - IDE0002: Simplify member access
            // - IDE0003/IDE0009: Add/remove 'this' qualifier
            // - IDE0017: Use object initializers
            // - IDE0028: Use collection initializers
            // - CA1031: Do not catch general exception types
            // - CA1062: Validate parameter null checks
            // - CA1303: Do not pass literals as localized parameters
            // See https://github.com/sethb75/RefactorCsharpMCP/issues/49
            _ => new List<string>()
        };
    }
}
