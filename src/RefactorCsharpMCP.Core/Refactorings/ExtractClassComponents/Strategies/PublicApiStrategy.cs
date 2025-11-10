using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorCsharpMCP.Core.Refactorings.ExtractClassComponents.Strategies;

/// <summary>
/// Strategy for extracting public API classes.
/// Creates public classes with visibility based on original member accessibility,
/// suitable for library APIs and public-facing components.
/// </summary>
/// <remarks>
/// This strategy generates:
/// <list type="bullet">
/// <item><description>Public class declaration</description></item>
/// <item><description>Public methods (if originally public/protected), internal otherwise</description></item>
/// <item><description>Private fields (encapsulation for public APIs)</description></item>
/// </list>
/// Use when extracting functionality that needs to be consumed externally.
/// </remarks>
public class PublicApiStrategy : ExtractionModifierStrategyBase
{
    /// <inheritdoc/>
    public override string StrategyName => "PublicApi";

    /// <inheritdoc/>
    /// <remarks>
    /// Always returns public modifier for API visibility.
    /// </remarks>
    public override SyntaxTokenList GetClassModifiers(ExtractionContext context)
    {
        return SyntaxFactory.TokenList(
            SyntaxFactory.Token(SyntaxKind.PublicKeyword));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Determines method visibility based on original accessibility:
    /// <list type="bullet">
    /// <item><description>Public/Protected → Public (API surface)</description></item>
    /// <item><description>Private/Internal → Internal (implementation details)</description></item>
    /// </list>
    /// Preserves other modifiers (static, async, virtual, etc.).
    /// </remarks>
    public override MethodDeclarationSyntax TransformMethodModifiers(
        MethodDeclarationSyntax method,
        ExtractionContext context)
    {
        var modifiersWithoutAccessibility = RemoveAccessibilityModifiers(method.Modifiers);

        // Determine if method should be public based on original visibility
        var wasPublicOrProtected = method.Modifiers.Any(m =>
            m.IsKind(SyntaxKind.PublicKeyword) ||
            m.IsKind(SyntaxKind.ProtectedKeyword));

        var accessModifier = wasPublicOrProtected
            ? SyntaxFactory.Token(SyntaxKind.PublicKeyword)
            : SyntaxFactory.Token(SyntaxKind.InternalKeyword);

        var newModifiers = SyntaxFactory.TokenList(accessModifier)
            .AddRange(modifiersWithoutAccessibility);

        return method.WithModifiers(newModifiers);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// For public APIs, fields are made private and should typically be exposed via properties.
    /// This follows encapsulation best practices for public-facing components.
    /// </remarks>
    public override FieldDeclarationSyntax TransformFieldModifiers(
        FieldDeclarationSyntax field,
        ExtractionContext context)
    {
        // For public API, make fields private and expose via properties
        var modifiersWithoutAccessibility = RemoveAccessibilityModifiers(field.Modifiers);

        var newModifiers = SyntaxFactory.TokenList(
            SyntaxFactory.Token(SyntaxKind.PrivateKeyword))
            .AddRange(modifiersWithoutAccessibility);

        return field.WithModifiers(newModifiers);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Handles explicit PublicApi mode or automatic mode when source class is public
    /// and contains public members, indicating an API extraction scenario.
    /// </remarks>
    public override bool CanHandle(ExtractionContext context)
    {
        return context.Mode == ExtractionMode.PublicApi ||
               (context.Mode == ExtractionMode.Automatic &&
                context.IsSourceClassPublic &&
                context.HasPublicMembers);
    }
}
