using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorCsharpMCP.Core.Refactorings.ExtractClassComponents;

/// <summary>
/// Single-pass syntax rewriter that transforms the source class and adds the extracted class.
/// Eliminates stale reference issues by performing all transformations in one tree traversal.
/// </summary>
/// <remarks>
/// This rewriter performs the following transformations in a single pass:
/// <list type="number">
/// <item><description>Removes extracted members (fields, methods, nested types) from source class</description></item>
/// <item><description>Adds composition field to source class</description></item>
/// <item><description>Adds extracted class to the same namespace/scope</description></item>
/// </list>
/// </remarks>
public class ExtractClassTransformer : CSharpSyntaxRewriter
{
    private readonly string _sourceClassName;
    private readonly HashSet<string> _fieldNamesToRemove;
    private readonly HashSet<string> _methodNamesToRemove;
    private readonly HashSet<string> _nestedTypeNamesToRemove;
    private readonly FieldDeclarationSyntax _compositionField;
    private readonly ClassDeclarationSyntax _extractedClass;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractClassTransformer"/> class.
    /// </summary>
    /// <param name="sourceClassName">Name of the source class being refactored.</param>
    /// <param name="fieldsToRemove">Fields to extract from source class.</param>
    /// <param name="methodsToRemove">Methods to extract from source class.</param>
    /// <param name="nestedTypesToRemove">Nested type names to extract from source class.</param>
    /// <param name="compositionField">Composition field to add to source class.</param>
    /// <param name="extractedClass">New extracted class to add to namespace.</param>
    public ExtractClassTransformer(
        string sourceClassName,
        IEnumerable<FieldDeclarationSyntax> fieldsToRemove,
        IEnumerable<MethodDeclarationSyntax> methodsToRemove,
        IEnumerable<string> nestedTypesToRemove,
        FieldDeclarationSyntax compositionField,
        ClassDeclarationSyntax extractedClass)
    {
        _sourceClassName = sourceClassName ?? throw new ArgumentNullException(nameof(sourceClassName));
        _compositionField = compositionField ?? throw new ArgumentNullException(nameof(compositionField));
        _extractedClass = extractedClass ?? throw new ArgumentNullException(nameof(extractedClass));

        // Build hash sets for O(1) lookup during traversal
        _fieldNamesToRemove = new HashSet<string>(
            (fieldsToRemove ?? throw new ArgumentNullException(nameof(fieldsToRemove)))
            .SelectMany(f => f.Declaration.Variables.Select(v => v.Identifier.Text)));

        _methodNamesToRemove = new HashSet<string>(
            (methodsToRemove ?? throw new ArgumentNullException(nameof(methodsToRemove)))
            .Select(m => m.Identifier.Text));

        _nestedTypeNamesToRemove = new HashSet<string>(
            nestedTypesToRemove ?? throw new ArgumentNullException(nameof(nestedTypesToRemove)));
    }

    /// <summary>
    /// Visits a class declaration node.
    /// If this is the source class, removes extracted members and adds composition field.
    /// </summary>
    /// <param name="node">The class declaration to visit.</param>
    /// <returns>The transformed class declaration, or the original if not the source class.</returns>
    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        // First visit children to ensure nested transformations happen
        var visited = (ClassDeclarationSyntax?)base.VisitClassDeclaration(node);
        if (visited == null)
            return null;

        // Check if this is the source class
        if (visited.Identifier.Text != _sourceClassName)
            return visited;

        // Transform source class: remove extracted members and add composition field
        var newMembers = new List<MemberDeclarationSyntax>();

        // Add composition field first
        newMembers.Add(_compositionField);

        // Filter members: keep only those NOT being extracted
        foreach (var member in visited.Members)
        {
            var shouldKeep = member switch
            {
                FieldDeclarationSyntax field => !ShouldRemoveField(field),
                MethodDeclarationSyntax method => !ShouldRemoveMethod(method),
                BaseTypeDeclarationSyntax nestedType => !ShouldRemoveNestedType(nestedType),
                _ => true // Keep all other member types (properties, events, etc.)
            };

            if (shouldKeep)
            {
                newMembers.Add(member);
            }
        }

        return visited.WithMembers(SyntaxFactory.List(newMembers));
    }

    /// <summary>
    /// Visits a namespace declaration node.
    /// Adds the extracted class after the source class.
    /// </summary>
    /// <param name="node">The namespace declaration to visit.</param>
    /// <returns>The transformed namespace with extracted class added.</returns>
    public override SyntaxNode? VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
    {
        var visited = (NamespaceDeclarationSyntax?)base.VisitNamespaceDeclaration(node);
        if (visited == null)
            return null;

        // Check if this namespace contains the source class
        if (!ContainsClass(visited, _sourceClassName))
            return visited;

        // Add extracted class to namespace members
        var membersWithExtractedClass = visited.Members.Add(_extractedClass);
        return visited.WithMembers(membersWithExtractedClass);
    }

    /// <summary>
    /// Visits a file-scoped namespace declaration node.
    /// Adds the extracted class after the source class.
    /// </summary>
    /// <param name="node">The file-scoped namespace declaration to visit.</param>
    /// <returns>The transformed namespace with extracted class added.</returns>
    public override SyntaxNode? VisitFileScopedNamespaceDeclaration(FileScopedNamespaceDeclarationSyntax node)
    {
        var visited = (FileScopedNamespaceDeclarationSyntax?)base.VisitFileScopedNamespaceDeclaration(node);
        if (visited == null)
            return null;

        // Check if this namespace contains the source class
        if (!ContainsClass(visited, _sourceClassName))
            return visited;

        // Add extracted class to namespace members
        var membersWithExtractedClass = visited.Members.Add(_extractedClass);
        return visited.WithMembers(membersWithExtractedClass);
    }

    /// <summary>
    /// Visits the compilation unit root node.
    /// Handles adding extracted class when no namespace is present.
    /// </summary>
    /// <param name="node">The compilation unit to visit.</param>
    /// <returns>The transformed compilation unit with extracted class added if no namespace exists.</returns>
    public override SyntaxNode? VisitCompilationUnit(CompilationUnitSyntax node)
    {
        var visited = (CompilationUnitSyntax?)base.VisitCompilationUnit(node);
        if (visited == null)
            return null;

        // Only add extracted class at compilation unit level if no namespace exists
        var hasNamespace = visited.Members.Any(m =>
            m is NamespaceDeclarationSyntax || m is FileScopedNamespaceDeclarationSyntax);

        if (hasNamespace)
            return visited;

        // Check if source class exists at compilation unit level
        if (!ContainsClass(visited, _sourceClassName))
            return visited;

        // Add extracted class to compilation unit members
        var membersWithExtractedClass = visited.Members.Add(_extractedClass);
        return visited.WithMembers(membersWithExtractedClass);
    }

    /// <summary>
    /// Determines if a field should be removed from the source class.
    /// </summary>
    /// <param name="field">The field to check.</param>
    /// <returns>True if the field should be removed; otherwise, false.</returns>
    private bool ShouldRemoveField(FieldDeclarationSyntax field)
    {
        // A field declaration can have multiple variables (e.g., int x, y, z;)
        // Remove if ANY variable in the declaration is marked for extraction
        return field.Declaration.Variables.Any(v =>
            _fieldNamesToRemove.Contains(v.Identifier.Text));
    }

    /// <summary>
    /// Determines if a method should be removed from the source class.
    /// </summary>
    /// <param name="method">The method to check.</param>
    /// <returns>True if the method should be removed; otherwise, false.</returns>
    private bool ShouldRemoveMethod(MethodDeclarationSyntax method)
    {
        return _methodNamesToRemove.Contains(method.Identifier.Text);
    }

    /// <summary>
    /// Determines if a nested type should be removed from the source class.
    /// </summary>
    /// <param name="nestedType">The nested type to check.</param>
    /// <returns>True if the nested type should be removed; otherwise, false.</returns>
    private bool ShouldRemoveNestedType(BaseTypeDeclarationSyntax nestedType)
    {
        return _nestedTypeNamesToRemove.Contains(nestedType.Identifier.Text);
    }

    /// <summary>
    /// Checks if a container (namespace or compilation unit) contains a class with the specified name.
    /// </summary>
    /// <param name="container">The container to search.</param>
    /// <param name="className">The class name to search for.</param>
    /// <returns>True if the container contains the class; otherwise, false.</returns>
    private static bool ContainsClass(SyntaxNode container, string className)
    {
        return container.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Any(c => c.Identifier.Text == className);
    }
}
