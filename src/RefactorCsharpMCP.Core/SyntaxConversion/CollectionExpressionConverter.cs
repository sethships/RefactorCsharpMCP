using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorCsharpMCP.Core.SyntaxConversion;

/// <summary>
/// Converts C# 12 collection expressions to legacy array/collection initialization syntax.
///
/// Transformations:
/// - [1, 2, 3] → new[] { 1, 2, 3 } (array)
/// - [] → Array.Empty&lt;T&gt;() (empty collection)
/// - [..arr] → arr.ToArray() (spread to LINQ)
///
/// IMPLEMENTATION STATUS: Intentionally deferred (not a missing API issue).
///
/// The CollectionExpressionSyntax API is fully available in Roslyn 4.14.0 (introduced in 4.7.0).
/// However, full implementation is deferred because:
///
/// 1. RARE USE CASE: Collection expressions are C# 12 (2023). Targeting frameworks requiring
///    C# 11 or lower is uncommon as of 2025. Most production code targets modern frameworks
///    that support collection expressions natively.
///
/// 2. COMPLEX TRIVIA PRESERVATION: Like TupleReturnConverter, this requires sophisticated
///    trivia management during major syntax transformations:
///    - Whitespace and formatting preservation
///    - Handling nested collection expressions
///    - Type inference for empty collections ([] → Array.Empty&lt;T&gt;())
///    - Spread element conversion ([..items] → items.ToArray())
///    - Proper indentation and code formatting
///
/// 3. FOCUS ON HIGH-VALUE FEATURES: V1 prioritizes refactorings with clear ROI.
///    Collection expression downgrading serves a niche migration scenario.
///
/// This converter demonstrates the architecture and will be fully implemented when
/// real-world demand emerges. See docs/FUTURE-ROADMAP.md for implementation timeline.
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
    /// Currently returns the node unchanged as full implementation is intentionally deferred.
    /// </summary>
    /// <remarks>
    /// IMPLEMENTATION READY: CollectionExpressionSyntax is available in Roslyn 4.14.0.
    ///
    /// To implement, override VisitCollectionExpression:
    /// <code>
    /// public override SyntaxNode? VisitCollectionExpression(CollectionExpressionSyntax node)
    /// {
    ///     // Convert [1, 2, 3] → new[] { 1, 2, 3 }
    ///     // Convert [] → Array.Empty&lt;T&gt;() (requires type inference)
    ///     // Convert [..arr] → arr.ToArray()
    /// }
    /// </code>
    ///
    /// Implementation requires:
    /// - Type inference for empty collections (requires SemanticModel)
    /// - Spread element transformation
    /// - Trivia preservation during structural changes
    ///
    /// See docs/FUTURE-ROADMAP.md Section "Collection Expression Converter Implementation"
    /// for complete implementation plan.
    /// </remarks>
    public override SyntaxNode? Visit(SyntaxNode? node)
    {
        // Implementation deferred - see class documentation and FUTURE-ROADMAP.md
        return base.Visit(node);
    }
}
