using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorCsharpMCP.Core.Refactorings.InlineMethodComponents;

/// <summary>
/// Information about a method to be inlined.
/// Immutable record that encapsulates method metadata and body information.
/// Provides value-based equality and with-expression support for non-destructive mutation.
/// </summary>
internal sealed record MethodInfo
{
    /// <summary>
    /// The Roslyn symbol representing the method.
    /// </summary>
    public required IMethodSymbol Symbol { get; init; }

    /// <summary>
    /// The syntax node for the method declaration.
    /// </summary>
    public required MethodDeclarationSyntax MethodDeclaration { get; init; }

    /// <summary>
    /// The block body if the method has a block-bodied implementation (e.g., { ... }).
    /// </summary>
    public required BlockSyntax? BlockBody { get; init; }

    /// <summary>
    /// The expression body if the method has an expression-bodied implementation (e.g., => expr).
    /// </summary>
    public required ArrowExpressionClauseSyntax? ExpressionBody { get; init; }

    /// <summary>
    /// True if the method has a void return type.
    /// </summary>
    public required bool IsVoid { get; init; }

    /// <summary>
    /// The method's parameters.
    /// </summary>
    public required IReadOnlyList<IParameterSymbol> Parameters { get; init; }
}
