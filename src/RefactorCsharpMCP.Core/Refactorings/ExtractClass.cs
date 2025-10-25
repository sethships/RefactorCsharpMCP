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
    /// <param name="fieldNames">Comma or semicolon-separated field names to extract.</param>
    /// <param name="targetFramework">The target .NET framework (e.g., "net8.0", "net48").</param>
    /// <param name="methodNames">Comma or semicolon-separated method names to extract (optional).</param>
    /// <returns>A result containing the refactored code or error information.</returns>
    public async Task<RefactoringResult> ExecuteAsync(
        string sourceCode,
        string className,
        string newClassName,
        string fieldNames,
        string targetFramework,
        string? methodNames = null)
    {
        return await ExecuteWithValidationAsync(
            sourceCode,
            targetFramework,
            async () => await Task.Run(() => Execute(sourceCode, className, newClassName, fieldNames, methodNames)));
    }

    /// <summary>
    /// Extracts specified fields and methods into a new class.
    /// </summary>
    /// <param name="sourceCode">The source code containing the class.</param>
    /// <param name="className">The name of the source class.</param>
    /// <param name="newClassName">The name of the new class to create.</param>
    /// <param name="fieldNames">Comma or semicolon-separated field names to extract.</param>
    /// <param name="methodNames">Comma or semicolon-separated method names to extract (optional).</param>
    /// <returns>A result containing the refactored code or error information.</returns>
    public RefactoringResult Execute(
        string sourceCode,
        string className,
        string newClassName,
        string fieldNames,
        string? methodNames = null)
    {
        // Validate inputs
        var sourceValidation = ValidateNonEmpty(sourceCode, "Source code");
        if (!sourceValidation.IsSuccess) return sourceValidation;

        var classValidation = ValidateNonEmpty(className, "Class name");
        if (!classValidation.IsSuccess) return classValidation;

        var newClassValidation = ValidateNonEmpty(newClassName, "New class name");
        if (!newClassValidation.IsSuccess) return newClassValidation;

        var fieldValidation = ValidateNonEmpty(fieldNames, "Field names");
        if (!fieldValidation.IsSuccess) return fieldValidation;

        try
        {
            // Parse field and method names
            var fieldsToExtract = ParseNames(fieldNames);
            var methodsToExtract = string.IsNullOrWhiteSpace(methodNames)
                ? new List<string>()
                : ParseNames(methodNames);

            if (!fieldsToExtract.Any())
            {
                return RefactoringResult.Failure("At least one field name must be specified.");
            }

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

            // Get symbols for extracted members BEFORE any modifications
            var extractedSymbols = GetExtractedSymbols(semanticModel, fieldsToExtractNodes, methodsToExtractNodes);

            // Create a field name for the new class instance
            var newClassFieldName = $"_{char.ToLower(newClassName[0])}{newClassName.Substring(1)}";

            // Find and categorize references BEFORE modifying the tree
            var (sameClassReferences, externalReferences) = FindAndCategorizeReferences(
                extractedSymbols,
                compilation,
                classDeclaration);

            // Create a field in the original class for the new class instance
            var newClassField = CreateNewClassField(newClassName, newClassFieldName);

            // Add the new class field to the original class FIRST
            var membersWithField = classDeclaration.Members.Insert(0, newClassField);
            var classWithNewField = classDeclaration.WithMembers(membersWithField);

            // Replace the class in the root to get an updated root
            root = root.ReplaceNode(classDeclaration, classWithNewField);

            // Update references to use the new class field (before removing members)
            root = UpdateSameClassReferences(
                (CompilationUnitSyntax)root,
                sameClassReferences,
                extractedSymbols,
                newClassFieldName,
                compilation,
                classDeclaration);

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
            var newClass = CreateNewClass(newClassName, fieldsToExtractNodes, methodsToExtractNodes);

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

    private ClassDeclarationSyntax CreateNewClass(
        string newClassName,
        List<FieldDeclarationSyntax> fields,
        List<MethodDeclarationSyntax> methods)
    {
        var members = new List<MemberDeclarationSyntax>();

        // Add fields
        members.AddRange(fields);

        // Add methods
        members.AddRange(methods);

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
    /// Gets symbols for extracted field and method declarations.
    /// </summary>
    private List<ISymbol> GetExtractedSymbols(
        SemanticModel semanticModel,
        List<FieldDeclarationSyntax> fieldDeclarations,
        List<MethodDeclarationSyntax> methodDeclarations)
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

        return symbols;
    }

    /// <summary>
    /// Finds all references to extracted members and categorizes them.
    /// </summary>
    private (List<Location> sameClassReferences, List<Location> externalReferences) FindAndCategorizeReferences(
        List<ISymbol> extractedSymbols,
        Compilation compilation,
        ClassDeclarationSyntax sourceClass)
    {
        var sameClassReferences = new List<Location>();
        var externalReferences = new List<Location>();

        foreach (var symbol in extractedSymbols)
        {
            var references = _symbolHelper.GetAllReferences(symbol, compilation);

            foreach (var location in references)
            {
                // Check if reference is within the source class
                var node = location.SourceTree?.GetRoot().FindNode(location.SourceSpan);
                var containingClass = node?.FirstAncestorOrSelf<ClassDeclarationSyntax>();

                if (containingClass != null && containingClass.Identifier.Text == sourceClass.Identifier.Text)
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
        Compilation compilation,
        ClassDeclarationSyntax sourceClass)
    {
        if (!sameClassReferences.Any() && extractedSymbols.Any())
        {
            // Even if no references found by symbol analysis, still try to update by name
            // This handles cases where the tree structure has changed
        }

        // Create a rewriter that will update the references
        var rewriter = new ReferenceUpdateRewriter(
            sameClassReferences,
            extractedSymbols,
            newClassFieldName,
            compilation,
            sourceClass);

        return (CompilationUnitSyntax)rewriter.Visit(root);
    }

    /// <summary>
    /// Builds a warning message for external references that need manual updates.
    /// </summary>
    private string BuildExternalReferencesWarning(
        List<Location> externalReferences,
        int fieldsCount,
        int methodsCount,
        string newClassName)
    {
        var baseMessage = $"Extracted {fieldsCount} field(s) and {methodsCount} method(s) into new class '{newClassName}'.";

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
    /// Syntax rewriter that updates references to extracted members.
    /// </summary>
    private class ReferenceUpdateRewriter : CSharpSyntaxRewriter
    {
        private readonly HashSet<string> _extractedSymbolNames;
        private readonly string _newClassFieldName;
        private readonly ClassDeclarationSyntax _sourceClass;

        public ReferenceUpdateRewriter(
            List<Location> sameClassReferences,
            List<ISymbol> extractedSymbols,
            string newClassFieldName,
            Compilation compilation,
            ClassDeclarationSyntax sourceClass)
        {
            _extractedSymbolNames = extractedSymbols.Select(s => s.Name).ToHashSet();
            _newClassFieldName = newClassFieldName;
            _sourceClass = sourceClass;
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            // Only process identifiers with names matching extracted symbols
            if (!_extractedSymbolNames.Contains(node.Identifier.Text))
            {
                return base.VisitIdentifierName(node);
            }

            // Check if this identifier is already part of a member access expression
            // (e.g., _address._city) - if so, don't transform it again
            if (node.Parent is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name == node)
            {
                return base.VisitIdentifierName(node);
            }

            // Check if this identifier is within the source class (not in the extracted class)
            var containingClass = node.FirstAncestorOrSelf<ClassDeclarationSyntax>();
            if (containingClass == null || containingClass.Identifier.Text != _sourceClass.Identifier.Text)
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
    }
}
