using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace RefactorCsharpMCP.Core.Refactorings.ExtractClassComponents;

/// <summary>
/// Transforms type identifiers to qualified names based on location-based matching.
/// Part of the Transform Planning Pattern - applies transformations collected during analysis phase.
/// </summary>
/// <remarks>
/// <para><strong>Design Pattern</strong>: This transformer uses location-based matching (TextSpan)
/// instead of semantic analysis, allowing it to work on modified syntax trees without SemanticModel issues.</para>
///
/// <para><strong>Solves Issue #124</strong>: Field type qualification without node identity conflicts.
/// By using TextSpan matching, transformations survive tree mutations and don't require SemanticModel.</para>
///
/// <para><strong>Example Transformation</strong>:</para>
/// <code>
/// // Before: private Config _config;
/// // After:  private Configuration.Config _config;
/// </code>
/// </remarks>
internal class TypeQualificationTransformer : CSharpSyntaxRewriter
{
    private readonly Dictionary<TextSpan, TypeQualificationInfo> _typeQualifications;

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeQualificationTransformer"/> class.
    /// </summary>
    /// <param name="typeQualifications">Dictionary mapping type identifier locations to qualification metadata.</param>
    public TypeQualificationTransformer(Dictionary<TextSpan, TypeQualificationInfo> typeQualifications)
    {
        _typeQualifications = typeQualifications;
    }

    /// <summary>
    /// Visits identifier names and transforms them to qualified names if they match a type qualification.
    /// </summary>
    /// <param name="node">The identifier name node.</param>
    /// <returns>Qualified name if transformation needed, otherwise original node.</returns>
    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        // Check if this identifier location needs type qualification
        if (_typeQualifications.TryGetValue(node.Span, out var qualInfo))
        {
            // Transform: Config → Configuration.Config
            var qualifiedName = SyntaxFactory.QualifiedName(
                SyntaxFactory.IdentifierName(qualInfo.NewClassName),
                node).WithTriviaFrom(node);

            // Return immediately without calling base to avoid traversing the new qualified name
            return qualifiedName;
        }

        // No transformation needed for this identifier
        return base.VisitIdentifierName(node);
    }

    /// <summary>
    /// Visits variable declarations. This override exists to ensure we don't accidentally
    /// traverse into variable declarators when we've already transformed the type.
    /// </summary>
    /// <param name="node">The variable declaration node.</param>
    /// <returns>Transformed node with qualified type, or original node.</returns>
    /// <remarks>
    /// The transformation happens in VisitIdentifierName when it encounters the type identifier.
    /// This method just ensures proper traversal behavior.
    /// </remarks>
    public override SyntaxNode? VisitVariableDeclaration(VariableDeclarationSyntax node)
    {
        // Let the base implementation handle traversal
        // The actual transformation happens when VisitIdentifierName encounters the type
        return base.VisitVariableDeclaration(node);
    }
}
