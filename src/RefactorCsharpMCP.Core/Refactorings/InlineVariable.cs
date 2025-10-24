using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.Logging;
using RefactorCsharpMCP.Core.Utilities;
using RefactorCsharpMCP.Core.Validation;

namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Provides functionality to inline a variable by replacing all uses with its initialization expression.
/// Maps to Roslyn diagnostics IDE0059 (unnecessary value assignment) and IDE0058 (expression value never used).
/// </summary>
public class InlineVariable : RefactoringBase
{
    private readonly SymbolResolutionHelper _symbolHelper = new();

    /// <summary>
    /// Inlines a variable by replacing all its uses with its initialization expression, with framework-aware validation.
    /// </summary>
    /// <param name="sourceCode">The source code containing the variable.</param>
    /// <param name="lineNumber">The line number (1-based) where the variable is declared.</param>
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
    /// Inlines a variable by replacing all its uses with its initialization expression.
    /// </summary>
    /// <param name="sourceCode">The source code containing the variable.</param>
    /// <param name="lineNumber">The line number (1-based) where the variable is declared.</param>
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
            // Parse and validate syntax
            CurrentPhase = "Syntax Parsing";
            var parseResult = ParseAndValidateSyntax(sourceCode, out var root, out var syntaxTree);
            if (!parseResult.IsSuccess || root == null || syntaxTree == null)
            {
                return parseResult;
            }

            // Create compilation for semantic analysis
            CurrentPhase = "Semantic Analysis";
            var compilation = CreateCompilation(syntaxTree);
            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            // Find the variable at the specified position using canonical pattern
            CurrentPhase = "Variable Resolution";
            var symbolResult = _symbolHelper.GetSymbolAtPosition(semanticModel, syntaxTree, lineNumber, columnNumber);
            if (!symbolResult.Success)
            {
                return RefactoringResult.Failure(symbolResult.ErrorMessage ?? "Failed to resolve symbol at the specified position.");
            }

            // Extract variable information from the resolved symbol
            var variableInfo = ExtractVariableInfo(symbolResult, semanticModel);
            if (variableInfo == null)
            {
                return RefactoringResult.Failure(
                    $"No local variable found at line {lineNumber}, column {columnNumber}. " +
                    "Ensure the cursor is on a variable declaration.");
            }

            // Validate that the variable can be inlined
            CurrentPhase = "Validation";
            var validation = CanVariableBeInlined(variableInfo, semanticModel);
            if (!validation.CanInline)
            {
                return RefactoringResult.Failure(validation.Reason ?? "Variable cannot be inlined.");
            }

            // Find all references to the variable
            CurrentPhase = "Reference Analysis";
            var references = FindVariableReferences(root, variableInfo.Symbol, semanticModel);

            Logger?.LogDebug(
                "Found {Count} references to variable '{Name}'",
                references.Count,
                variableInfo.Symbol.Name);

            // Track both the declaration and all reference nodes so we can find them after transformation
            var nodesToTrack = new List<SyntaxNode> { variableInfo.DeclarationStatement };
            nodesToTrack.AddRange(references);
            var trackedRoot = root.TrackNodes(nodesToTrack);

            // Find the tracked references in the new tree
            var trackedReferences = references
                .Select(r => trackedRoot.GetCurrentNode(r))
                .Where(n => n != null)
                .Cast<IdentifierNameSyntax>()
                .ToList();

            if (trackedReferences.Count != references.Count)
            {
                Logger?.LogWarning("Failed to track all reference nodes across transformation");
                return RefactoringResult.Failure("Failed to track all variable references.");
            }

            // Inline all references with the initialization expression
            CurrentPhase = "Inlining";
            // Defensive check - should never happen due to validation above
            if (variableInfo.Initializer == null)
            {
                return RefactoringResult.Failure("Internal error: Variable initializer is null after validation.");
            }
            var newRoot = InlineAllReferences(trackedRoot, trackedReferences, variableInfo.Initializer);

            // Find the declaration statement in the transformed tree
            var trackedDeclaration = newRoot.GetCurrentNode(variableInfo.DeclarationStatement);
            if (trackedDeclaration == null)
            {
                Logger?.LogWarning("Failed to track declaration statement across transformation");
                return RefactoringResult.Failure("Failed to remove variable declaration after inlining.");
            }

            // Remove the variable declaration
            CurrentPhase = "Declaration Removal";
            newRoot = RemoveVariableDeclaration(newRoot, trackedDeclaration);

            // Normalize whitespace
            newRoot = NormalizeWhitespace(newRoot);

            var variableName = variableInfo.Symbol.Name;
            return RefactoringResult.Success(
                newRoot.ToFullString(),
                $"Inlined variable '{variableName}' ({references.Count} reference(s) replaced)."
            );
        }
        catch (Exception ex)
        {
            return HandleException(ex, "inline variable");
        }
    }

    /// <summary>
    /// Information about a variable to be inlined.
    /// </summary>
    private class VariableInfo
    {
        public required ILocalSymbol Symbol { get; init; }
        public required ExpressionSyntax? Initializer { get; init; }
        public required LocalDeclarationStatementSyntax DeclarationStatement { get; init; }
        public required VariableDeclaratorSyntax Declarator { get; init; }
    }

    /// <summary>
    /// Extracts variable information from a symbol resolution result.
    /// </summary>
    private VariableInfo? ExtractVariableInfo(
        SymbolResolutionHelper.SymbolResolutionResult symbolResult,
        SemanticModel semanticModel)
    {
        // Verify we have a local symbol
        if (symbolResult.Symbol is not ILocalSymbol localSymbol)
        {
            Logger?.LogWarning("Symbol at position is not a local variable (found: {SymbolKind})",
                symbolResult.Symbol?.Kind.ToString() ?? "null");
            return null;
        }

        // Find the variable declarator from the resolved node
        var declarator = symbolResult.Node?.FirstAncestorOrSelf<VariableDeclaratorSyntax>();
        if (declarator == null)
        {
            Logger?.LogWarning("Could not find VariableDeclaratorSyntax for local symbol");
            return null;
        }

        // Find the local declaration statement
        var declaration = declarator.FirstAncestorOrSelf<LocalDeclarationStatementSyntax>();
        if (declaration == null)
        {
            Logger?.LogWarning("Variable declarator is not part of a local declaration statement");
            return null;
        }

        // Return variable info even if no initializer - let validation handle it
        return new VariableInfo
        {
            Symbol = localSymbol,
            Initializer = declarator.Initializer?.Value,
            DeclarationStatement = declaration,
            Declarator = declarator
        };
    }

    /// <summary>
    /// Validates that a variable can be safely inlined.
    /// </summary>
    private (bool CanInline, string? Reason) CanVariableBeInlined(
        VariableInfo variableInfo,
        SemanticModel semanticModel)
    {
        var symbol = variableInfo.Symbol;

        // Check if variable has an initializer
        if (variableInfo.Initializer == null)
        {
            return (false, $"Variable '{symbol.Name}' has no initializer. Only variables with initialization expressions can be inlined.");
        }

        // Check if variable is assigned multiple times (only initial assignment allowed)
        var containingMethod = variableInfo.DeclarationStatement.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        var containingConstructor = variableInfo.DeclarationStatement.FirstAncestorOrSelf<ConstructorDeclarationSyntax>();
        var containingAccessor = variableInfo.DeclarationStatement.FirstAncestorOrSelf<AccessorDeclarationSyntax>();

        SyntaxNode? containingBlock = containingMethod
            ?? containingConstructor as SyntaxNode
            ?? containingAccessor as SyntaxNode;

        if (containingBlock == null)
        {
            return (false, $"Variable '{symbol.Name}' is not in a method, constructor, or accessor.");
        }

        // Find all assignments to this variable (excluding the initial declaration)
        var assignments = containingBlock.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Where(a => IsAssignmentToVariable(a, symbol.Name, semanticModel))
            .ToList();

        if (assignments.Any())
        {
            Logger?.LogDebug("Variable '{Name}' has {Count} assignment(s) after declaration", symbol.Name, assignments.Count);
            return (false, $"Variable '{symbol.Name}' is assigned after its declaration. Only variables assigned once at declaration can be inlined.");
        }

        // Check for unary modifications (++, --)
        var prefixUnaryMods = containingBlock.DescendantNodes()
            .OfType<PrefixUnaryExpressionSyntax>()
            .Where(u => (u.IsKind(SyntaxKind.PreIncrementExpression) || u.IsKind(SyntaxKind.PreDecrementExpression))
                && IsVariableReference(u.Operand, symbol.Name, semanticModel))
            .ToList();

        var postfixUnaryMods = containingBlock.DescendantNodes()
            .OfType<PostfixUnaryExpressionSyntax>()
            .Where(u => (u.IsKind(SyntaxKind.PostIncrementExpression) || u.IsKind(SyntaxKind.PostDecrementExpression))
                && IsVariableReference(u.Operand, symbol.Name, semanticModel))
            .ToList();

        if (prefixUnaryMods.Any() || postfixUnaryMods.Any())
        {
            return (false, $"Variable '{symbol.Name}' is modified with increment/decrement operators.");
        }

        // Check for lambda captures (V1 out of scope)
        var lambdas = containingBlock.DescendantNodes()
            .OfType<AnonymousFunctionExpressionSyntax>()
            .Where(lambda => LambdaCapturesVariable(lambda, symbol.Name))
            .ToList();

        if (lambdas.Any())
        {
            return (false, $"Variable '{symbol.Name}' is captured by lambda/anonymous function. Lambda captures are not supported in V1.");
        }

        return (true, null);
    }

    /// <summary>
    /// Checks if an assignment expression assigns to the specified variable.
    /// </summary>
    private bool IsAssignmentToVariable(AssignmentExpressionSyntax assignment, string variableName, SemanticModel semanticModel)
    {
        return IsVariableReference(assignment.Left, variableName, semanticModel);
    }

    /// <summary>
    /// Checks if an expression references the specified variable.
    /// </summary>
    private bool IsVariableReference(ExpressionSyntax expression, string variableName, SemanticModel semanticModel)
    {
        if (expression is IdentifierNameSyntax identifierName)
        {
            var symbol = semanticModel.GetSymbolInfo(identifierName).Symbol;
            return symbol != null && symbol.Name == variableName && symbol is ILocalSymbol;
        }

        return false;
    }

    /// <summary>
    /// Checks if a lambda captures the specified variable.
    /// </summary>
    /// <remarks>
    /// TODO(V2): Use data flow analysis for accurate capture detection.
    /// Current implementation uses simple text matching which may have false positives
    /// for variables with the same name in different scopes. Consider using
    /// DataFlowAnalysis.GetCapturedVariables() or semantic symbol comparison for V2.
    /// </remarks>
    private bool LambdaCapturesVariable(AnonymousFunctionExpressionSyntax lambda, string variableName)
    {
        // Simple text matching for V1 - may have false positives
        var identifiers = lambda.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(i => i.Identifier.Text == variableName)
            .ToList();

        return identifiers.Any();
    }

    /// <summary>
    /// Finds all references to a variable within the syntax tree.
    /// </summary>
    private List<IdentifierNameSyntax> FindVariableReferences(
        CompilationUnitSyntax root,
        ILocalSymbol symbol,
        SemanticModel semanticModel)
    {
        var references = root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(identifier =>
            {
                var idSymbol = semanticModel.GetSymbolInfo(identifier).Symbol;
                return SymbolEqualityComparer.Default.Equals(idSymbol, symbol);
            })
            .ToList();

        return references;
    }

    /// <summary>
    /// Replaces all variable references with the initialization expression.
    /// </summary>
    private CompilationUnitSyntax InlineAllReferences(
        CompilationUnitSyntax root,
        List<IdentifierNameSyntax> references,
        ExpressionSyntax initializerExpression)
    {
        // Create a dictionary for batch replacement
        var replacements = new Dictionary<SyntaxNode, SyntaxNode>();

        foreach (var reference in references)
        {
            // Determine if parentheses are needed based on parent context
            var replacement = WrapWithParenthesesIfNeeded(initializerExpression, reference.Parent);
            replacements[reference] = replacement;
        }

        // Perform batch replacement
        var newRoot = root.ReplaceNodes(replacements.Keys, (original, _) => replacements[original]);
        return newRoot;
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
                // unless they're already high-precedence (literals, identifiers, invocations)
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

    /// <summary>
    /// Removes the variable declaration statement.
    /// </summary>
    private CompilationUnitSyntax RemoveVariableDeclaration(
        CompilationUnitSyntax root,
        LocalDeclarationStatementSyntax declarationStatement)
    {
        // Remove the declaration statement
        var newRoot = root.RemoveNode(declarationStatement, SyntaxRemoveOptions.KeepNoTrivia);

        if (newRoot == null)
        {
            Logger?.LogWarning("Failed to remove declaration statement, returning original root");
            return root;
        }

        return newRoot;
    }
}
