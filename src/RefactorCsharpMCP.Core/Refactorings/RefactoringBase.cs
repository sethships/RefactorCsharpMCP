using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCsharpMCP.Core.Validation;

namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Base class for all refactoring operations, providing common infrastructure and utilities.
/// Reduces boilerplate by centralizing validation, error handling, and Roslyn operations.
/// </summary>
public abstract class RefactoringBase
{
    /// <summary>
    /// Validates that a string parameter is not null or whitespace.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="parameterName">The parameter name for error messages.</param>
    /// <returns>A RefactoringResult indicating success or failure.</returns>
    protected RefactoringResult ValidateNonEmpty(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return RefactoringResult.Failure($"{parameterName} cannot be empty.");
        }
        // Return validation success without refactored code (validation helper, not actual refactoring)
        return new RefactoringResult
        {
            IsSuccess = true,
            RefactoredCode = null,
            Message = "Validation passed"
        };
    }

    /// <summary>
    /// Parses source code into a syntax tree and checks for syntax errors.
    /// </summary>
    /// <param name="sourceCode">The source code to parse.</param>
    /// <param name="root">The parsed compilation unit root (output parameter).</param>
    /// <param name="syntaxTree">The parsed syntax tree (output parameter).</param>
    /// <returns>A RefactoringResult indicating success or failure. If failed, contains error details.</returns>
    protected RefactoringResult ParseAndValidateSyntax(
        string sourceCode,
        out CompilationUnitSyntax? root,
        out SyntaxTree? syntaxTree)
    {
        root = null;
        syntaxTree = null;

        try
        {
            syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            root = (CompilationUnitSyntax)syntaxTree.GetRoot();

            // Check for parse errors
            var diagnostics = syntaxTree.GetDiagnostics();
            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            if (errors.Any())
            {
                var errorMessages = string.Join(", ", errors.Select(e => e.GetMessage()).Take(3));
                return RefactoringResult.Failure($"Syntax errors in source code: {errorMessages}");
            }

            // Return validation success without refactored code (validation helper, not actual refactoring)
            return new RefactoringResult
            {
                IsSuccess = true,
                RefactoredCode = null,
                Message = "Syntax validation passed"
            };
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    /// <summary>
    /// Creates a compilation for semantic analysis with common assembly references.
    /// </summary>
    /// <param name="syntaxTree">The syntax tree to include in the compilation.</param>
    /// <returns>A CSharpCompilation instance configured with standard references.</returns>
    protected CSharpCompilation CreateCompilation(SyntaxTree syntaxTree)
    {
        return CSharpCompilation.Create("temp")
            .AddReferences(
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location), // mscorlib/System.Private.CoreLib
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location), // System.Collections
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location) // System.Linq
            )
            .AddSyntaxTrees(syntaxTree);
    }

    /// <summary>
    /// Finds a class declaration by name in the syntax root.
    /// </summary>
    /// <param name="root">The compilation unit root to search.</param>
    /// <param name="className">The name of the class to find.</param>
    /// <returns>The class declaration if found; otherwise, null.</returns>
    protected ClassDeclarationSyntax? FindClass(CompilationUnitSyntax root, string className)
    {
        return root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == className);
    }

    /// <summary>
    /// Finds a method declaration by name within a class.
    /// </summary>
    /// <param name="classDeclaration">The class to search.</param>
    /// <param name="methodName">The name of the method to find.</param>
    /// <returns>The method declaration if found; otherwise, null.</returns>
    protected MethodDeclarationSyntax? FindMethod(ClassDeclarationSyntax classDeclaration, string methodName)
    {
        return classDeclaration.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == methodName);
    }

    /// <summary>
    /// Handles exceptions by categorizing and sanitizing error messages for security.
    /// </summary>
    /// <param name="ex">The exception to handle.</param>
    /// <param name="operationName">The name of the operation (for error messages).</param>
    /// <returns>A RefactoringResult with a sanitized error message.</returns>
    protected RefactoringResult HandleException(Exception ex, string operationName = "refactoring")
    {
        // Sanitize exception message for security
        var errorCategory = ex switch
        {
            ArgumentException => "InvalidInput",
            InvalidOperationException => "InvalidState",
            FormatException => "ParseError",
            _ => "UnexpectedError"
        };
        return RefactoringResult.Failure($"An error occurred during {operationName} ({errorCategory}). Please check the code syntax and try again.");
    }

    /// <summary>
    /// Performs framework-aware validation wrapping for a refactoring operation.
    /// This is a template method that handles input validation, executes the refactoring, and validates output.
    /// </summary>
    /// <param name="sourceCode">The source code to refactor.</param>
    /// <param name="targetFramework">The target framework moniker (e.g., "net8.0", "net48").</param>
    /// <param name="refactoringOperation">The core refactoring operation to execute.</param>
    /// <returns>A RefactoringResult with validation details.</returns>
    protected async Task<RefactoringResult> ExecuteWithValidationAsync(
        string sourceCode,
        string targetFramework,
        Func<Task<RefactoringResult>> refactoringOperation)
    {
        // Step 1: Validate input code against target framework
        var validator = new SyntaxValidator();
        try
        {
            var inputValidation = await validator.ValidateInputAsync(sourceCode, targetFramework);

            if (!inputValidation.IsValid)
            {
                return RefactoringResult.ValidationFailure(inputValidation);
            }

            // Step 2: Perform refactoring (delegate to provided operation)
            var refactoringResult = await refactoringOperation();

            if (!refactoringResult.IsSuccess)
            {
                return refactoringResult;
            }

            // Step 3: Validate refactored code is not empty
            if (string.IsNullOrWhiteSpace(refactoringResult.RefactoredCode))
            {
                return RefactoringResult.Failure("Refactoring succeeded but produced no output code.");
            }

            // Step 4: Validate output code against target framework
            var outputValidation = await validator.ValidateOutputAsync(refactoringResult.RefactoredCode, targetFramework);

            if (!outputValidation.IsValid)
            {
                return RefactoringResult.ValidationFailure(outputValidation);
            }

            return refactoringResult;
        }
        finally
        {
            validator.Dispose();
        }
    }

    /// <summary>
    /// Normalizes whitespace in a syntax node to ensure proper formatting.
    /// </summary>
    /// <param name="node">The syntax node to normalize.</param>
    /// <returns>The normalized syntax node.</returns>
    protected T NormalizeWhitespace<T>(T node) where T : SyntaxNode
    {
        return node.NormalizeWhitespace();
    }
}
