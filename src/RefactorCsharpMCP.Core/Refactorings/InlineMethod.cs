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

    public InlineMethod()
    {
        _methodResolver = new MethodResolver(Logger);
        _conflictResolver = new ConflictResolver(Logger);
        _parameterMapper = new ParameterMapper(Logger);
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
            var newRoot = InlineAllReferences(
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
            newRoot = RemoveMethodDeclaration(newRoot, (MethodDeclarationSyntax)trackedDeclaration);

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



    /// <summary>
    /// Replaces all method invocations with the method's body.
    /// </summary>
    private CompilationUnitSyntax InlineAllReferences(
        CompilationUnitSyntax root,
        List<InvocationExpressionSyntax> references,
        MethodInfo methodInfo,
        SemanticModel semanticModel)
    {
        // Create a dictionary for batch replacement
        var replacements = new Dictionary<SyntaxNode, SyntaxNode>();

        foreach (var invocation in references)
        {
            // Find the statement containing the invocation
            // For void methods, the invocation should be inside an ExpressionStatement
            var statementToReplace = invocation.FirstAncestorOrSelf<ExpressionStatementSyntax>();
            if (statementToReplace == null)
            {
                Logger?.LogWarning("Invocation is not inside an expression statement - skipping");
                continue;
            }

            // Extract the method body to inline
            SyntaxNode replacement;

            if (methodInfo.ExpressionBody != null)
            {
                // Expression-bodied method: => expression
                replacement = InlineExpressionBody(invocation, methodInfo, semanticModel);
            }
            else if (methodInfo.BlockBody != null)
            {
                // Block-bodied method: { statements }
                replacement = InlineBlockBody(invocation, methodInfo, semanticModel);
            }
            else
            {
                // Should never happen due to validation
                Logger?.LogError("Method has neither block nor expression body");
                continue;
            }

            // Preserve leading trivia (comments, whitespace) from both sources:
            // 1. Original statement trivia (e.g., "// Call the helper")
            // 2. Replacement statement trivia (e.g., "// Important comment")
            if (replacement is StatementSyntax replacementStatement)
            {
                var combinedTrivia = statementToReplace.GetLeadingTrivia()
                    .AddRange(replacementStatement.GetLeadingTrivia());
                replacement = replacementStatement.WithLeadingTrivia(combinedTrivia);
            }

            replacements[statementToReplace] = replacement;
        }

        // Perform batch replacement
        var newRoot = root.ReplaceNodes(replacements.Keys, (original, _) => replacements[original]);
        return newRoot;
    }

    /// <summary>
    /// Inlines an expression-bodied method.
    /// </summary>
    private SyntaxNode InlineExpressionBody(
        InvocationExpressionSyntax invocation,
        MethodInfo methodInfo,
        SemanticModel semanticModel)
    {
        // Get the expression from the arrow expression clause
        var expression = methodInfo.ExpressionBody!.Expression;

        // If method has parameters, substitute them with arguments
        if (methodInfo.Parameters.Any())
        {
            expression = _parameterMapper.SubstituteParameters(expression, methodInfo, invocation, semanticModel);
        }

        // For void methods, the invocation is a statement, so wrap in expression statement
        // For now (Part 1), all methods are void
        return SyntaxFactory.ExpressionStatement(expression);
    }

    /// <summary>
    /// Inlines a block-bodied method.
    /// </summary>
    private SyntaxNode InlineBlockBody(
        InvocationExpressionSyntax invocation,
        MethodInfo methodInfo,
        SemanticModel semanticModel)
    {
        var blockBody = methodInfo.BlockBody!;
        var statements = blockBody.Statements;

        // If method has parameters, substitute them in all statements
        if (methodInfo.Parameters.Any())
        {
            var substitutedStatements = new List<StatementSyntax>();
            foreach (var statement in statements)
            {
                var substitutedStatement = _parameterMapper.SubstituteParametersInStatement(statement, methodInfo, invocation, semanticModel);
                substitutedStatements.Add(substitutedStatement);
            }
            statements = SyntaxFactory.List(substitutedStatements);
        }

        // If only one statement, return it directly
        if (statements.Count == 1)
        {
            return statements[0];
        }

        // Multiple statements: wrap in a block statement
        // This creates a nested block which preserves scoping
        return SyntaxFactory.Block(statements);
    }

    /// <summary>
    /// Removes the method declaration from the syntax tree.
    /// </summary>
    private CompilationUnitSyntax RemoveMethodDeclaration(
        CompilationUnitSyntax root,
        MethodDeclarationSyntax methodDeclaration)
    {
        // Remove the method declaration, preserving leading trivia (comments above method)
        var newRoot = root.RemoveNode(methodDeclaration, SyntaxRemoveOptions.KeepLeadingTrivia);

        if (newRoot == null)
        {
            Logger?.LogWarning("Failed to remove method declaration, returning original root");
            return root;
        }

        return newRoot;
    }
}
