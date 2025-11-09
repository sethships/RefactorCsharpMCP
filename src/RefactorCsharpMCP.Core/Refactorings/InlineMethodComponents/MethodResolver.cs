using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using RefactorCsharpMCP.Core.Utilities;

namespace RefactorCsharpMCP.Core.Refactorings.InlineMethodComponents;

/// <summary>
/// Responsible for resolving method symbols and validating inline-ability.
/// Handles method extraction, validation, and recursion detection.
/// </summary>
internal sealed class MethodResolver
{
    private readonly ILogger? _logger;

    public MethodResolver(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Extracts method information from a symbol resolution result.
    /// </summary>
    /// <param name="symbolResult">The symbol resolution result from SymbolResolutionHelper.</param>
    /// <param name="semanticModel">The semantic model for symbol analysis.</param>
    /// <returns>Method information if successful, null if the symbol is not a valid method.</returns>
    public MethodInfo? ExtractMethodInfo(
        SymbolResolutionHelper.SymbolResolutionResult symbolResult,
        SemanticModel semanticModel)
    {
        // Verify we have a method symbol
        if (symbolResult.Symbol is not IMethodSymbol methodSymbol)
        {
            _logger?.LogWarning("Symbol at position is not a method (found: {SymbolKind})",
                symbolResult.Symbol?.Kind.ToString() ?? "null");
            return null;
        }

        // Find the method declaration from the resolved node
        var methodDeclaration = symbolResult.Node?.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (methodDeclaration == null)
        {
            _logger?.LogWarning("Could not find MethodDeclarationSyntax for method symbol");
            return null;
        }

        // Extract method body (either block or expression)
        var blockBody = methodDeclaration.Body;
        var expressionBody = methodDeclaration.ExpressionBody;

        // Note: Don't return null here for methods with no body (abstract/partial)
        // Let CanMethodBeInlined() handle validation and provide proper error messages
        if (blockBody == null && expressionBody == null)
        {
            _logger?.LogDebug("Method has no body (abstract or partial method) - will validate in CanMethodBeInlined");
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
    /// <param name="methodInfo">The method information to validate.</param>
    /// <param name="semanticModel">The semantic model for symbol analysis.</param>
    /// <param name="compilation">The compilation for semantic checks.</param>
    /// <returns>A tuple indicating whether the method can be inlined and the reason if not.</returns>
    public (bool CanInline, string? Reason) CanMethodBeInlined(
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
    /// Checks if a method is recursive.
    /// </summary>
    /// <param name="methodInfo">The method information to check.</param>
    /// <param name="semanticModel">The semantic model for symbol resolution.</param>
    /// <returns>True if the method is recursive, false otherwise.</returns>
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
    /// <param name="typeName">The type name to check.</param>
    /// <returns>True if the type is simple, false otherwise.</returns>
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
}
