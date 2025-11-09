using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorCsharpMCP.Core.Utilities.Symbols;

/// <summary>
/// Locates all references to symbols within a compilation.
/// Provides simplified reference finding for single-project scenarios.
/// </summary>
/// <remarks>
/// This class was extracted from SymbolResolutionHelper.cs as part of Sprint 3 decomposition (Issue #90).
/// It enables independent optimization of reference finding algorithms.
///
/// <para>
/// <strong>Note</strong>: This is a simplified version that only searches within the provided compilation.
/// For full cross-project reference finding, use SymbolFinder with a Solution-based approach.
/// </para>
/// </remarks>
public class ReferenceLocator
{
    /// <summary>
    /// Finds all references to a symbol within a syntax tree.
    /// Note: This is a simplified version that only searches within the provided compilation.
    /// For full cross-project reference finding, use SymbolFinder with a Solution-based approach.
    /// </summary>
    /// <param name="symbol">The symbol to find references for.</param>
    /// <param name="compilation">The compilation to search.</param>
    /// <returns>A list of reference locations.</returns>
    public List<Location> GetAllReferences(ISymbol symbol, Compilation compilation)
    {
        if (symbol == null || compilation == null)
        {
            return new List<Location>();
        }

        try
        {
            var references = new List<Location>();

            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var root = syntaxTree.GetRoot();

                // Find all identifier nodes that reference this symbol
                var identifiers = root.DescendantNodes()
                    .OfType<IdentifierNameSyntax>();

                foreach (var identifier in identifiers)
                {
                    var symbolInfo = semanticModel.GetSymbolInfo(identifier);
                    if (SymbolEqualityComparer.Default.Equals(symbolInfo.Symbol, symbol))
                    {
                        references.Add(identifier.GetLocation());
                    }
                }
            }

            return references;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to find references for symbol '{symbol?.Name}'. " +
                $"This may indicate compilation errors or API changes.", ex);
        }
    }
}
