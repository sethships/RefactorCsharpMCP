using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCsharpMCP.Core.Utilities;

namespace RefactorCsharpMCP.Core.Refactorings.ExtractClassComponents;

/// <summary>
/// Handles finding and updating references to extracted members.
/// Coordinates symbol resolution, reference finding, and reference transformation
/// for Extract Class refactoring.
/// </summary>
internal class ReferenceUpdater
{
    private readonly SymbolResolutionHelper _symbolHelper;

    public ReferenceUpdater()
    {
        _symbolHelper = new SymbolResolutionHelper();
    }

    /// <summary>
    /// Gets symbols for extracted field, method, and nested type declarations.
    /// </summary>
    public List<ISymbol> GetExtractedSymbols(
        SemanticModel semanticModel,
        List<FieldDeclarationSyntax> fieldDeclarations,
        List<MethodDeclarationSyntax> methodDeclarations,
        List<BaseTypeDeclarationSyntax> nestedTypeDeclarations)
    {
        var symbols = new List<ISymbol>();

        // Get field symbols
        foreach (var field in fieldDeclarations)
        {
            foreach (var variable in field.Declaration.Variables)
            {
                var symbol = semanticModel.GetDeclaredSymbol(variable);
                if (symbol != null)
                {
                    symbols.Add(symbol);
                }
            }
        }

        // Get method symbols
        foreach (var method in methodDeclarations)
        {
            var symbol = semanticModel.GetDeclaredSymbol(method);
            if (symbol != null)
            {
                symbols.Add(symbol);
            }
        }

        // Get nested type symbols
        foreach (var nestedType in nestedTypeDeclarations)
        {
            var symbol = semanticModel.GetDeclaredSymbol(nestedType);
            if (symbol != null)
            {
                symbols.Add(symbol);
            }
        }

        return symbols;
    }

    /// <summary>
    /// Finds references using syntax-based searching when semantic model is unavailable.
    /// Looks for identifier names and invocation expressions that match extracted member names.
    /// </summary>
    /// <param name="root">The syntax tree root to search.</param>
    /// <param name="extractedSymbols">Symbols for extracted members (fields, methods, types).</param>
    /// <param name="sourceClass">The source class containing the references.</param>
    /// <returns>List of locations where extracted members are referenced.</returns>
    /// <remarks>
    /// <para><strong>IMPORTANT LIMITATION</strong>: This method only matches <see cref="IMethodSymbol"/>,
    /// <see cref="IFieldSymbol"/>, and <see cref="IPropertySymbol"/>. Type symbols (<see cref="INamedTypeSymbol"/>)
    /// are intentionally excluded to prevent incorrect transformations.</para>
    ///
    /// <para><strong>Why Type Symbols Are Excluded</strong>:</para>
    /// <list type="bullet">
    ///   <item>In simple identifier contexts, type names should NOT be qualified with the extracted class field.
    ///         Example: <c>Config _field;</c> should remain <c>Config</c>, not become <c>_extracted.Config</c></item>
    ///   <item>Type reference transformation requires different logic (qualified name updates, using directives, etc.)</item>
    ///   <item>See Issue #120 for nested type extraction limitations and planned enhancements</item>
    /// </list>
    ///
    /// <para><strong>Additional Filtering</strong>:</para>
    /// <list type="bullet">
    ///   <item>Skips method declaration identifiers (method names themselves)</item>
    ///   <item>Skips member access right-hand side (already qualified references)</item>
    ///   <item>Skips local variable declarations (prevents name collision false positives)</item>
    /// </list>
    /// </remarks>
    public List<Location> FindReferencesBySyntax(
        SyntaxNode root,
        List<ISymbol> extractedSymbols,
        ClassDeclarationSyntax sourceClass)
    {
        var references = new List<Location>();

        // Safety checks
        if (sourceClass == null || extractedSymbols == null || extractedSymbols.Count == 0)
        {
            return references;
        }

        // Include method, field, and type symbols for reference finding (Issue #120)
        // Type symbols need special handling in ReferenceTransformer (qualified names, not member access)
        var memberNames = extractedSymbols
            .Where(s => s != null && (s is IMethodSymbol || s is IFieldSymbol || s is IPropertySymbol || s is INamedTypeSymbol))
            .Select(s => s.Name)
            .ToHashSet();

        if (memberNames.Count == 0)
        {
            // No members to search for
            return references;
        }

        // Find all identifiers and invocations in the source class
        var identifiers = sourceClass.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(id => memberNames.Contains(id.Identifier.Text));

        foreach (var identifier in identifiers)
        {
            // Skip if it's the declaration itself (method name in method declaration)
            if (identifier.Parent is MethodDeclarationSyntax methodDecl &&
                methodDecl.Identifier.Text == identifier.Identifier.Text)
            {
                continue;
            }

            // Skip if it's the name part of a member access (right side of dot)
            if (identifier.Parent is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name == identifier)
            {
                continue;
            }

            // Skip if it's a local variable declaration
            // This prevents false positives when local variables have same name as extracted members
            if (identifier.Parent is VariableDeclaratorSyntax)
            {
                continue;
            }

            references.Add(identifier.GetLocation());
        }

        return references;
    }

    /// <summary>
    /// Finds all references to extracted members and categorizes them using semantic symbol comparison.
    /// Falls back to syntax-based searching when semantic model returns no results.
    /// </summary>
    public (List<Location> sameClassReferences, List<Location> externalReferences) FindAndCategorizeReferences(
        List<ISymbol> extractedSymbols,
        Compilation compilation,
        INamedTypeSymbol sourceClassSymbol,
        SyntaxNode root,
        ClassDeclarationSyntax sourceClass)
    {
        var sameClassReferences = new List<Location>();
        var externalReferences = new List<Location>();

        // Try semantic-based reference finding first
        var totalSemanticReferences = 0;
        foreach (var symbol in extractedSymbols)
        {
            var references = _symbolHelper.GetAllReferences(symbol, compilation);
            totalSemanticReferences += references.Count;

            foreach (var location in references)
            {
                // Skip non-source locations
                if (location.SourceTree == null || !location.IsInSource)
                {
                    continue;
                }

                // Get semantic model for this location's tree
                var locationSemanticModel = compilation.GetSemanticModel(location.SourceTree);

                // Get the containing type symbol at this location
                var containingTypeSymbol = locationSemanticModel.GetEnclosingSymbol(location.SourceSpan.Start)?.ContainingType;

                // Use semantic symbol comparison (handles partial classes, nested classes, etc.)
                if (containingTypeSymbol != null &&
                    SymbolEqualityComparer.Default.Equals(containingTypeSymbol, sourceClassSymbol))
                {
                    sameClassReferences.Add(location);
                }
                else
                {
                    externalReferences.Add(location);
                }
            }
        }

        // Fallback: If semantic search found no references (likely due to unresolved dependencies),
        // use syntax-based searching within the source class
        if (totalSemanticReferences == 0)
        {
            var syntaxReferences = FindReferencesBySyntax(root, extractedSymbols, sourceClass);
            sameClassReferences.AddRange(syntaxReferences);
        }

        return (sameClassReferences, externalReferences);
    }

    /// <summary>
    /// Updates references within the same class to use the new class field.
    /// </summary>
    public CompilationUnitSyntax UpdateSameClassReferences(
        CompilationUnitSyntax root,
        List<Location> sameClassReferences,
        List<ISymbol> extractedSymbols,
        string newClassFieldName,
        string newClassName,
        SemanticModel semanticModel,
        INamedTypeSymbol sourceClassSymbol)
    {
        // Create a rewriter that will update the references using location-based matching
        // This approach is more robust when code has unresolved dependencies
        var rewriter = new ReferenceTransformer(
            semanticModel,
            extractedSymbols,
            newClassFieldName,
            newClassName,
            sourceClassSymbol,
            sameClassReferences);

        return (CompilationUnitSyntax)rewriter.Visit(root);
    }

    /// <summary>
    /// Builds a warning message for external references that need manual updates.
    /// </summary>
    public string BuildExternalReferencesWarning(
        List<Location> externalReferences,
        int fieldsCount,
        int methodsCount,
        int nestedTypesCount,
        string newClassName)
    {
        var parts = new List<string>();
        if (fieldsCount > 0) parts.Add($"{fieldsCount} field(s)");
        if (methodsCount > 0) parts.Add($"{methodsCount} method(s)");
        if (nestedTypesCount > 0) parts.Add($"{nestedTypesCount} nested type(s)");

        var baseMessage = $"Extracted {string.Join(", ", parts)} into new class '{newClassName}'.";

        if (externalReferences.Any())
        {
            var referencesByFile = externalReferences
                .Where(loc => loc.SourceTree != null)
                .GroupBy(loc => System.IO.Path.GetFileName(loc.SourceTree!.FilePath))
                .Select(g => $"{g.Key} ({g.Count()} reference(s))")
                .ToList();

            if (referencesByFile.Any())
            {
                return baseMessage + " ⚠️ WARNING: Found external references that require manual updates: " +
                       string.Join(", ", referencesByFile) + ".";
            }
        }

        return baseMessage + " All references within the same class have been automatically updated.";
    }
}
