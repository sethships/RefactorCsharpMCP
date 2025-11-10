using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorCsharpMCP.Core.Refactorings.ExtractClassComponents.Strategies;

/// <summary>
/// Defines the strategy for determining modifiers when extracting classes and their members.
/// Implementations handle different extraction scenarios (composition, public API, inheritance, etc.).
/// </summary>
public interface IExtractionModifierStrategy
{
    /// <summary>
    /// Gets the name of this strategy for identification and logging.
    /// </summary>
    string StrategyName { get; }

    /// <summary>
    /// Determines the modifiers for the extracted class.
    /// </summary>
    /// <param name="context">Context containing source class and extraction details.</param>
    /// <returns>The modifier tokens for the new class (e.g., public, internal).</returns>
    SyntaxTokenList GetClassModifiers(ExtractionContext context);

    /// <summary>
    /// Transforms a method's modifiers for the extracted class.
    /// </summary>
    /// <param name="method">The method being extracted.</param>
    /// <param name="context">Context containing source class and extraction details.</param>
    /// <returns>The transformed method with appropriate modifiers.</returns>
    MethodDeclarationSyntax TransformMethodModifiers(
        MethodDeclarationSyntax method,
        ExtractionContext context);

    /// <summary>
    /// Transforms a field's modifiers for the extracted class.
    /// </summary>
    /// <param name="field">The field being extracted.</param>
    /// <param name="context">Context containing source class and extraction details.</param>
    /// <returns>The transformed field with appropriate modifiers.</returns>
    FieldDeclarationSyntax TransformFieldModifiers(
        FieldDeclarationSyntax field,
        ExtractionContext context);

    /// <summary>
    /// Determines if this strategy can handle the given extraction scenario.
    /// Used by the factory for automatic strategy selection.
    /// </summary>
    /// <param name="context">Context containing source class and extraction details.</param>
    /// <returns>True if this strategy is appropriate for the scenario; otherwise, false.</returns>
    bool CanHandle(ExtractionContext context);
}
