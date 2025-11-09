using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorCsharpMCP.Core.Refactorings.InlineMethodComponents;

/// <summary>
/// Responsible for finding and analyzing method references (call sites) in syntax trees.
/// Provides utilities for locating all invocations of a given method symbol.
/// </summary>
internal sealed class ReferenceAnalyzer
{
    /// <summary>
    /// Finds all references to a method (call sites) within the syntax tree.
    /// </summary>
    /// <param name="root">The compilation unit to search.</param>
    /// <param name="symbol">The method symbol to find references for.</param>
    /// <param name="semanticModel">The semantic model for symbol comparison.</param>
    /// <returns>A list of invocation expressions that reference the specified method.</returns>
    public List<InvocationExpressionSyntax> FindMethodReferences(
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
}
