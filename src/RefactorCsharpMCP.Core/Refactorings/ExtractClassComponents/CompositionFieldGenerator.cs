using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorCsharpMCP.Core.Refactorings.ExtractClassComponents;

/// <summary>
/// Generates composition field declarations for the Extract Class refactoring.
/// Creates private readonly fields initialized with new instances of the extracted class,
/// implementing the composition pattern for class decomposition.
/// </summary>
public static class CompositionFieldGenerator
{
    /// <summary>
    /// Creates a private readonly field declaration for composition pattern.
    /// Generates a field with automatic initialization (e.g., <c>private readonly ExtractedClass _field = new ExtractedClass();</c>).
    /// </summary>
    /// <param name="extractedClassName">The name of the extracted class (field type).</param>
    /// <param name="fieldName">The name of the composition field (typically camelCase with underscore prefix).</param>
    /// <returns>A <see cref="FieldDeclarationSyntax"/> representing the private readonly composition field with initialization.</returns>
    /// <remarks>
    /// The generated field follows these conventions:
    /// <list type="bullet">
    /// <item><description>Visibility: private (encapsulated implementation detail)</description></item>
    /// <item><description>Modifier: readonly (immutable reference after construction)</description></item>
    /// <item><description>Initialization: parameterless constructor call (<c>new ExtractedClass()</c>)</description></item>
    /// </list>
    /// <example>
    /// <code>
    /// var field = CompositionFieldGenerator.CreateCompositionField("AddressManager", "_addressManager");
    /// // Generates: private readonly AddressManager _addressManager = new AddressManager();
    /// </code>
    /// </example>
    /// </remarks>
    public static FieldDeclarationSyntax CreateCompositionField(string extractedClassName, string fieldName)
    {
        var variableDeclaration = SyntaxFactory.VariableDeclaration(
            SyntaxFactory.IdentifierName(extractedClassName))
            .AddVariables(
                SyntaxFactory.VariableDeclarator(fieldName)
                    .WithInitializer(
                        SyntaxFactory.EqualsValueClause(
                            SyntaxFactory.ObjectCreationExpression(
                                SyntaxFactory.IdentifierName(extractedClassName))
                            .WithArgumentList(SyntaxFactory.ArgumentList()))));

        return SyntaxFactory.FieldDeclaration(variableDeclaration)
            .AddModifiers(
                SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));
    }
}
