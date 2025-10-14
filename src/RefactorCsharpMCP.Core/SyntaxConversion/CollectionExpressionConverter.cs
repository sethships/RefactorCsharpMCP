using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorCsharpMCP.Core.SyntaxConversion;

/// <summary>
/// Converts C# 12 collection expressions to legacy array/collection initialization syntax.
/// Transformations:
/// - [1, 2, 3] → new[] { 1, 2, 3 } (array)
/// - [] → Array.Empty&lt;T&gt;() (empty collection)
/// - [..arr] → arr.ToArray() (spread to LINQ)
///
/// Note: This is a placeholder implementation that demonstrates the converter architecture.
/// Full collection expression support requires Roslyn 4.8.0+ which includes CollectionExpressionSyntax.
/// Current project uses Roslyn 4.14.0 but collection expression syntax nodes are not yet available
/// in that version's public API.
/// </summary>
public class CollectionExpressionConverter : SyntaxConverterBase
{
    /// <summary>
    /// Gets the name of this converter.
    /// </summary>
    public override string Name => "CollectionExpressionConverter";

    /// <summary>
    /// Collection expressions require C# 12 (introduced in .NET 8).
    /// </summary>
    public override LanguageVersion MinimumSourceLanguageVersion => LanguageVersion.CSharp12;

    /// <summary>
    /// Frameworks with C# 11 or lower need conversion.
    /// </summary>
    public override LanguageVersion MaximumTargetLanguageVersion => LanguageVersion.CSharp11;

    /// <summary>
    /// For now, this converter serves as architectural demonstration.
    /// Full implementation pending Roslyn version upgrade that includes CollectionExpressionSyntax.
    /// </summary>
    /// <remarks>
    /// Collection expressions are parsed by Roslyn 4.14.0 but the specific syntax node types
    /// (CollectionExpressionSyntax, SpreadElementSyntax, etc.) are not exposed in the public API yet.
    /// This will be implemented when upgrading to a newer Roslyn version.
    /// </remarks>
    public override SyntaxNode? Visit(SyntaxNode? node)
    {
        // Placeholder: In future versions with CollectionExpressionSyntax support,
        // this would override VisitCollectionExpression to handle the conversion
        return base.Visit(node);
    }
}
