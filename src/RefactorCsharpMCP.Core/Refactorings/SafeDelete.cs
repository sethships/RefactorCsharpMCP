using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using RefactorCsharpMCP.Core.Validation;

namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Provides functionality to safely delete code elements with dependency analysis.
/// LIMITATION: Only analyzes references within the same source file. Cross-file references are not detected.
/// </summary>
public class SafeDelete
{
    /// <summary>
    /// Safely deletes a method if it has no references within the same file, with framework-aware validation.
    /// LIMITATION: Only checks references in the provided source code. Does not analyze cross-file references.
    /// </summary>
    /// <param name="sourceCode">The source code containing the method.</param>
    /// <param name="className">The name of the class containing the method.</param>
    /// <param name="methodName">The name of the method to delete.</param>
    /// <param name="targetFramework">The target .NET framework (e.g., "net8.0", "net48").</param>
    /// <returns>A result containing the refactored code or error information.</returns>
    public async Task<RefactoringResult> ExecuteAsync(
        string sourceCode,
        string className,
        string methodName,
        string targetFramework)
    {
        // Step 1: Validate input code against target framework
        var validator = new SyntaxValidator();
        var inputValidation = await validator.ValidateInputAsync(sourceCode, targetFramework);

        if (!inputValidation.IsValid)
        {
            return RefactoringResult.ValidationFailure(inputValidation);
        }

        // Step 2: Perform refactoring (delegate to existing logic)
        var refactoringResult = Execute(sourceCode, className, methodName);

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
    /// Safely deletes a method if it has no references within the same file.
    /// LIMITATION: Only checks references in the provided source code. Does not analyze cross-file references.
    /// </summary>
    /// <param name="sourceCode">The source code containing the method.</param>
    /// <param name="className">The name of the class containing the method.</param>
    /// <param name="methodName">The name of the method to delete.</param>
    /// <returns>A result containing the refactored code or error information.</returns>
    public RefactoringResult Execute(string sourceCode, string className, string methodName)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return RefactoringResult.Failure("Source code cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(className))
        {
            return RefactoringResult.Failure("Class name cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(methodName))
        {
            return RefactoringResult.Failure("Method name cannot be empty.");
        }

        try
        {
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

            // Find the method declaration
            var methodDeclaration = classDeclaration.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.Text == methodName);

            if (methodDeclaration == null)
            {
                return RefactoringResult.Failure($"Method '{methodName}' not found in class '{className}'.");
            }

            // Create compilation for semantic analysis
            var compilation = CSharpCompilation.Create("temp")
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddSyntaxTrees(syntaxTree);

            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            // Check for references to this method
            var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration);
            if (methodSymbol == null)
            {
                return RefactoringResult.Failure("Unable to analyze method symbol.");
            }

            // Find all references to the method within the same file
            var references = FindMethodReferences(root, methodName, methodDeclaration);

            if (references.Any())
            {
                var referenceLocations = string.Join(", ", references.Select(r => $"line {r.GetLocation().GetLineSpan().StartLinePosition.Line + 1}"));
                return RefactoringResult.Failure($"Method '{methodName}' is referenced at: {referenceLocations}. Cannot safely delete.");
            }

            // Remove the method
            var updatedClass = classDeclaration.RemoveNode(methodDeclaration, SyntaxRemoveOptions.KeepNoTrivia);
            if (updatedClass == null)
            {
                return RefactoringResult.Failure("Failed to remove method from class.");
            }

            // Replace class in root
            var newRoot = root.ReplaceNode(classDeclaration, updatedClass);

            // Normalize whitespace to ensure proper formatting
            newRoot = newRoot.NormalizeWhitespace();

            return RefactoringResult.Success(
                newRoot.ToFullString(),
                $"Safely deleted method '{methodName}' from class '{className}'."
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

    private List<SyntaxNode> FindMethodReferences(SyntaxNode root, string methodName, MethodDeclarationSyntax methodToDelete)
    {
        var references = new List<SyntaxNode>();

        // Find all invocations of this method
        var invocations = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(i => IsInvocationOfMethod(i, methodName))
            .ToList();

        foreach (var invocation in invocations)
        {
            // Exclude invocations within the method itself (recursive calls don't count as external references)
            if (!invocation.Ancestors().Contains(methodToDelete))
            {
                references.Add(invocation);
            }
        }

        return references;
    }

    private bool IsInvocationOfMethod(InvocationExpressionSyntax invocation, string methodName)
    {
        // Check for direct method call: MethodName()
        if (invocation.Expression is IdentifierNameSyntax identifierName)
        {
            return identifierName.Identifier.Text == methodName;
        }

        // Check for this.MethodName() or object.MethodName()
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            if (memberAccess.Name is IdentifierNameSyntax memberName)
            {
                return memberName.Identifier.Text == methodName;
            }
        }

        return false;
    }
}
