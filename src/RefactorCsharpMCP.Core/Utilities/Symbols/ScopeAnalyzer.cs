using Microsoft.CodeAnalysis;

namespace RefactorCsharpMCP.Core.Utilities.Symbols;

/// <summary>
/// Analyzes symbol scope and accessibility characteristics.
/// Provides information about symbol kinds and accessibility modifiers.
/// </summary>
/// <remarks>
/// This class was extracted from SymbolResolutionHelper.cs as part of Sprint 3 decomposition (Issue #90).
/// It focuses on scope analysis, making it reusable for other refactorings.
/// </remarks>
public class ScopeAnalyzer
{
    /// <summary>
    /// Analyzes the scope and accessibility of a symbol.
    /// </summary>
    /// <param name="symbol">The symbol to analyze.</param>
    /// <returns>Information about the symbol's scope.</returns>
    public SymbolScopeInfo AnalyzeSymbolScope(ISymbol symbol)
    {
        if (symbol == null)
        {
            return new SymbolScopeInfo
            {
                ScopeName = "Unknown",
                IsLocal = false,
                IsParameter = false,
                IsField = false,
                IsMethod = false,
                IsPublic = false
            };
        }

        var scopeName = symbol.ContainingType?.Name ?? symbol.ContainingNamespace?.Name ?? "Global";

        return new SymbolScopeInfo
        {
            ScopeName = scopeName,
            IsLocal = symbol.Kind == SymbolKind.Local,
            IsParameter = symbol.Kind == SymbolKind.Parameter,
            IsField = symbol.Kind == SymbolKind.Field,
            IsMethod = symbol.Kind == SymbolKind.Method,
            IsProperty = symbol.Kind == SymbolKind.Property,
            IsPublic = symbol.DeclaredAccessibility == Accessibility.Public,
            IsPrivate = symbol.DeclaredAccessibility == Accessibility.Private,
            IsProtected = symbol.DeclaredAccessibility == Accessibility.Protected,
            IsInternal = symbol.DeclaredAccessibility == Accessibility.Internal
        };
    }
}

/// <summary>
/// Information about a symbol's scope and accessibility.
/// </summary>
public class SymbolScopeInfo
{
    public required string ScopeName { get; init; }
    public required bool IsLocal { get; init; }
    public required bool IsParameter { get; init; }
    public required bool IsField { get; init; }
    public required bool IsMethod { get; init; }
    public bool IsProperty { get; init; }
    public bool IsPublic { get; init; }
    public bool IsPrivate { get; init; }
    public bool IsProtected { get; init; }
    public bool IsInternal { get; init; }
}
