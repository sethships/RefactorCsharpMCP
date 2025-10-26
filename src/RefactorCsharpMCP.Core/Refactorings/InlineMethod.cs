using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using RefactorCsharpMCP.Core.Utilities;
using RefactorCsharpMCP.Core.Validation;

namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Provides functionality to inline a method by replacing all calls with the method's body.
/// Maps to Roslyn diagnostic IDE0022 (Use expression body for methods).
/// Part 1 implementation capabilities:
/// - Void methods (block-bodied or expression-bodied)
/// - Single caller validation
/// - Simple parameters (primitives and string)
/// - Comment preservation via trivia
/// - Framework-aware validation
/// - Semantic analysis to prevent variable shadowing
/// - Identifier conflict detection at call sites
/// </summary>
public class InlineMethod : RefactoringBase
{
    private readonly SymbolResolutionHelper _symbolHelper = new();

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
            var methodInfo = ExtractMethodInfo(symbolResult, semanticModel);
            if (methodInfo == null)
            {
                return RefactoringResult.Failure(
                    $"No method found at line {lineNumber}, column {columnNumber}. " +
                    "Ensure the cursor is on a method declaration.");
            }

            // Validate that the method can be inlined
            CurrentPhase = "Validation";
            var validation = CanMethodBeInlined(methodInfo, semanticModel, compilation);
            if (!validation.CanInline)
            {
                return RefactoringResult.Failure(validation.Reason ?? "Method cannot be inlined.");
            }

            // Find all references to the method (call sites)
            CurrentPhase = "Reference Analysis";
            var references = FindMethodReferences(root, methodInfo.Symbol, semanticModel);

            Logger?.LogDebug(
                "Found {Count} reference(s) to method '{Name}'",
                references.Count,
                methodInfo.Symbol.Name);

            // Part 1: Single caller only
            if (references.Count == 0)
            {
                return RefactoringResult.Failure($"Method '{methodInfo.Symbol.Name}' has no callers. Cannot inline unused method.");
            }

            if (references.Count > 1)
            {
                return RefactoringResult.Failure($"Method '{methodInfo.Symbol.Name}' has {references.Count} callers. Part 1 only supports single caller. Use Part 2 for multiple call sites.");
            }

            // Validate that inlining won't cause identifier conflicts at call sites
            CurrentPhase = "Identifier Conflict Validation";
            var conflictValidation = ValidateNoIdentifierConflicts(methodInfo, references, semanticModel, compilation);
            if (!conflictValidation.IsValid)
            {
                return RefactoringResult.Failure(conflictValidation.ErrorMessage ?? "Identifier conflicts detected.");
            }

            // Track both the declaration and all reference nodes for safe transformation
            var nodesToTrack = new List<SyntaxNode> { methodInfo.MethodDeclaration };
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
            var trackedDeclaration = newRoot.GetCurrentNode(methodInfo.MethodDeclaration);
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
    /// Information about a method to be inlined.
    /// </summary>
    private class MethodInfo
    {
        public required IMethodSymbol Symbol { get; init; }
        public required MethodDeclarationSyntax MethodDeclaration { get; init; }
        public required BlockSyntax? BlockBody { get; init; }
        public required ArrowExpressionClauseSyntax? ExpressionBody { get; init; }
        public required bool IsVoid { get; init; }
        public required IReadOnlyList<IParameterSymbol> Parameters { get; init; }
    }

    /// <summary>
    /// Extracts method information from a symbol resolution result.
    /// </summary>
    private MethodInfo? ExtractMethodInfo(
        SymbolResolutionHelper.SymbolResolutionResult symbolResult,
        SemanticModel semanticModel)
    {
        // Verify we have a method symbol
        if (symbolResult.Symbol is not IMethodSymbol methodSymbol)
        {
            Logger?.LogWarning("Symbol at position is not a method (found: {SymbolKind})",
                symbolResult.Symbol?.Kind.ToString() ?? "null");
            return null;
        }

        // Find the method declaration from the resolved node
        var methodDeclaration = symbolResult.Node?.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (methodDeclaration == null)
        {
            Logger?.LogWarning("Could not find MethodDeclarationSyntax for method symbol");
            return null;
        }

        // Extract method body (either block or expression)
        var blockBody = methodDeclaration.Body;
        var expressionBody = methodDeclaration.ExpressionBody;

        // Note: Don't return null here for methods with no body (abstract/partial)
        // Let CanMethodBeInlined() handle validation and provide proper error messages
        if (blockBody == null && expressionBody == null)
        {
            Logger?.LogDebug("Method has no body (abstract or partial method) - will validate in CanMethodBeInlined");
        }

        // Check if method is void
        var isVoid = methodSymbol.ReturnsVoid;

        return new MethodInfo
        {
            Symbol = methodSymbol,
            MethodDeclaration = methodDeclaration,
            BlockBody = blockBody,
            ExpressionBody = expressionBody,
            IsVoid = isVoid,
            Parameters = methodSymbol.Parameters.ToList()
        };
    }

    /// <summary>
    /// Validates that a method can be safely inlined.
    /// </summary>
    private (bool CanInline, string? Reason) CanMethodBeInlined(
        MethodInfo methodInfo,
        SemanticModel semanticModel,
        Compilation compilation)
    {
        var symbol = methodInfo.Symbol;

        // Check for method body
        if (methodInfo.BlockBody == null && methodInfo.ExpressionBody == null)
        {
            return (false, $"Method '{symbol.Name}' has no body (abstract or partial methods cannot be inlined).");
        }

        // Check if method is virtual, abstract, or override
        if (symbol.IsVirtual || symbol.IsAbstract || symbol.IsOverride)
        {
            return (false, $"Method '{symbol.Name}' is virtual/abstract/override. Virtual methods cannot be safely inlined.");
        }

        // Check for recursion
        if (IsRecursive(methodInfo, semanticModel))
        {
            return (false, $"Method '{symbol.Name}' is recursive. Recursive methods cannot be inlined in Part 1.");
        }

        // Part 1: Validate simple parameters only (no ref/out, no complex types)
        foreach (var parameter in methodInfo.Parameters)
        {
            if (parameter.RefKind != RefKind.None)
            {
                return (false, $"Method '{symbol.Name}' has ref/out parameter '{parameter.Name}'. Part 1 only supports simple parameters.");
            }

            // Part 1: Only primitives and string
            var typeName = parameter.Type?.ToString() ?? "unknown";
            if (!IsSimpleType(typeName))
            {
                return (false, $"Method '{symbol.Name}' has complex parameter type '{typeName}'. Part 1 only supports primitives and string.");
            }
        }

        // Part 1: Only void or simple return types
        if (!methodInfo.IsVoid)
        {
            return (false, $"Method '{symbol.Name}' has a return value. Part 1 only supports void methods.");
        }

        return (true, null);
    }

    /// <summary>
    /// Validates that inlining the method won't cause identifier conflicts at call sites.
    /// Checks if any local variables or fields in the method body have the same names as
    /// identifiers in scope at the call sites.
    /// </summary>
    private (bool IsValid, string? ErrorMessage) ValidateNoIdentifierConflicts(
        MethodInfo methodInfo,
        List<InvocationExpressionSyntax> callSites,
        SemanticModel semanticModel,
        Compilation compilation)
    {
        // Get the method body
        var methodBody = methodInfo.BlockBody ?? (SyntaxNode?)methodInfo.ExpressionBody;
        if (methodBody == null)
        {
            return (true, null); // No body means no conflicts
        }

        // Extract all identifiers from the method body that refer to local symbols or fields
        var methodBodyIdentifiers = methodBody.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Select(id => semanticModel.GetSymbolInfo(id).Symbol)
            .Where(s => s is ILocalSymbol || s is IFieldSymbol || s is IPropertySymbol)
            .Select(s => s!.Name)
            .Distinct()
            .ToHashSet();

        if (methodBodyIdentifiers.Count == 0)
        {
            return (true, null); // No local identifiers to conflict
        }

        // Check each call site for conflicts
        foreach (var callSite in callSites)
        {
            // Get the semantic model for the call site's syntax tree
            var callSiteTree = callSite.SyntaxTree;
            var callSiteModel = compilation.GetSemanticModel(callSiteTree);

            // Get all symbols in scope at the call site
            var scopeSymbols = callSiteModel.LookupSymbols(callSite.SpanStart)
                .Where(s => s is ILocalSymbol || s is IFieldSymbol || s is IPropertySymbol)
                .Select(s => s.Name)
                .ToHashSet();

            // Find any conflicts
            var conflicts = methodBodyIdentifiers.Intersect(scopeSymbols).ToList();

            if (conflicts.Any())
            {
                return (false,
                    $"Cannot inline method '{methodInfo.Symbol.Name}': Method body uses identifiers " +
                    $"that would conflict with call site scope: {string.Join(", ", conflicts)}. " +
                    "This could cause the inlined code to reference different variables than intended.");
            }
        }

        return (true, null);
    }

    /// <summary>
    /// Checks if a method is recursive.
    /// </summary>
    private bool IsRecursive(MethodInfo methodInfo, SemanticModel semanticModel)
    {
        var methodBody = methodInfo.BlockBody ?? (SyntaxNode?)methodInfo.ExpressionBody;
        if (methodBody == null) return false;

        // Find all invocations in the method body
        var invocations = methodBody.DescendantNodes()
            .OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invokedSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (SymbolEqualityComparer.Default.Equals(invokedSymbol, methodInfo.Symbol))
            {
                return true; // Recursive call found
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a type is a simple type (primitive or string).
    /// </summary>
    private bool IsSimpleType(string typeName)
    {
        return typeName switch
        {
            "int" or "System.Int32" => true,
            "long" or "System.Int64" => true,
            "short" or "System.Int16" => true,
            "byte" or "System.Byte" => true,
            "bool" or "System.Boolean" => true,
            "double" or "System.Double" => true,
            "float" or "System.Single" => true,
            "decimal" or "System.Decimal" => true,
            "char" or "System.Char" => true,
            "string" or "System.String" => true,
            _ => false
        };
    }

    /// <summary>
    /// Finds all references to a method (call sites) within the syntax tree.
    /// </summary>
    private List<InvocationExpressionSyntax> FindMethodReferences(
        CompilationUnitSyntax root,
        IMethodSymbol symbol,
        SemanticModel semanticModel)
    {
        var references = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation =>
            {
                var invokedSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                return SymbolEqualityComparer.Default.Equals(invokedSymbol, symbol);
            })
            .ToList();

        return references;
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
            expression = SubstituteParameters(expression, methodInfo, invocation, semanticModel);
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
                var substitutedStatement = SubstituteParametersInStatement(statement, methodInfo, invocation, semanticModel);
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
    /// Substitutes parameters with arguments in an expression using semantic analysis.
    /// Uses symbol information to avoid variable shadowing bugs.
    /// </summary>
    private ExpressionSyntax SubstituteParameters(
        ExpressionSyntax expression,
        MethodInfo methodInfo,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count != methodInfo.Parameters.Count)
        {
            throw new InvalidOperationException(
                $"Argument count mismatch: expected {methodInfo.Parameters.Count}, got {arguments.Count}. " +
                "This indicates a semantic analysis error and should not occur for valid C# code.");
        }

        // Create a mapping from parameter symbol to argument expression
        var parameterMap = new Dictionary<IParameterSymbol, ExpressionSyntax>(SymbolEqualityComparer.Default);
        for (int i = 0; i < methodInfo.Parameters.Count; i++)
        {
            parameterMap[methodInfo.Parameters[i]] = arguments[i].Expression;
        }

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
    private StatementSyntax SubstituteParametersInStatement(
        StatementSyntax statement,
        MethodInfo methodInfo,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count != methodInfo.Parameters.Count)
        {
            throw new InvalidOperationException(
                $"Argument count mismatch: expected {methodInfo.Parameters.Count}, got {arguments.Count}. " +
                "This indicates a semantic analysis error and should not occur for valid C# code.");
        }

        // Create a mapping from parameter symbol to argument expression
        var parameterMap = new Dictionary<IParameterSymbol, ExpressionSyntax>(SymbolEqualityComparer.Default);
        for (int i = 0; i < methodInfo.Parameters.Count; i++)
        {
            parameterMap[methodInfo.Parameters[i]] = arguments[i].Expression;
        }

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
