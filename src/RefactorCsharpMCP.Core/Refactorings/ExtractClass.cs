using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using RefactorCsharpMCP.Core.Utilities;
using RefactorCsharpMCP.Core.Validation;

namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Provides functionality to extract fields and methods into a new class.
/// </summary>
public class ExtractClass : RefactoringBase
{
    private readonly SymbolResolutionHelper _symbolHelper;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractClass"/> class.
    /// </summary>
    public ExtractClass()
    {
        _symbolHelper = new SymbolResolutionHelper();
    }

    /// <summary>
    /// Extracts specified fields and methods into a new class with framework-aware validation.
    /// </summary>
    /// <param name="sourceCode">The source code containing the class.</param>
    /// <param name="className">The name of the source class.</param>
    /// <param name="newClassName">The name of the new class to create.</param>
    /// <param name="fieldNames">Comma or semicolon-separated field names to extract. Optional if methodNames is provided; at least one of fieldNames or methodNames must be non-empty.</param>
    /// <param name="targetFramework">The target .NET framework (e.g., "net8.0", "net48").</param>
    /// <param name="methodNames">Comma or semicolon-separated method names to extract. Optional if fieldNames is provided; at least one of fieldNames or methodNames must be non-empty.</param>
    /// <param name="nestedTypeNames">Comma or semicolon-separated nested type names to extract. Optional.</param>
    /// <returns>A result containing the refactored code or error information.</returns>
    public async Task<RefactoringResult> ExecuteAsync(
        string sourceCode,
        string className,
        string newClassName,
        string? fieldNames,
        string targetFramework,
        string? methodNames = null,
        string? nestedTypeNames = null)
    {
        return await ExecuteWithValidationAsync(
            sourceCode,
            targetFramework,
            async () => await Task.Run(() => Execute(sourceCode, className, newClassName, fieldNames, methodNames, nestedTypeNames)));
    }

    /// <summary>
    /// Extracts specified fields and methods into a new class.
    /// </summary>
    /// <param name="sourceCode">The source code containing the class.</param>
    /// <param name="className">The name of the source class.</param>
    /// <param name="newClassName">The name of the new class to create.</param>
    /// <param name="fieldNames">Comma or semicolon-separated field names to extract. Optional if methodNames is provided; at least one of fieldNames or methodNames must be non-empty.</param>
    /// <param name="methodNames">Comma or semicolon-separated method names to extract. Optional if fieldNames is provided; at least one of fieldNames or methodNames must be non-empty.</param>
    /// <param name="nestedTypeNames">Comma or semicolon-separated nested type names to extract. Optional.</param>
    /// <returns>A result containing the refactored code or error information.</returns>
    public RefactoringResult Execute(
        string sourceCode,
        string className,
        string newClassName,
        string? fieldNames,
        string? methodNames = null,
        string? nestedTypeNames = null)
    {
        // Validate inputs
        var sourceValidation = ValidateNonEmpty(sourceCode, "Source code");
        if (!sourceValidation.IsSuccess) return sourceValidation;

        var classValidation = ValidateNonEmpty(className, "Class name");
        if (!classValidation.IsSuccess) return classValidation;

        var newClassValidation = ValidateNonEmpty(newClassName, "New class name");
        if (!newClassValidation.IsSuccess) return newClassValidation;

        // Validate that at least one of fieldNames, methodNames, or nestedTypeNames is provided
        if (string.IsNullOrWhiteSpace(fieldNames) &&
            string.IsNullOrWhiteSpace(methodNames) &&
            string.IsNullOrWhiteSpace(nestedTypeNames))
        {
            return RefactoringResult.Failure("At least one field, method, or nested type name must be specified.");
        }

        try
        {
            // Parse field, method, and nested type names
            var fieldsToExtract = string.IsNullOrWhiteSpace(fieldNames)
                ? new List<string>()
                : ParseNames(fieldNames);
            var methodsToExtract = string.IsNullOrWhiteSpace(methodNames)
                ? new List<string>()
                : ParseNames(methodNames);
            var nestedTypesToExtract = string.IsNullOrWhiteSpace(nestedTypeNames)
                ? new List<string>()
                : ParseNames(nestedTypeNames);

            // Parse and validate syntax
            var parseResult = ParseAndValidateSyntax(sourceCode, out var root, out var syntaxTree);
            if (!parseResult.IsSuccess || root == null || syntaxTree == null)
            {
                return parseResult;
            }

            // Create compilation and semantic model for symbol resolution
            var compilation = CreateCompilation(syntaxTree);
            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            // Find the class declaration
            var classDeclaration = FindClass(root, className);
            if (classDeclaration == null)
            {
                return RefactoringResult.Failure($"Class '{className}' not found in source code.");
            }

            // Validate all fields exist
            var fieldsToExtractNodes = new List<FieldDeclarationSyntax>();
            foreach (var fieldName in fieldsToExtract)
            {
                var fieldDeclaration = FindFieldDeclaration(classDeclaration, fieldName);
                if (fieldDeclaration == null)
                {
                    return RefactoringResult.Failure($"Field '{fieldName}' not found in class '{className}'.");
                }
                fieldsToExtractNodes.Add(fieldDeclaration);
            }

            // Validate all methods exist
            var methodsToExtractNodes = new List<MethodDeclarationSyntax>();
            foreach (var methodName in methodsToExtract)
            {
                var methodDeclaration = classDeclaration.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => m.Identifier.Text == methodName);

                if (methodDeclaration == null)
                {
                    return RefactoringResult.Failure($"Method '{methodName}' not found in class '{className}'.");
                }
                methodsToExtractNodes.Add(methodDeclaration);
            }

            // Validate all nested types exist
            var nestedTypesToExtractNodes = new List<BaseTypeDeclarationSyntax>();
            foreach (var typeName in nestedTypesToExtract)
            {
                var nestedType = FindNestedType(classDeclaration, typeName);
                if (nestedType == null)
                {
                    return RefactoringResult.Failure($"Nested type '{typeName}' not found in class '{className}'.");
                }
                nestedTypesToExtractNodes.Add(nestedType);
            }

            // Get symbols for extracted members BEFORE any modifications
            var extractedSymbols = GetExtractedSymbols(semanticModel, fieldsToExtractNodes, methodsToExtractNodes, nestedTypesToExtractNodes);

            // Get the source class symbol for semantic comparison
            var sourceClassSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
            if (sourceClassSymbol == null)
            {
                return RefactoringResult.Failure($"Could not resolve symbol for class '{className}'.");
            }

            // Create a field name for the new class instance
            var newClassFieldName = $"_{char.ToLower(newClassName[0])}{newClassName.Substring(1)}";

            // Find and categorize references BEFORE modifying the tree
            var (sameClassReferences, externalReferences) = FindAndCategorizeReferences(
                extractedSymbols,
                compilation,
                sourceClassSymbol);

            // Update references to use the new class field BEFORE any tree mutations
            // This preserves SyntaxTree identity for semantic analysis
            root = UpdateSameClassReferences(
                (CompilationUnitSyntax)root,
                sameClassReferences,
                extractedSymbols,
                newClassFieldName,
                newClassName,
                semanticModel,
                sourceClassSymbol);

            // NOW find the updated class in the modified root
            classDeclaration = FindClass(root, className)!;

            // Create a field in the original class for the new class instance
            var newClassField = CreateNewClassField(newClassName, newClassFieldName);

            // Add the new class field to the original class
            var membersWithField = classDeclaration.Members.Insert(0, newClassField);
            var classWithNewField = classDeclaration.WithMembers(membersWithField);

            // Replace the class in the root to get an updated root
            root = root.ReplaceNode(classDeclaration, classWithNewField);

            // Now find the updated class in the new root
            classDeclaration = FindClass(root, className)!;

            // Re-find the fields and methods in the updated class (since we can't use nodes from the old tree)
            var fieldsToRemove = new List<FieldDeclarationSyntax>();
            foreach (var fieldName in fieldsToExtract)
            {
                var field = FindFieldDeclaration(classDeclaration, fieldName);
                if (field != null)
                {
                    fieldsToRemove.Add(field);
                }
            }

            var methodsToRemove = new List<MethodDeclarationSyntax>();
            foreach (var methodName in methodsToExtract)
            {
                var method = classDeclaration.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => m.Identifier.Text == methodName);
                if (method != null)
                {
                    methodsToRemove.Add(method);
                }
            }

            // Create the new class with the ORIGINAL extracted member nodes
            var newClass = CreateNewClass(newClassName, fieldsToExtractNodes, methodsToExtractNodes, nestedTypesToExtractNodes);

            // Remove extracted members from the updated class
            var updatedClass = classDeclaration;

            // Remove fields
            foreach (var field in fieldsToRemove)
            {
                updatedClass = updatedClass.RemoveNode(field, SyntaxRemoveOptions.KeepNoTrivia);
                if (updatedClass == null)
                {
                    return RefactoringResult.Failure("Failed to remove field from original class.");
                }
            }

            // Remove methods
            foreach (var method in methodsToRemove)
            {
                updatedClass = updatedClass.RemoveNode(method, SyntaxRemoveOptions.KeepNoTrivia);
                if (updatedClass == null)
                {
                    return RefactoringResult.Failure("Failed to remove method from original class.");
                }
            }

            // Re-find and remove nested types from the updated class
            var nestedTypesToRemove = new List<BaseTypeDeclarationSyntax>();
            foreach (var typeName in nestedTypesToExtract)
            {
                var nestedType = FindNestedType(updatedClass, typeName);
                if (nestedType != null)
                {
                    nestedTypesToRemove.Add(nestedType);
                }
            }

            // Remove nested types
            foreach (var nestedType in nestedTypesToRemove)
            {
                updatedClass = updatedClass.RemoveNode(nestedType, SyntaxRemoveOptions.KeepNoTrivia);
                if (updatedClass == null)
                {
                    return RefactoringResult.Failure("Failed to remove nested type from original class.");
                }
            }

            // The class already has the new field from earlier, so just replace in root
            // Replace original class in root
            var newRoot = root.ReplaceNode(classDeclaration, updatedClass);

            // Add the new class after the original class
            var namespaceDeclaration = classDeclaration.FirstAncestorOrSelf<NamespaceDeclarationSyntax>();
            var fileScopedNamespace = classDeclaration.FirstAncestorOrSelf<FileScopedNamespaceDeclarationSyntax>();

            if (namespaceDeclaration != null)
            {
                var updatedNamespaceDecl = newRoot.DescendantNodes()
                    .OfType<NamespaceDeclarationSyntax>()
                    .FirstOrDefault(n => n.Name.ToString() == namespaceDeclaration.Name.ToString());

                if (updatedNamespaceDecl != null)
                {
                    var membersWithNewClass = updatedNamespaceDecl.Members.Add(newClass);
                    var finalNamespace = updatedNamespaceDecl.WithMembers(membersWithNewClass);
                    newRoot = newRoot.ReplaceNode(updatedNamespaceDecl, finalNamespace);
                }
            }
            else if (fileScopedNamespace != null)
            {
                var updatedFileScopedNs = newRoot.DescendantNodes()
                    .OfType<FileScopedNamespaceDeclarationSyntax>()
                    .FirstOrDefault();

                if (updatedFileScopedNs != null)
                {
                    var membersWithNewClass = updatedFileScopedNs.Members.Add(newClass);
                    var finalNamespace = updatedFileScopedNs.WithMembers(membersWithNewClass);
                    newRoot = newRoot.ReplaceNode(updatedFileScopedNs, finalNamespace);
                }
            }
            else
            {
                // No namespace - add class at compilation unit level
                var membersWithNewClass = newRoot.Members.Add(newClass);
                newRoot = newRoot.WithMembers(membersWithNewClass);
            }

            // Normalize whitespace to ensure proper formatting
            newRoot = NormalizeWhitespace(newRoot);

            // Build result message (warning only if external references exist)
            var resultMessage = BuildExternalReferencesWarning(
                externalReferences,
                fieldsToExtract.Count,
                methodsToExtract.Count,
                nestedTypesToExtract.Count,
                newClassName);

            return RefactoringResult.Success(
                newRoot.ToFullString(),
                resultMessage
            );
        }
        catch (Exception ex)
        {
            return HandleException(ex, "extract class");
        }
    }

    private List<string> ParseNames(string names)
    {
        return names
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(n => n.Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();
    }

    private FieldDeclarationSyntax? FindFieldDeclaration(ClassDeclarationSyntax classDeclaration, string fieldName)
    {
        return classDeclaration.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .FirstOrDefault(f => f.Declaration.Variables.Any(v => v.Identifier.Text == fieldName));
    }

    /// <summary>
    /// Finds a nested type declaration by name.
    /// Uses BaseTypeDeclarationSyntax for polymorphism (supports class, struct, record, enum, interface).
    /// NOTE: Does not support nested delegate types as they inherit from BaseMethodDeclarationSyntax, not BaseTypeDeclarationSyntax.
    /// </summary>
    private BaseTypeDeclarationSyntax? FindNestedType(ClassDeclarationSyntax classDeclaration, string typeName)
    {
        return classDeclaration.Members
            .OfType<BaseTypeDeclarationSyntax>()
            .FirstOrDefault(t => t.Identifier.Text == typeName);
    }

    private ClassDeclarationSyntax CreateNewClass(
        string newClassName,
        List<FieldDeclarationSyntax> fields,
        List<MethodDeclarationSyntax> methods,
        List<BaseTypeDeclarationSyntax> nestedTypes)
    {
        var members = new List<MemberDeclarationSyntax>();

        // Add fields
        members.AddRange(fields);

        // Add methods
        members.AddRange(methods);

        // Add nested types
        members.AddRange(nestedTypes);

        // Create class declaration
        var classDecl = SyntaxFactory.ClassDeclaration(newClassName)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .WithMembers(SyntaxFactory.List(members));

        return classDecl;
    }

    private FieldDeclarationSyntax CreateNewClassField(string newClassName, string fieldName)
    {
        var variableDeclaration = SyntaxFactory.VariableDeclaration(
            SyntaxFactory.IdentifierName(newClassName))
            .AddVariables(
                SyntaxFactory.VariableDeclarator(fieldName)
                    .WithInitializer(
                        SyntaxFactory.EqualsValueClause(
                            SyntaxFactory.ObjectCreationExpression(
                                SyntaxFactory.IdentifierName(newClassName))
                            .WithArgumentList(SyntaxFactory.ArgumentList()))));

        return SyntaxFactory.FieldDeclaration(variableDeclaration)
            .AddModifiers(
                SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));
    }

    /// <summary>
    /// Gets symbols for extracted field, method, and nested type declarations.
    /// </summary>
    private List<ISymbol> GetExtractedSymbols(
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
    /// Finds all references to extracted members and categorizes them using semantic symbol comparison.
    /// </summary>
    private (List<Location> sameClassReferences, List<Location> externalReferences) FindAndCategorizeReferences(
        List<ISymbol> extractedSymbols,
        Compilation compilation,
        INamedTypeSymbol sourceClassSymbol)
    {
        var sameClassReferences = new List<Location>();
        var externalReferences = new List<Location>();

        foreach (var symbol in extractedSymbols)
        {
            var references = _symbolHelper.GetAllReferences(symbol, compilation);

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

        return (sameClassReferences, externalReferences);
    }

    /// <summary>
    /// Updates references within the same class to use the new class field.
    /// </summary>
    private CompilationUnitSyntax UpdateSameClassReferences(
        CompilationUnitSyntax root,
        List<Location> sameClassReferences,
        List<ISymbol> extractedSymbols,
        string newClassFieldName,
        string newClassName,
        SemanticModel semanticModel,
        INamedTypeSymbol sourceClassSymbol)
    {
        // Create a rewriter that will update the references using semantic analysis
        var rewriter = new ReferenceUpdateRewriter(
            semanticModel,
            extractedSymbols,
            newClassFieldName,
            newClassName,
            sourceClassSymbol);

        return (CompilationUnitSyntax)rewriter.Visit(root);
    }

    /// <summary>
    /// Builds a warning message for external references that need manual updates.
    /// </summary>
    private string BuildExternalReferencesWarning(
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

    /// <summary>
    /// Syntax rewriter that updates references to extracted members using semantic analysis.
    /// </summary>
    private class ReferenceUpdateRewriter : CSharpSyntaxRewriter
    {
        private readonly SemanticModel _semanticModel;
        private readonly HashSet<ISymbol> _extractedSymbolSet;
        private readonly string _newClassFieldName;
        private readonly string _newClassName;
        private readonly INamedTypeSymbol _sourceClassSymbol;

        public ReferenceUpdateRewriter(
            SemanticModel semanticModel,
            List<ISymbol> extractedSymbols,
            string newClassFieldName,
            string newClassName,
            INamedTypeSymbol sourceClassSymbol)
        {
            _semanticModel = semanticModel;
            _extractedSymbolSet = extractedSymbols.ToHashSet(SymbolEqualityComparer.Default);
            _newClassFieldName = newClassFieldName;
            _newClassName = newClassName;
            _sourceClassSymbol = sourceClassSymbol;
        }

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

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            // Get the symbol for this identifier
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

            // Do NOT transform type symbols (INamedTypeSymbol) in identifier contexts
            // Rationale:
            // - Type references in variable declarations (e.g., 'MyType myVar') should NOT become '_field.MyType'
            // - Type references in object creation (e.g., 'new MyType()') should NOT become 'new _field.MyType()'
            // - Type references in type parameters (e.g., 'List<MyType>') should NOT be transformed
            // - Only qualified type names (OriginalClass.MyType) need transformation, handled by VisitQualifiedName
            // This prevents InvalidCastException when Roslyn tries to use member access in type contexts
            if (symbolInfo.Symbol is INamedTypeSymbol)
            {
                return base.VisitIdentifierName(node);
            }

            // Check if this identifier is already part of a member access expression
            // (e.g., _address._city or this._city) - if so, don't transform it again
            if (node.Parent is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name == node)
            {
                return base.VisitIdentifierName(node);
            }

            // Check if this identifier is within the source class (not in the extracted class)
            var containingType = _semanticModel.GetEnclosingSymbol(node.SpanStart)?.ContainingType;
            if (containingType == null ||
                !SymbolEqualityComparer.Default.Equals(containingType, _sourceClassSymbol))
            {
                return base.VisitIdentifierName(node);
            }

            // Transform: identifier -> _newClassField.identifier
            var newFieldIdentifier = SyntaxFactory.IdentifierName(_newClassFieldName);
            var memberAccessExpr = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                newFieldIdentifier,
                node);

            return memberAccessExpr.WithTriviaFrom(node);
        }

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
}
