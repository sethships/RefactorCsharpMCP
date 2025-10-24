using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RefactorCsharpMCP.Core.Infrastructure.FrameworkSupport;

namespace RefactorCsharpMCP.Tests.Infrastructure;

/// <summary>
/// Factory for creating framework-aware Roslyn CSharpCompilation instances for testing.
/// Configures parse options, language version, preprocessor symbols, nullable context, and metadata references.
/// </summary>
public class CompilationFactory
{
    private readonly ReferenceAssemblyResolver _resolver;

    public CompilationFactory(ReferenceAssemblyResolver? resolver = null)
    {
        _resolver = resolver ?? new ReferenceAssemblyResolver();
    }

    /// <summary>
    /// Creates a fully-configured CSharpCompilation for a target framework.
    /// </summary>
    /// <param name="targetFramework">Target framework moniker (e.g., "net8.0", "net48")</param>
    /// <param name="sourceCode">C# source code to compile</param>
    /// <param name="assemblyName">Optional assembly name (default: "TestAssembly")</param>
    /// <returns>CSharpCompilation ready for semantic analysis</returns>
    public async Task<CSharpCompilation> CreateCompilationAsync(
        string targetFramework,
        string sourceCode,
        string? assemblyName = null)
    {
        // Normalize framework moniker
        targetFramework = FrameworkMoniker.Normalize(targetFramework);

        // Validate framework is supported
        if (!FrameworkMoniker.IsSupported(targetFramework))
        {
            throw new ArgumentException($"Unsupported framework: {targetFramework}", nameof(targetFramework));
        }

        // Create parse options with framework-specific language version
        var parseOptions = CreateParseOptions(targetFramework);

        // Parse source code
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, parseOptions);

        // Get framework-specific metadata references
        var references = await _resolver.GetReferenceAssembliesAsync(targetFramework);

        // Create compilation options
        var compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            nullableContextOptions: FrameworkMappings.GetNullableContextOptions(targetFramework),
            allowUnsafe: false,
            optimizationLevel: OptimizationLevel.Debug);

        // Create compilation
        var compilation = CSharpCompilation.Create(
            assemblyName ?? "TestAssembly",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: compilationOptions);

        return compilation;
    }

    /// <summary>
    /// Creates CSharpParseOptions configured for a target framework.
    /// </summary>
    public static CSharpParseOptions CreateParseOptions(string targetFramework)
    {
        var languageVersion = FrameworkMappings.GetLanguageVersion(targetFramework);
        var preprocessorSymbols = FrameworkMappings.GetPreprocessorSymbols(targetFramework);

        return new CSharpParseOptions(
            languageVersion: languageVersion,
            kind: SourceCodeKind.Regular,
            documentationMode: DocumentationMode.None,
            preprocessorSymbols: preprocessorSymbols);
    }

    /// <summary>
    /// Creates a compilation and validates that it has no errors.
    /// Throws an exception if the source code does not compile.
    /// </summary>
    public async Task<CSharpCompilation> CreateValidCompilationAsync(
        string targetFramework,
        string sourceCode,
        string? assemblyName = null)
    {
        var compilation = await CreateCompilationAsync(targetFramework, sourceCode, assemblyName);

        // Check for compilation errors
        var diagnostics = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (diagnostics.Any())
        {
            var errors = string.Join(Environment.NewLine,
                diagnostics.Select(d => $"{d.Id}: {d.GetMessage()}"));

            throw new InvalidOperationException(
                $"Source code has compilation errors for framework {targetFramework}:{Environment.NewLine}{errors}");
        }

        return compilation;
    }

    /// <summary>
    /// Creates a semantic model for source code in a target framework.
    /// </summary>
    public async Task<SemanticModel> CreateSemanticModelAsync(
        string targetFramework,
        string sourceCode)
    {
        var compilation = await CreateCompilationAsync(targetFramework, sourceCode);
        var syntaxTree = compilation.SyntaxTrees.First();
        return compilation.GetSemanticModel(syntaxTree);
    }

    /// <summary>
    /// Validates that source code compiles without errors for a target framework.
    /// Returns a tuple with success status and any compilation errors.
    /// </summary>
    public async Task<(bool success, IEnumerable<Diagnostic> errors)> ValidateCompilationAsync(
        string targetFramework,
        string sourceCode)
    {
        try
        {
            var compilation = await CreateCompilationAsync(targetFramework, sourceCode);
            var diagnostics = compilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            return (!diagnostics.Any(), diagnostics);
        }
        catch (ArgumentException ex)
        {
            // Create a synthetic diagnostic for framework validation errors
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor(
                    "TFM001",
                    "Invalid Target Framework",
                    ex.Message,
                    "Framework",
                    DiagnosticSeverity.Error,
                    true),
                Location.None);
            return (false, new[] { diagnostic });
        }
        catch (Exception ex)
        {
            // Create a synthetic diagnostic for unexpected exceptions
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor(
                    "TFM999",
                    "Unexpected Compilation Error",
                    $"Failed to create compilation: {ex.GetType().Name}: {ex.Message}",
                    "Internal",
                    DiagnosticSeverity.Error,
                    true),
                Location.None);
            return (false, new[] { diagnostic });
        }
    }

    /// <summary>
    /// Gets diagnostic messages for compilation errors.
    /// </summary>
    public static string FormatDiagnostics(IEnumerable<Diagnostic> diagnostics)
    {
        return string.Join(Environment.NewLine,
            diagnostics.Select(d => $"{d.Id} at {d.Location.GetLineSpan().StartLinePosition}: {d.GetMessage()}"));
    }
}
