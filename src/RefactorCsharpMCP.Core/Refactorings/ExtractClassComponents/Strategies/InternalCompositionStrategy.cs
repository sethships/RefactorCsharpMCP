using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorCsharpMCP.Core.Refactorings.ExtractClassComponents.Strategies;

/// <summary>
/// Default strategy matching current ExtractClass behavior - internal visibility for composition pattern.
/// Creates internal classes with internal methods, implementing encapsulated implementation details
/// accessed via composition field in the source class.
/// </summary>
/// <remarks>
/// This is the default strategy for backward compatibility. It generates:
/// <list type="bullet">
/// <item><description>Internal class declaration</description></item>
/// <item><description>Internal methods (transformed from any visibility)</description></item>
/// <item><description>Fields preserved as-is</description></item>
/// </list>
/// </remarks>
public class InternalCompositionStrategy : ExtractionModifierStrategyBase
{
    /// <inheritdoc/>
    public override string StrategyName => "InternalComposition";

    /// <inheritdoc/>
    /// <remarks>
    /// Always returns internal modifier for composition pattern encapsulation.
    /// </remarks>
    public override SyntaxTokenList GetClassModifiers(ExtractionContext context)
    {
        return SyntaxFactory.TokenList(
            SyntaxFactory.Token(SyntaxKind.InternalKeyword));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Transforms all methods to internal visibility regardless of original visibility.
    /// Preserves other modifiers (static, async, virtual, etc.).
    /// </remarks>
    public override MethodDeclarationSyntax TransformMethodModifiers(
        MethodDeclarationSyntax method,
        ExtractionContext context)
    {
        var modifiersWithoutAccessibility = RemoveAccessibilityModifiers(method.Modifiers);

        var newModifiers = SyntaxFactory.TokenList()
            .Add(SyntaxFactory.Token(SyntaxKind.InternalKeyword))
            .AddRange(modifiersWithoutAccessibility);

        return method.WithModifiers(newModifiers);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Preserves field modifiers as-is for composition pattern.
    /// Fields are typically private in the extracted class and accessed via methods.
    /// </remarks>
    public override FieldDeclarationSyntax TransformFieldModifiers(
        FieldDeclarationSyntax field,
        ExtractionContext context)
    {
        // Preserve field modifiers as-is for composition pattern
        return field;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Handles Default and explicit InternalComposition modes.
    /// This is the fallback strategy when no other strategy matches.
    /// </remarks>
    public override bool CanHandle(ExtractionContext context)
    {
        // This is the default strategy - handles Default and explicit InternalComposition modes
        return context.Mode == ExtractionMode.Default ||
               context.Mode == ExtractionMode.InternalComposition;
    }
}
