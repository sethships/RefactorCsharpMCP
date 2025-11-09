using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace RefactorCsharpMCP.Core.Refactorings.InlineMethodComponents;

/// <summary>
/// Responsible for transforming method bodies and performing the actual inlining operations.
/// Handles both expression-bodied and block-bodied methods with comment preservation.
/// </summary>
internal sealed class BodyTransformer
{
    private readonly ILogger? _logger;
    private readonly ParameterMapper _parameterMapper;

    public BodyTransformer(ParameterMapper parameterMapper, ILogger? logger = null)
    {
        _parameterMapper = parameterMapper;
        _logger = logger;
    }

    /// <summary>
    /// Replaces all method invocations with the method's body.
    /// </summary>
    public CompilationUnitSyntax InlineAllReferences(
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
                _logger?.LogWarning("Invocation is not inside an expression statement - skipping");
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
                _logger?.LogError("Method has neither block nor expression body");
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
    public CompilationUnitSyntax RemoveMethodDeclaration(
        CompilationUnitSyntax root,
        MethodDeclarationSyntax methodDeclaration)
    {
        // Remove the method declaration, preserving leading trivia (comments above method)
        var newRoot = root.RemoveNode(methodDeclaration, SyntaxRemoveOptions.KeepLeadingTrivia);

        if (newRoot == null)
        {
            _logger?.LogWarning("Failed to remove method declaration, returning original root");
            return root;
        }

        return newRoot;
    }
}
