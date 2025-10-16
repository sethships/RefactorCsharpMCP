using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCsharpMCP.Core.Infrastructure.FrameworkSupport;

namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Provides functionality to remove unused using directives from C# source code.
/// Detects unused usings via Roslyn diagnostics (IDE0005, CS8019) and removes them
/// while preserving file structure and global using directives (C# 10+).
/// </summary>
public class RemoveUnusedUsings : RefactoringBase
{
    /// <summary>
    /// Removes unused using directives from source code with framework-aware validation.
    /// Preserves global using directives for C# 10+ frameworks (net8.0, net9.0).
    /// </summary>
    /// <param name="sourceCode">The C# source code to refactor.</param>
    /// <param name="targetFramework">The target .NET framework (e.g., "net8.0", "net48").</param>
    /// <returns>A result containing the refactored code or error information.</returns>
    public async Task<RefactoringResult> ExecuteAsync(string sourceCode, string targetFramework)
    {
        return await ExecuteWithValidationAsync(
            sourceCode,
            targetFramework,
            async () => await Task.Run(() => Execute(sourceCode, targetFramework)));
    }

    /// <summary>
    /// Removes unused using directives from source code.
    /// </summary>
    /// <param name="sourceCode">The C# source code to refactor.</param>
    /// <param name="targetFramework">The target .NET framework for language version detection.</param>
    /// <returns>A result containing the refactored code or error information.</returns>
    public RefactoringResult Execute(string sourceCode, string targetFramework)
    {
        // Validate inputs
        var sourceValidation = ValidateNonEmpty(sourceCode, "Source code");
        if (!sourceValidation.IsSuccess) return sourceValidation;

        var frameworkValidation = ValidateNonEmpty(targetFramework, "Target framework");
        if (!frameworkValidation.IsSuccess) return frameworkValidation;

        try
        {
            // Normalize framework moniker
            targetFramework = FrameworkMoniker.Normalize(targetFramework);

            // Validate framework is supported
            if (!FrameworkMoniker.IsSupported(targetFramework))
            {
                return RefactoringResult.Failure($"Unsupported framework: {targetFramework}. Use a Microsoft-supported framework version.");
            }

            // Get language version for target framework
            var languageVersion = FrameworkMoniker.GetLanguageVersion(targetFramework);
            var preprocessorSymbols = FrameworkMoniker.GetPreprocessorSymbols(targetFramework);

            // Parse source code with framework-specific language version
            var parseOptions = new CSharpParseOptions(
                languageVersion: languageVersion,
                kind: SourceCodeKind.Regular,
                documentationMode: DocumentationMode.Parse, // Preserve XML documentation
                preprocessorSymbols: preprocessorSymbols);

            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, parseOptions);
            var root = (CompilationUnitSyntax)syntaxTree.GetRoot();

            // Check for parse errors
            var parseDiagnostics = syntaxTree.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            if (parseDiagnostics.Any())
            {
                var errorMessages = string.Join(", ", parseDiagnostics.Select(d => d.GetMessage()).Take(3));
                return RefactoringResult.Failure($"Syntax errors in source code: {errorMessages}");
            }

            // If there are no using directives, return success with original code
            var allUsings = root.Usings;
            if (!allUsings.Any())
            {
                return RefactoringResult.Success(
                    sourceCode,
                    "No using directives found in source code.");
            }

            // Create compilation for semantic analysis
            var compilation = CreateCompilation(syntaxTree);

            // Get all diagnostics from compilation
            var diagnostics = compilation.GetDiagnostics();

            // Find unused using directive diagnostics (IDE0005 and CS8019)
            var unusedUsingDiagnostics = diagnostics
                .Where(d => d.Id == "IDE0005" || d.Id == "CS8019")
                .Where(d => d.Location.IsInSource)
                .ToList();

            // If no unused usings found, return success with original code
            if (!unusedUsingDiagnostics.Any())
            {
                return RefactoringResult.Success(
                    sourceCode,
                    $"All {allUsings.Count} using directive(s) are in use. No changes made.");
            }

            // Identify which using directives to remove
            var usingsToRemove = GetUsingsToRemove(root, unusedUsingDiagnostics, languageVersion);

            if (!usingsToRemove.Any())
            {
                return RefactoringResult.Success(
                    sourceCode,
                    "No unused using directives can be safely removed.");
            }

            // Remove unused using directives
            var newRoot = root.RemoveNodes(usingsToRemove, SyntaxRemoveOptions.KeepLeadingTrivia);
            if (newRoot == null)
            {
                return RefactoringResult.Failure("Failed to remove using directives. The syntax tree transformation returned null.");
            }

            // Normalize whitespace to ensure proper formatting
            newRoot = NormalizeWhitespace(newRoot);

            var removedCount = usingsToRemove.Count;
            var remainingCount = allUsings.Count - removedCount;

            return RefactoringResult.Success(
                newRoot.ToFullString(),
                $"Removed {removedCount} unused using directive(s). {remainingCount} using directive(s) remain.");
        }
        catch (Exception ex)
        {
            return HandleException(ex, "remove unused usings");
        }
    }

    /// <summary>
    /// Identifies which using directives should be removed based on diagnostics and language version.
    /// Preserves global using directives for C# 10+ language versions.
    /// </summary>
    /// <param name="root">The compilation unit root.</param>
    /// <param name="unusedDiagnostics">Diagnostics indicating unused using directives.</param>
    /// <param name="languageVersion">The target C# language version.</param>
    /// <returns>A list of using directive syntax nodes to remove.</returns>
    private List<UsingDirectiveSyntax> GetUsingsToRemove(
        CompilationUnitSyntax root,
        List<Diagnostic> unusedDiagnostics,
        LanguageVersion languageVersion)
    {
        var usingsToRemove = new List<UsingDirectiveSyntax>();

        // Check if language version supports global usings (C# 10+)
        var supportsGlobalUsings = languageVersion >= LanguageVersion.CSharp10;

        foreach (var diagnostic in unusedDiagnostics)
        {
            var location = diagnostic.Location;
            if (!location.IsInSource)
                continue;

            // Find the using directive at this location
            var node = root.FindNode(location.SourceSpan);
            var usingDirective = node.AncestorsAndSelf()
                .OfType<UsingDirectiveSyntax>()
                .FirstOrDefault();

            if (usingDirective == null)
                continue;

            // Check if this is a global using directive
            var isGlobalUsing = usingDirective.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword);

            // Preserve global using directives for C# 10+ frameworks
            if (isGlobalUsing && supportsGlobalUsings)
            {
                // Skip this using - it's a global using and language version supports it
                continue;
            }

            // This using directive should be removed
            usingsToRemove.Add(usingDirective);
        }

        return usingsToRemove;
    }
}
