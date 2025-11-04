using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using RefactorCsharpMCP.Core.Infrastructure.FrameworkSupport;

namespace RefactorCsharpMCP.Core.Diagnostics;

/// <summary>
/// Advanced diagnostic analyzer that uses Workspace APIs to provide full IDE analyzer support.
/// Detects IDE diagnostics (IDE0001-IDE9999) that require full compilation with analyzers.
///
/// Performance Note: This analyzer is ~2-5x slower than the basic DiagnosticAnalyzer but provides
/// complete diagnostic coverage including IDE0005 (unused usings), IDE0044 (readonly fields), etc.
///
/// Use Cases:
/// - When complete diagnostic detection is required
/// - When IDE analyzer rules need to be enforced
/// - When EditorConfig settings should be respected (future)
///
/// See Issue #72 for implementation details and roadmap.
/// </summary>
public class WorkspaceBasedDiagnosticAnalyzer
{
    private readonly ReferenceAssemblyResolver _referenceResolver;
    private readonly ILogger? _logger;
    private readonly DiagnosticAnalyzer _legacyAnalyzer;

    /// <summary>
    /// Creates a new WorkspaceBasedDiagnosticAnalyzer instance.
    /// </summary>
    /// <param name="referenceResolver">Optional reference assembly resolver for framework-specific analysis.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public WorkspaceBasedDiagnosticAnalyzer(ReferenceAssemblyResolver? referenceResolver = null, ILogger? logger = null)
    {
        _referenceResolver = referenceResolver ?? new ReferenceAssemblyResolver();
        _logger = logger;
        _legacyAnalyzer = new DiagnosticAnalyzer(_referenceResolver, _logger);
    }

    /// <summary>
    /// Analyzes C# source code using Workspace APIs and returns diagnostics including IDE analyzers.
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

            _logger?.LogDebug("Analyzing code with Workspace APIs for framework {Framework} with minimum severity {Severity}",
                normalizedFramework, minSeverity);

            // Create workspace and project
            var (workspace, document) = await CreateWorkspaceAsync(sourceCode, normalizedFramework);

            try
            {
                // Get compilation with IDE analyzers
                var compilation = await document.Project.GetCompilationAsync();
                if (compilation == null)
                {
                    return DiagnosticResult.CreateFailure("Failed to create compilation from workspace.");
                }

                // Get IDE analyzers
                var analyzers = AnalyzerDiscovery.GetCodeStyleAnalyzers(_logger);
                if (analyzers.IsEmpty)
                {
                    _logger?.LogWarning("No IDE analyzers discovered. Falling back to basic diagnostic analysis.");
                    return await _legacyAnalyzer.AnalyzeCodeAsync(sourceCode, normalizedFramework, minSeverity);
                }

                _logger?.LogDebug("Running analysis with {Count} IDE analyzers", analyzers.Length);

                // Create compilation with analyzers
                var analyzerOptions = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty);
                var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers, analyzerOptions);

                // Get all diagnostics (compiler + IDE analyzers)
                var allDiagnostics = await compilationWithAnalyzers.GetAllDiagnosticsAsync();

                // Filter by severity and sort
                var filteredDiagnostics = allDiagnostics
                    .Where(d => d.Severity >= minSeverity)
                    .OrderBy(d => d.Location.SourceSpan.Start)
                    .ToList();

                _logger?.LogDebug("Found {Count} diagnostics with severity >= {Severity}",
                    filteredDiagnostics.Count, minSeverity);

                // Convert Roslyn diagnostics to DiagnosticInfo
                var diagnosticInfos = filteredDiagnostics.Select(ConvertDiagnostic).ToList();

                return DiagnosticResult.CreateSuccess(diagnosticInfos);
            }
            finally
            {
                // Dispose workspace to free resources
                workspace.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error analyzing code with Workspace APIs: {Message}", ex.Message);
            return DiagnosticResult.CreateFailure($"Analysis failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates an AdhocWorkspace and document for analysis.
    /// </summary>
    private async Task<(Microsoft.CodeAnalysis.AdhocWorkspace workspace, Document document)> CreateWorkspaceAsync(
        string sourceCode,
        string targetFramework)
    {
        // Create workspace with MEF host services for full IDE support
        var hostServices = MefHostServices.Create(MefHostServices.DefaultAssemblies);
        var workspace = new Microsoft.CodeAnalysis.AdhocWorkspace(hostServices);

        // Get framework-specific settings
        var languageVersion = FrameworkMoniker.GetLanguageVersion(targetFramework);
        var preprocessorSymbols = FrameworkMoniker.GetPreprocessorSymbols(targetFramework);
        var references = await _referenceResolver.GetReferenceAssembliesAsync(targetFramework);

        // Create parse options
        var parseOptions = new CSharpParseOptions(
            languageVersion: languageVersion,
            kind: SourceCodeKind.Regular,
            documentationMode: DocumentationMode.None,
            preprocessorSymbols: preprocessorSymbols);

        // Create compilation options with all diagnostics enabled
        // Enable IDE diagnostics (like IDE0005) explicitly by setting them to warning level
        var specificDiagnosticOptions = new Dictionary<string, ReportDiagnostic>
        {
            ["IDE0005"] = ReportDiagnostic.Warn,  // Remove unnecessary usings
            ["CS8019"] = ReportDiagnostic.Warn     // Unused using directive
        };

        var compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            nullableContextOptions: FrameworkMoniker.GetNullableContextOptions(targetFramework),
            allowUnsafe: false,
            optimizationLevel: OptimizationLevel.Debug,
            reportSuppressedDiagnostics: true,
            specificDiagnosticOptions: specificDiagnosticOptions.ToImmutableDictionary());

        // Create project info
        var projectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            name: "DiagnosticAnalysis",
            assemblyName: "DiagnosticAnalysis",
            language: LanguageNames.CSharp)
            .WithMetadataReferences(references)
            .WithCompilationOptions(compilationOptions)
            .WithParseOptions(parseOptions);

        // Add project and document to workspace
        var project = workspace.AddProject(projectInfo);
        var document = workspace.AddDocument(project.Id, "Program.cs", SourceText.From(sourceCode));

        return (workspace, document);
    }

    /// <summary>
    /// Converts a Roslyn Diagnostic to a DiagnosticInfo.
    /// Reuses the same conversion logic as the legacy analyzer for consistency.
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
    /// Reuses the same mapping logic as the legacy analyzer for consistency.
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
    /// Reuses the same mapping logic as the legacy analyzer for consistency.
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
