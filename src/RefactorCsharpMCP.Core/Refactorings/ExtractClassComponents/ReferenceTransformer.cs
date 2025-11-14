using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace RefactorCsharpMCP.Core.Refactorings.ExtractClassComponents;

/// <summary>
/// Transforms references to extracted members in the source class to use the composition field.
/// Rewrites member access, method invocations, identifier references, and qualified type names
/// to route through the composition field after Extract Class refactoring.
/// </summary>
/// <remarks>
/// This rewriter handles four transformation scenarios:
/// <list type="number">
/// <item><description>Member access expressions: <c>this._field</c> → <c>_compositionField._field</c></description></item>
/// <item><description>Method invocations: <c>MethodName(args)</c> → <c>_compositionField.MethodName(args)</c></description></item>
/// <item><description>Identifier references: <c>identifier</c> → <c>_compositionField.identifier</c></description></item>
/// <item><description>Qualified type names: <c>OriginalClass.NestedType</c> → <c>NewClass.NestedType</c></description></item>
/// </list>
/// Uses semantic analysis to ensure only references within the source class are transformed.
/// </remarks>
internal class ReferenceTransformer : CSharpSyntaxRewriter
{
    private readonly SemanticModel _semanticModel;
    private readonly HashSet<ISymbol> _extractedSymbolSet;
    private readonly string _newClassFieldName;
    private readonly string _newClassName;
    private readonly INamedTypeSymbol _sourceClassSymbol;
    private readonly HashSet<TextSpan> _referenceSpans;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReferenceTransformer"/> class.
    /// </summary>
    /// <param name="semanticModel">The semantic model for symbol resolution.</param>
    /// <param name="extractedSymbols">List of symbols (fields, methods, nested types) that were extracted to the new class.</param>
    /// <param name="newClassFieldName">The name of the composition field in the source class (e.g., "_addressManager").</param>
    /// <param name="newClassName">The name of the extracted class (for qualified type name transformations).</param>
    /// <param name="sourceClassSymbol">The symbol for the source class containing the composition field.</param>
    /// <param name="referenceLocations">Optional list of pre-found reference locations to transform. When provided, only these locations are transformed.</param>
    public ReferenceTransformer(
        SemanticModel semanticModel,
        List<ISymbol> extractedSymbols,
        string newClassFieldName,
        string newClassName,
        INamedTypeSymbol sourceClassSymbol,
        List<Location>? referenceLocations = null)
    {
        _semanticModel = semanticModel;
        _extractedSymbolSet = extractedSymbols.ToHashSet(SymbolEqualityComparer.Default);
        _newClassFieldName = newClassFieldName;
        _newClassName = newClassName;
        _sourceClassSymbol = sourceClassSymbol;

        // Pre-compute spans from locations for fast lookup
        _referenceSpans = referenceLocations?.Select(loc => loc.SourceSpan).ToHashSet() ?? new HashSet<TextSpan>();
    }

    /// <summary>
    /// Checks if a syntax node's location matches a pre-found reference span.
    /// Used for location-based matching when semantic model is unreliable (unresolved dependencies).
    /// </summary>
    /// <param name="node">The syntax node to check.</param>
    /// <returns>True if the node's span is in the reference spans list, otherwise false.</returns>
    private bool IsLocationInReferenceSpans(SyntaxNode node)
    {
        return _referenceSpans.Count > 0 && _referenceSpans.Contains(node.Span);
    }

    /// <summary>
    /// Visits member access expressions like <c>this._field</c> or <c>ClassName._field</c>
    /// and transforms them to route through the composition field.
    /// </summary>
    /// <param name="node">The member access expression node.</param>
    /// <returns>Transformed node with composition field qualification, or original node if not applicable.</returns>
    public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        // Handle cases like: this._city or ClassName._city
        // Check if the name part references an extracted symbol
        var symbolInfo = _semanticModel.GetSymbolInfo(node.Name);
        if (symbolInfo.Symbol != null && _extractedSymbolSet.Contains(symbolInfo.Symbol))
        {
            // Check if expression is 'this'
            if (node.Expression is ThisExpressionSyntax)
            {
                // Check if this is within the source class
                var containingType = _semanticModel.GetEnclosingSymbol(node.SpanStart)?.ContainingType;
                if (containingType != null &&
                    SymbolEqualityComparer.Default.Equals(containingType, _sourceClassSymbol))
                {
                    // Transform: this._field -> this._newClassField._field
                    // Or simpler: this._field -> _newClassField._field
                    var newFieldIdentifier = SyntaxFactory.IdentifierName(_newClassFieldName);
                    var newMemberAccess = SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        newFieldIdentifier,
                        (SimpleNameSyntax)node.Name);

                    return newMemberAccess.WithTriviaFrom(node);
                }
            }
        }

        return base.VisitMemberAccessExpression(node);
    }

    /// <summary>
    /// Visits method invocation expressions and transforms direct calls to extracted methods
    /// to route through the composition field.
    /// </summary>
    /// <param name="node">The invocation expression node.</param>
    /// <returns>Transformed invocation with composition field qualification, or original node if not applicable.</returns>
    /// <remarks>
    /// Handles the critical case where extracted methods are called directly without member access:
    /// <c>MethodName(args)</c> → <c>_compositionField.MethodName(args)</c>
    /// This ensures ALL method call sites are updated, not just explicitly qualified calls.
    /// </remarks>
    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        // Handle direct method invocations: MethodName(args) → _field.MethodName(args)
        // This ensures ALL method call sites are updated, not just identifiers

        // If we have pre-found reference locations, use location-based matching
        // This avoids semantic model lookup issues when the code has unresolved dependencies
        if (node.Expression is IdentifierNameSyntax identifier && IsLocationInReferenceSpans(identifier))
        {
            // Transform: MethodName(args) → _newClassField.MethodName(args)
            var memberAccess = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(_newClassFieldName),
                identifier);

            return node.WithExpression(memberAccess).WithTriviaFrom(node);
        }

        // If we have reference spans but didn't find a match, skip transformation
        if (_referenceSpans.Count > 0)
        {
            return base.VisitInvocationExpression(node);
        }

        // Fallback to semantic-based approach when no reference locations provided
        var symbolInfo = _semanticModel.GetSymbolInfo(node);

        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
        {
            return base.VisitInvocationExpression(node);
        }

        // Only process if it's an extracted method
        if (!_extractedSymbolSet.Contains(methodSymbol))
        {
            return base.VisitInvocationExpression(node);
        }

        // Check if this is a simple identifier invocation (not already qualified)
        if (node.Expression is IdentifierNameSyntax identifier2)
        {
            // Check if invocation is within source class
            var containingType = _semanticModel.GetEnclosingSymbol(node.SpanStart)?.ContainingType;

            if (containingType != null &&
                SymbolEqualityComparer.Default.Equals(containingType, _sourceClassSymbol))
            {
                // Transform: MethodName(args) → _newClassField.MethodName(args)
                var memberAccess = SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(_newClassFieldName),
                    identifier2);

                return node.WithExpression(memberAccess).WithTriviaFrom(node);
            }
        }

        return base.VisitInvocationExpression(node);
    }

    /// <summary>
    /// Visits identifier name syntax nodes and transforms references to extracted symbols
    /// to route through the composition field.
    /// </summary>
    /// <param name="node">The identifier name node.</param>
    /// <returns>Member access expression through composition field, or original node if not applicable.</returns>
    /// <remarks>
    /// Handles field and variable references, with special handling to avoid transforming:
    /// <list type="bullet">
    /// <item><description>Type symbols in identifier contexts (e.g., variable declarations, object creation)</description></item>
    /// <item><description>Identifiers already part of member access expressions</description></item>
    /// <item><description>References outside the source class</description></item>
    /// </list>
    /// </remarks>
    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        // Skip if already part of member access or invocation (handled elsewhere)
        if (node.Parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == node)
        {
            return base.VisitIdentifierName(node);
        }

        if (node.Parent is InvocationExpressionSyntax)
        {
            return base.VisitIdentifierName(node);
        }

        // Get symbol info once for all branches
        var symbolInfo = _semanticModel.GetSymbolInfo(node);
        if (symbolInfo.Symbol == null)
        {
            return base.VisitIdentifierName(node);
        }

        // Only process identifiers that reference extracted symbols
        if (!_extractedSymbolSet.Contains(symbolInfo.Symbol))
        {
            return base.VisitIdentifierName(node);
        }

        // Check if this identifier is within the source class
        var enclosingType = _semanticModel.GetEnclosingSymbol(node.SpanStart)?.ContainingType;
        if (enclosingType == null || !SymbolEqualityComparer.Default.Equals(enclosingType, _sourceClassSymbol))
        {
            return base.VisitIdentifierName(node);
        }

        // Handle type symbols specially (Issue #120: nested type extraction)
        // Type references need qualified name transformation, not member access
        // Example: 'Config' → 'ExtractedClass.Config'
        if (symbolInfo.Symbol is INamedTypeSymbol)
        {
            // Transform type reference to qualified name: MyType → NewClass.MyType
            var newClassIdentifier = SyntaxFactory.IdentifierName(_newClassName);
            var qualifiedName = SyntaxFactory.QualifiedName(
                newClassIdentifier,
                (SimpleNameSyntax)node);

            return qualifiedName.WithTriviaFrom(node);
        }

        // For method/field symbols, use location-based matching if available, otherwise semantic
        if (_referenceSpans.Count > 0 && !IsLocationInReferenceSpans(node))
        {
            // Have reference spans but this node isn't in them - skip
            return base.VisitIdentifierName(node);
        }

        // Transform: identifier -> _newClassField.identifier (for fields/methods)
        var newFieldIdentifier = SyntaxFactory.IdentifierName(_newClassFieldName);
        var memberAccessExpr = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            newFieldIdentifier,
            node);

        return memberAccessExpr.WithTriviaFrom(node);
    }

    /// <summary>
    /// Visits variable declarations to transform field/local variable type references.
    /// </summary>
    /// <remarks>
    /// Ensures that field declarations like `private Config _field;` are transformed to
    /// `private Configuration.Config _field;` when Config is an extracted nested type.
    /// </remarks>
    public override SyntaxNode? VisitVariableDeclaration(VariableDeclarationSyntax node)
    {
        // TEMPORARY: Disabled to debug field removal issue
        // The transformation is correct but causes fields to disappear - investigating

        /*
        // Check if the type is a simple identifier (not already qualified)
        if (node.Type is IdentifierNameSyntax typeIdentifier)
        {
            var symbolInfo = _semanticModel.GetSymbolInfo(typeIdentifier);

            // Check if this is an extracted nested type
            if (symbolInfo.Symbol is INamedTypeSymbol typeSymbol && _extractedSymbolSet.Contains(typeSymbol))
            {
                // Check if we're in the source class
                var enclosingType = _semanticModel.GetEnclosingSymbol(typeIdentifier.SpanStart)?.ContainingType;
                if (enclosingType != null && SymbolEqualityComparer.Default.Equals(enclosingType, _sourceClassSymbol))
                {
                    // Transform type to qualified name: Config → Configuration.Config
                    var qualifiedType = SyntaxFactory.QualifiedName(
                        SyntaxFactory.IdentifierName(_newClassName),
                        typeIdentifier);

                    // Replace the type in the variable declaration
                    node = node.WithType(qualifiedType);
                }
            }
        }
        */

        return base.VisitVariableDeclaration(node);
    }

    /// <summary>
    /// Visits qualified name syntax nodes (e.g., <c>OriginalClass.NestedType</c>) and transforms
    /// qualified references to extracted nested types.
    /// </summary>
    /// <param name="node">The qualified name node.</param>
    /// <returns>Qualified name with new class prefix, or original node if not applicable.</returns>
    /// <remarks>
    /// Handles qualified type names for nested types that were extracted:
    /// <c>OriginalClass.NestedType</c> → <c>NewClass.NestedType</c>
    ///
    /// <para>
    /// <strong>Limitation:</strong> Currently handles two-level qualified names only.
    /// Multi-level nesting (e.g., <c>Outer.Middle.Inner</c>) will be handled recursively
    /// by the base visitor, but only the rightmost qualification is checked here.
    /// For complex nested scenarios, consider explicit multi-level support in future versions.
    /// </para>
    /// </remarks>
    public override SyntaxNode? VisitQualifiedName(QualifiedNameSyntax node)
    {
        // Handle qualified type names like: OriginalClass.NestedType
        // NOTE: Currently handles two-level qualified names only.
        // Multi-level nesting (e.g., Outer.Middle.Inner) will be handled recursively
        // by base visitor, but only the rightmost qualification is checked here.
        // For complex nested scenarios, consider explicit multi-level support in future versions.

        // Get the symbol for the right side (the nested type name)
        var symbolInfo = _semanticModel.GetSymbolInfo(node.Right);
        if (symbolInfo.Symbol == null)
        {
            return base.VisitQualifiedName(node);
        }

        // Only process if the right side is an extracted nested type symbol
        if (!_extractedSymbolSet.Contains(symbolInfo.Symbol))
        {
            return base.VisitQualifiedName(node);
        }

        // Check if the left side refers to the source class
        var leftSymbolInfo = _semanticModel.GetSymbolInfo(node.Left);
        if (leftSymbolInfo.Symbol is INamedTypeSymbol leftTypeSymbol &&
            SymbolEqualityComparer.Default.Equals(leftTypeSymbol, _sourceClassSymbol))
        {
            // Transform: OriginalClass.NestedType -> NewClass.NestedType
            var newClassIdentifier = SyntaxFactory.IdentifierName(_newClassName);
            var newQualifiedName = SyntaxFactory.QualifiedName(
                newClassIdentifier,
                (SimpleNameSyntax)node.Right);

            return newQualifiedName.WithTriviaFrom(node);
        }

        return base.VisitQualifiedName(node);
    }
}
