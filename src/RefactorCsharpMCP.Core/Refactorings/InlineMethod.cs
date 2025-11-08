using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using RefactorCsharpMCP.Core.Refactorings.InlineMethodComponents;
using RefactorCsharpMCP.Core.Utilities;
using RefactorCsharpMCP.Core.Validation;

namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Provides functionality to inline a method by replacing all calls with the method's body.
/// Maps to Roslyn diagnostic IDE0022 (Use expression body for methods).
/// Part 2 implementation capabilities:
/// - Void methods (block-bodied or expression-bodied)
/// - Multiple call site support (inlines at all call sites)
/// - Simple parameters (primitives and string)
/// - Comment preservation via trivia
/// - Framework-aware validation
/// - Semantic analysis to prevent variable shadowing
/// - Automatic identifier conflict detection and resolution
/// - Conflicting variables renamed with _1 suffix
/// </summary>
public class InlineMethod : RefactoringBase
{
    private readonly SymbolResolutionHelper _symbolHelper = new();
    private readonly MethodResolver _methodResolver;
    private readonly ReferenceAnalyzer _referenceAnalyzer = new();
    private readonly ConflictResolver _conflictResolver;
    private readonly ParameterMapper _parameterMapper;
    private readonly BodyTransformer _bodyTransformer;

    public InlineMethod()
    {
        _methodResolver = new MethodResolver(Logger);
        _conflictResolver = new ConflictResolver(Logger);
        _parameterMapper = new ParameterMapper(Logger);
        _bodyTransformer = new BodyTransformer(_parameterMapper, Logger);
    }

    /// <summary>
    /// Inlines a method by replacing all calls with the method's body, with framework-aware validation.
    /// </summary>
    /// <param name="sourceCode">The source code containing the method.</param>
    /// <param name="lineNumber">The line number (1-based) where the method is declared.</param>
    /// <param name="columnNumber">The column number (1-based) within the line.</param>
    /// <param name="targetFramework">The target .NET framework (e.g., "net8.0", "net48").</param>
    /// <returns>A result containing the refactored code or error information.</returns>
    public async Task<RefactoringResult> ExecuteAsync(
        string sourceCode,
        int lineNumber,
        int columnNumber,
        string targetFramework)
    {
        return await ExecuteWithValidationAsync(
            sourceCode,
            targetFramework,
            async () => await Task.Run(() => Execute(sourceCode, lineNumber, columnNumber)));
    }

    /// <summary>
    /// Inlines a method by replacing all calls with the method's body.
    /// </summary>
    /// <param name="sourceCode">The source code containing the method.</param>
    /// <param name="lineNumber">The line number (1-based) where the method is declared.</param>
    /// <param name="columnNumber">The column number (1-based) within the line.</param>
    /// <returns>A result containing the refactored code or error information.</returns>
    public RefactoringResult Execute(string sourceCode, int lineNumber, int columnNumber)
    {
        // Validate inputs
        var sourceValidation = ValidateNonEmpty(sourceCode, "Source code");
        if (!sourceValidation.IsSuccess) return sourceValidation;

        if (lineNumber < 1)
        {
            return RefactoringResult.Failure("Line number must be >= 1.");
        }

        if (columnNumber < 1)
        {
            return RefactoringResult.Failure("Column number must be >= 1.");
        }

        try
        {
            // Parse and validate syntax (CRITICAL: Parse ONCE and maintain SyntaxTree identity)
            CurrentPhase = "Syntax Parsing";
            var parseResult = ParseAndValidateSyntax(sourceCode, out var root, out var syntaxTree);
            if (!parseResult.IsSuccess || root == null || syntaxTree == null)
            {
                return parseResult;
            }

            // Create compilation for semantic analysis (leverages cache)
            CurrentPhase = "Semantic Analysis";
            var compilation = CreateCompilation(syntaxTree);
            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            // Find the method at the specified position using canonical pattern
            CurrentPhase = "Method Resolution";
            var symbolResult = _symbolHelper.GetSymbolAtPosition(semanticModel, syntaxTree, lineNumber, columnNumber);
            if (!symbolResult.Success)
            {
                return RefactoringResult.Failure(symbolResult.ErrorMessage ?? "Failed to resolve symbol at the specified position.");
            }

            // Extract method information from the resolved symbol
            var methodInfo = _methodResolver.ExtractMethodInfo(symbolResult, semanticModel);
            if (methodInfo == null)
            {
                return RefactoringResult.Failure(
                    $"No method found at line {lineNumber}, column {columnNumber}. " +
                    "Ensure the cursor is on a method declaration.");
            }

            // Validate that the method can be inlined
            CurrentPhase = "Validation";
            var validation = _methodResolver.CanMethodBeInlined(methodInfo, semanticModel, compilation);
            if (!validation.CanInline)
            {
                return RefactoringResult.Failure(validation.Reason ?? "Method cannot be inlined.");
            }

            // Find all references to the method (call sites)
            CurrentPhase = "Reference Analysis";
            var references = _referenceAnalyzer.FindMethodReferences(root, methodInfo.Symbol, semanticModel);

            Logger?.LogDebug(
                "Found {Count} reference(s) to method '{Name}'",
                references.Count,
                methodInfo.Symbol.Name);

            // Validate we have at least one caller
            if (references.Count == 0)
            {
                return RefactoringResult.Failure($"Method '{methodInfo.Symbol.Name}' has no callers. Cannot inline unused method.");
            }

            Logger?.LogDebug(
                "Method '{Name}' has {Count} call site(s) to inline",
                methodInfo.Symbol.Name,
                references.Count);

            // Store the original declaration for tracking (before any renaming)
            var originalDeclaration = methodInfo.MethodDeclaration;

            // Detect and resolve identifier conflicts at call sites
            CurrentPhase = "Identifier Conflict Detection";
            var conflicts = _conflictResolver.DetectIdentifierConflicts(methodInfo, references, semanticModel, compilation);

            if (conflicts.Count > 0)
            {
                Logger?.LogInformation(
                    "Detected {Count} identifier conflict(s), applying automatic resolution",
                    conflicts.Count);

                CurrentPhase = "Identifier Conflict Resolution";
                methodInfo = _conflictResolver.ResolveIdentifierConflicts(methodInfo, conflicts, references, semanticModel, compilation);
            }

            // Track both the ORIGINAL declaration and all reference nodes for safe transformation
            // Important: We track the original declaration (before renaming) since the renamed
            // declaration is a new node not yet in the tree
            var nodesToTrack = new List<SyntaxNode> { originalDeclaration };
            nodesToTrack.AddRange(references);
            var trackedRoot = root.TrackNodes(nodesToTrack);

            // Find the tracked references in the new tree
            var trackedReferences = references
                .Select(r => trackedRoot.GetCurrentNode(r))
                .Where(n => n != null)
                .Cast<InvocationExpressionSyntax>()
                .ToList();

            if (trackedReferences.Count != references.Count)
            {
                Logger?.LogWarning("Failed to track all reference nodes across transformation");
                return RefactoringResult.Failure("Failed to track all method references.");
            }

            // Inline all call sites with the method body
            CurrentPhase = "Inlining";
            var newRoot = _bodyTransformer.InlineAllReferences(
                trackedRoot,
                trackedReferences,
                methodInfo,
                semanticModel);

            // Find the method declaration in the transformed tree
            // Use originalDeclaration since that's what we tracked (before conflict resolution)
            var trackedDeclaration = newRoot.GetCurrentNode(originalDeclaration);
            if (trackedDeclaration == null)
            {
                Logger?.LogWarning("Failed to track method declaration across transformation");
                return RefactoringResult.Failure("Failed to remove method declaration after inlining.");
            }

            // Remove the method declaration
            CurrentPhase = "Method Removal";
            newRoot = _bodyTransformer.RemoveMethodDeclaration(newRoot, (MethodDeclarationSyntax)trackedDeclaration);

            // Normalize whitespace
            newRoot = NormalizeWhitespace(newRoot);

            var methodName = methodInfo.Symbol.Name;
            return RefactoringResult.Success(
                newRoot.ToFullString(),
                $"Inlined method '{methodName}' ({references.Count} call site(s) replaced)."
            );
        }
        catch (Exception ex)
        {
            return HandleException(ex, "inline method");
        }
    }

}
