using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorCsharpMCP.Core.Refactorings.ExtractClassComponents.Strategies;

/// <summary>
/// Base class providing common functionality for extraction modifier strategies.
/// Implements shared logic for modifier manipulation used by concrete strategy implementations.
/// </summary>
public abstract class ExtractionModifierStrategyBase : IExtractionModifierStrategy
{
    /// <inheritdoc/>
    public abstract string StrategyName { get; }

    /// <inheritdoc/>
    public abstract SyntaxTokenList GetClassModifiers(ExtractionContext context);

    /// <inheritdoc/>
    public abstract MethodDeclarationSyntax TransformMethodModifiers(
        MethodDeclarationSyntax method,
        ExtractionContext context);

    /// <inheritdoc/>
    public abstract FieldDeclarationSyntax TransformFieldModifiers(
        FieldDeclarationSyntax field,
        ExtractionContext context);

    /// <inheritdoc/>
    public abstract bool CanHandle(ExtractionContext context);

    /// <summary>
    /// Removes all accessibility modifiers from a modifier list.
    /// </summary>
    /// <param name="modifiers">The original modifier list.</param>
    /// <returns>A new modifier list without accessibility modifiers (public, private, protected, internal).</returns>
    protected SyntaxTokenList RemoveAccessibilityModifiers(SyntaxTokenList modifiers)
    {
        return SyntaxFactory.TokenList(
            modifiers.Where(m => !IsAccessibilityModifier(m.Kind())));
    }

    /// <summary>
    /// Determines if a syntax kind represents an accessibility modifier.
    /// </summary>
    /// <param name="kind">The syntax kind to check.</param>
    /// <returns>True if the kind is public, private, protected, or internal; otherwise, false.</returns>
    protected bool IsAccessibilityModifier(SyntaxKind kind)
    {
        return kind == SyntaxKind.PublicKeyword ||
               kind == SyntaxKind.PrivateKeyword ||
               kind == SyntaxKind.ProtectedKeyword ||
               kind == SyntaxKind.InternalKeyword;
    }
}
