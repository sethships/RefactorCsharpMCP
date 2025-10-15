using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCsharpMCP.Core.Validation;

namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Provides functionality to extract fields and methods into a new class.
/// </summary>
public class ExtractClass : RefactoringBase
{
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

            // Create the new class
            var newClass = CreateNewClass(newClassName, fieldsToExtractNodes, methodsToExtractNodes);

            // Create a field in the original class for the new class instance
            var newClassFieldName = $"_{char.ToLower(newClassName[0])}{newClassName.Substring(1)}";
            var newClassField = CreateNewClassField(newClassName, newClassFieldName);

            // Remove extracted members from original class
            var updatedClass = classDeclaration;

            // Remove fields
            foreach (var field in fieldsToExtractNodes)
            {
                updatedClass = updatedClass.RemoveNode(field, SyntaxRemoveOptions.KeepNoTrivia);
                if (updatedClass == null)
                {
                    return RefactoringResult.Failure("Failed to remove field from original class.");
                }
            }

            // Remove methods
            foreach (var method in methodsToExtractNodes)
            {
                updatedClass = updatedClass.RemoveNode(method, SyntaxRemoveOptions.KeepNoTrivia);
                if (updatedClass == null)
                {
                    return RefactoringResult.Failure("Failed to remove method from original class.");
                }
            }

            // Add new class field to original class
            var membersWithField = updatedClass.Members.Insert(0, newClassField);
            updatedClass = updatedClass.WithMembers(membersWithField);

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

            // Build warning message about manual updates needed
            var warningMessage = $"Extracted {fieldsToExtract.Count} field(s) and {methodsToExtract.Count} method(s) into new class '{newClassName}'. " +
                                $"⚠️ IMPORTANT: You must manually update all references to extracted members to use the new class instance '{newClassFieldName}'.";

            return RefactoringResult.Success(
                newRoot.ToFullString(),
                warningMessage
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
}
