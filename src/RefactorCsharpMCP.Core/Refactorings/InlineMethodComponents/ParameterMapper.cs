using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace RefactorCsharpMCP.Core.Refactorings.InlineMethodComponents;

/// <summary>
/// Responsible for parameter substitution when inlining methods.
/// Handles mapping of method parameters to call-site arguments with proper precedence preservation.
/// </summary>
internal sealed class ParameterMapper
{
    private readonly ILogger? _logger;

    public ParameterMapper(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Substitutes parameters with arguments in an expression using semantic analysis.
    /// Uses symbol information to avoid variable shadowing bugs.
    /// </summary>
    public ExpressionSyntax SubstituteParameters(
        ExpressionSyntax expression,
        MethodInfo methodInfo,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        // Create parameter-to-argument mapping with validation
        var parameterMap = CreateParameterMap(methodInfo, invocation);

        // Replace all parameter references with arguments using semantic analysis
        var newExpression = expression.ReplaceNodes(
            expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>(),
            (original, _) =>
            {
                // Use semantic model to determine what this identifier refers to
                var symbolInfo = semanticModel.GetSymbolInfo(original);

                // Check if this identifier refers to one of our method's parameters
                if (symbolInfo.Symbol is IParameterSymbol paramSymbol)
                {
                    // Verify it's one of the parameters we're substituting
                    if (parameterMap.TryGetValue(paramSymbol, out var argumentExpr))
                    {
                        // Wrap complex expressions in parentheses to preserve precedence
                        return WrapWithParenthesesIfNeeded(argumentExpr, original.Parent);
                    }
                }

                return original;
            });

        return newExpression;
    }

    /// <summary>
    /// Substitutes parameters with arguments in a statement using semantic analysis.
    /// Uses symbol information to avoid variable shadowing bugs.
    /// </summary>
    public StatementSyntax SubstituteParametersInStatement(
        StatementSyntax statement,
        MethodInfo methodInfo,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        // Create parameter-to-argument mapping with validation
        var parameterMap = CreateParameterMap(methodInfo, invocation);

        // Replace all parameter references with arguments using semantic analysis
        var newStatement = statement.ReplaceNodes(
            statement.DescendantNodes().OfType<IdentifierNameSyntax>(),
            (original, _) =>
            {
                // Use semantic model to determine what this identifier refers to
                var symbolInfo = semanticModel.GetSymbolInfo(original);

                // Check if this identifier refers to one of our method's parameters
                if (symbolInfo.Symbol is IParameterSymbol paramSymbol)
                {
                    // Verify it's one of the parameters we're substituting
                    if (parameterMap.TryGetValue(paramSymbol, out var argumentExpr))
                    {
                        // Wrap complex expressions in parentheses to preserve precedence
                        return WrapWithParenthesesIfNeeded(argumentExpr, original.Parent);
                    }
                }

                return original;
            });

        return newStatement;
    }

    /// <summary>
    /// Creates a mapping from method parameters to invocation arguments with validation.
    /// Validates that argument count matches parameter count and throws if mismatch detected.
    /// </summary>
    /// <param name="methodInfo">The method information containing parameters.</param>
    /// <param name="invocation">The invocation expression containing arguments.</param>
    /// <returns>A dictionary mapping parameter symbols to argument expressions.</returns>
    /// <exception cref="InvalidOperationException">Thrown when argument count doesn't match parameter count.</exception>
    private Dictionary<IParameterSymbol, ExpressionSyntax> CreateParameterMap(
        MethodInfo methodInfo,
        InvocationExpressionSyntax invocation)
    {
        var arguments = invocation.ArgumentList.Arguments;

        // Validate argument count matches parameter count
        if (arguments.Count != methodInfo.Parameters.Count)
        {
            // This should never happen for valid C# code that passed compilation
            // If it does, it indicates a serious semantic analysis bug - fail fast
            _logger?.LogError(
                "Argument count mismatch during parameter substitution: expected {Expected}, got {Actual}.",
                methodInfo.Parameters.Count,
                arguments.Count);

            throw new InvalidOperationException(
                $"Argument count mismatch during parameter substitution: " +
                $"expected {methodInfo.Parameters.Count} parameters, got {arguments.Count} arguments. " +
                "This indicates a compiler semantic analysis error.");
        }

        // Create a mapping from parameter symbol to argument expression
        var parameterMap = new Dictionary<IParameterSymbol, ExpressionSyntax>(SymbolEqualityComparer.Default);
        for (int i = 0; i < methodInfo.Parameters.Count; i++)
        {
            parameterMap[methodInfo.Parameters[i]] = arguments[i].Expression;
        }

        return parameterMap;
    }

    /// <summary>
    /// Wraps an expression with parentheses if needed to preserve operator precedence.
    /// </summary>
    private ExpressionSyntax WrapWithParenthesesIfNeeded(ExpressionSyntax expression, SyntaxNode? parent)
    {
        // If expression is already parenthesized, return as-is
        if (expression is ParenthesizedExpressionSyntax)
        {
            return expression;
        }

        // If expression is a simple literal, identifier, or invocation, no parentheses needed
        if (expression is LiteralExpressionSyntax ||
            expression is IdentifierNameSyntax ||
            expression is InvocationExpressionSyntax ||
            expression is ObjectCreationExpressionSyntax ||
            expression is MemberAccessExpressionSyntax)
        {
            return expression;
        }

        // If parent is a binary expression, check precedence
        if (parent is BinaryExpressionSyntax parentBinary)
        {
            // If the expression itself is a binary expression, check operator precedence
            if (expression is BinaryExpressionSyntax exprBinary)
            {
                var parentPrecedence = GetOperatorPrecedence(parentBinary.OperatorToken.Kind());
                var exprPrecedence = GetOperatorPrecedence(exprBinary.OperatorToken.Kind());

                // If expression has lower precedence, wrap in parentheses
                if (exprPrecedence < parentPrecedence)
                {
                    return SyntaxFactory.ParenthesizedExpression(expression);
                }
            }
            else
            {
                // Non-binary expressions in binary context generally need parentheses
                return SyntaxFactory.ParenthesizedExpression(expression);
            }
        }

        // If parent is a prefix/postfix unary expression, wrap complex expressions
        if (parent is PrefixUnaryExpressionSyntax || parent is PostfixUnaryExpressionSyntax)
        {
            if (expression is BinaryExpressionSyntax)
            {
                return SyntaxFactory.ParenthesizedExpression(expression);
            }
        }

        // Default: return as-is
        return expression;
    }

    /// <summary>
    /// Gets the precedence level for a binary operator.
    /// Higher number = higher precedence.
    /// </summary>
    private int GetOperatorPrecedence(SyntaxKind operatorKind)
    {
        return operatorKind switch
        {
            // Multiplicative: *, /, %
            SyntaxKind.AsteriskToken or SyntaxKind.SlashToken or SyntaxKind.PercentToken => 13,

            // Additive: +, -
            SyntaxKind.PlusToken or SyntaxKind.MinusToken => 12,

            // Shift: <<, >>
            SyntaxKind.LessThanLessThanToken or SyntaxKind.GreaterThanGreaterThanToken => 11,

            // Relational: <, >, <=, >=
            SyntaxKind.LessThanToken or SyntaxKind.GreaterThanToken or
            SyntaxKind.LessThanEqualsToken or SyntaxKind.GreaterThanEqualsToken => 10,

            // Equality: ==, !=
            SyntaxKind.EqualsEqualsToken or SyntaxKind.ExclamationEqualsToken => 9,

            // Bitwise AND: &
            SyntaxKind.AmpersandToken => 8,

            // Bitwise XOR: ^
            SyntaxKind.CaretToken => 7,

            // Bitwise OR: |
            SyntaxKind.BarToken => 6,

            // Logical AND: &&
            SyntaxKind.AmpersandAmpersandToken => 5,

            // Logical OR: ||
            SyntaxKind.BarBarToken => 4,

            // Null coalescing: ??
            SyntaxKind.QuestionQuestionToken => 3,

            // Assignment and compound assignment
            SyntaxKind.EqualsToken or
            SyntaxKind.PlusEqualsToken or SyntaxKind.MinusEqualsToken or
            SyntaxKind.AsteriskEqualsToken or SyntaxKind.SlashEqualsToken => 2,

            // Default low precedence
            _ => 1
        };
    }
}
