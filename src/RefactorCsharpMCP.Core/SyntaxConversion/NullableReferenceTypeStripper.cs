using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorCsharpMCP.Core.SyntaxConversion;

/// <summary>
/// Strips nullable reference type annotations for frameworks that don't support C# 8.0+.
/// Transformations:
/// - string? → string
/// - List&lt;string?&gt; → List&lt;string&gt;
/// - Removes #nullable enable/disable directives
/// - Removes ! null-forgiving operators
/// </summary>
public class NullableReferenceTypeStripper : SyntaxConverterBase
{
    /// <summary>
    /// Gets the name of this converter.
    /// </summary>
    public override string Name => "NullableReferenceTypeStripper";

    /// <summary>
    /// Nullable reference types require C# 8.0.
    /// </summary>
    public override LanguageVersion MinimumSourceLanguageVersion => LanguageVersion.CSharp8;

    /// <summary>
    /// Frameworks with C# 7.3 or lower need stripping.
    /// </summary>
    public override LanguageVersion MaximumTargetLanguageVersion => LanguageVersion.CSharp7_3;

    /// <summary>
    /// Visits a nullable type and strips the ? annotation.
    /// </summary>
    public override SyntaxNode? VisitNullableType(NullableTypeSyntax node)
    {
        // Strip the nullable annotation, keeping the underlying type
        var elementType = (TypeSyntax)Visit(node.ElementType)!;
        return PreserveTrivia(elementType, node);
    }

    /// <summary>
    /// Visits a postfix unary expression to remove null-forgiving operator (!).
    /// </summary>
    public override SyntaxNode? VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
    {
        // If this is a null-forgiving operator (x!), remove it
        if (node.IsKind(SyntaxKind.SuppressNullableWarningExpression))
        {
            // Return just the operand, without the ! operator
            var operand = (ExpressionSyntax)Visit(node.Operand)!;
            return PreserveTrivia(operand, node);
        }

        return base.VisitPostfixUnaryExpression(node);
    }

    /// <summary>
    /// Visits trivia to remove #nullable directives.
    /// </summary>
    public override SyntaxTrivia VisitTrivia(SyntaxTrivia trivia)
    {
        // Remove #nullable enable/disable/restore directives
        if (trivia.IsKind(SyntaxKind.NullableDirectiveTrivia))
        {
            // Return empty trivia (removes the directive)
            return default;
        }

        return base.VisitTrivia(trivia);
    }

    /// <summary>
    /// Override Visit to handle trivia stripping at the root level.
    /// </summary>
    public override SyntaxNode? Visit(SyntaxNode? node)
    {
        if (node == null)
        {
            return null;
        }

        // First, perform the standard visit
        var visited = base.Visit(node);

        if (visited == null)
        {
            return null;
        }

        // Then, remove #nullable directives from all trivia
        visited = RemoveNullableDirectives(visited);

        return visited;
    }

    /// <summary>
    /// Removes #nullable directives from a syntax node's trivia.
    /// </summary>
    private SyntaxNode RemoveNullableDirectives(SyntaxNode node)
    {
        // Process leading trivia
        var leadingTrivia = node.GetLeadingTrivia();
        var newLeadingTrivia = leadingTrivia.Where(t => !t.IsKind(SyntaxKind.NullableDirectiveTrivia));

        // Process trailing trivia
        var trailingTrivia = node.GetTrailingTrivia();
        var newTrailingTrivia = trailingTrivia.Where(t => !t.IsKind(SyntaxKind.NullableDirectiveTrivia));

        return node
            .WithLeadingTrivia(newLeadingTrivia)
            .WithTrailingTrivia(newTrailingTrivia);
    }
}
