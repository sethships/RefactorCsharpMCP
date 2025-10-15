using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCsharpMCP.Core.Validation;

namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Provides functionality to extract fields and methods into a new class.
/// </summary>
public class ExtractClass
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
        // Step 1: Validate input code against target framework
        using var validator = new SyntaxValidator();
        var inputValidation = await validator.ValidateInputAsync(sourceCode, targetFramework);

        if (!inputValidation.IsValid)
        {
            return RefactoringResult.ValidationFailure(inputValidation);
        }

        // Step 2: Perform refactoring (delegate to existing logic)
        var refactoringResult = Execute(sourceCode, className, newClassName, fieldNames, methodNames);

        if (!refactoringResult.IsSuccess)
        {
            return refactoringResult;
        }

        // Step 3: Validate output code against target framework
        var outputValidation = await validator.ValidateOutputAsync(refactoringResult.RefactoredCode!, targetFramework);

        if (!outputValidation.IsValid)
        {
            return RefactoringResult.ValidationFailure(outputValidation);
        }

        return refactoringResult;
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
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return RefactoringResult.Failure("Source code cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(className))
        {
            return RefactoringResult.Failure("Class name cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(newClassName))
        {
            return RefactoringResult.Failure("New class name cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(fieldNames))
        {
            return RefactoringResult.Failure("Field names cannot be empty.");
        }

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

            // Parse the source code into a syntax tree
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = (CompilationUnitSyntax)syntaxTree.GetRoot();

            // Check for parse errors
            var diagnostics = syntaxTree.GetDiagnostics();
            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            if (errors.Any())
            {
                var errorMessages = string.Join(", ", errors.Select(e => e.GetMessage()).Take(3));
                return RefactoringResult.Failure($"Syntax errors in source code: {errorMessages}");
            }

            // Find the class declaration
            var classDeclaration = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.Text == className);

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
            newRoot = newRoot.NormalizeWhitespace();

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
            // Sanitize exception message for security
            var errorCategory = ex switch
            {
                ArgumentException => "InvalidInput",
                InvalidOperationException => "InvalidState",
                FormatException => "ParseError",
                _ => "UnexpectedError"
            };
            return RefactoringResult.Failure($"An error occurred during the refactoring ({errorCategory}). Please check the code syntax and try again.");
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
