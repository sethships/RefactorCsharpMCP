using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using RefactorCsharpMCP.Core.Validation;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Provides functionality to extract a block of code into a new method using Roslyn semantic analysis.
/// </summary>
public class ExtractMethod : RefactoringBase
{
    private readonly CodeSelectionAnalyzer _codeSelector = new CodeSelectionAnalyzer();
    private readonly ParameterExtractor _parameterExtractor;
    private readonly MethodGenerator _methodGenerator = new MethodGenerator();

    /// <summary>
    /// Initializes a new instance of ExtractMethod with required dependencies.
    /// </summary>
    public ExtractMethod() : base()
    {
        _parameterExtractor = new ParameterExtractor(new ReturnValueAnalyzer(Logger), Logger);
    }

    /// <summary>
    /// Extracts the specified lines of code into a new method with framework-aware validation.
    /// </summary>
    /// <param name="sourceCode">The source code containing the code to extract.</param>
    /// <param name="startLine">The starting line number (1-based) of the code to extract.</param>
    /// <param name="endLine">The ending line number (1-based) of the code to extract.</param>
    /// <param name="newMethodName">The name for the new extracted method.</param>
    /// <param name="targetFramework">The target framework moniker (e.g., "net8.0", "net48").</param>
    /// <returns>A result containing the refactored code or error information.</returns>
    public async Task<RefactoringResult> ExecuteAsync(
        string sourceCode,
        int startLine,
        int endLine,
        string newMethodName,
        string targetFramework)
    {
        return await ExecuteWithValidationAsync(
            sourceCode,
            targetFramework,
            async () => await Task.Run(() => Execute(sourceCode, startLine, endLine, newMethodName, targetFramework)));
    }

    /// <summary>
    /// Extracts the specified lines of code into a new method (without validation).
    /// Use ExecuteAsync() for framework-aware validation.
    /// </summary>
    /// <param name="sourceCode">The source code containing the code to extract.</param>
    /// <param name="startLine">The starting line number (1-based) of the code to extract.</param>
    /// <param name="endLine">The ending line number (1-based) of the code to extract.</param>
    /// <param name="newMethodName">The name for the new extracted method.</param>
    /// <param name="targetFramework">The target framework moniker (e.g., "net8.0", "net48"). Defaults to "net8.0".</param>
    /// <returns>A result containing the refactored code or error information.</returns>
    public RefactoringResult Execute(string sourceCode, int startLine, int endLine, string newMethodName, string targetFramework = "net8.0")
    {
        // Validate inputs
        var sourceValidation = ValidateNonEmpty(sourceCode, "Source code");
        if (!sourceValidation.IsSuccess) return sourceValidation;

        var methodValidation = ValidateNonEmpty(newMethodName, "Method name");
        if (!methodValidation.IsSuccess) return methodValidation;

        // Validate method name format using shared compiled regex
        // Note: Validation also performed in ExtractMethodTool, this is defense-in-depth
        if (!McpToolConstants.CSharpIdentifierRegex.IsMatch(newMethodName))
        {
            return RefactoringResult.Failure(ErrorCode.REFACTORING_FAILED, "Method name must be a valid C# identifier.");
        }

        if (startLine < 1 || endLine < startLine)
        {
            return RefactoringResult.Failure(ErrorCode.INVALID_LINE_RANGE, $"Invalid line range: {startLine}-{endLine}");
        }

        // Validate framework support early (before expensive semantic analysis) - CR Issue #2
        if (string.IsNullOrWhiteSpace(targetFramework))
        {
            return RefactoringResult.Failure(
                ErrorCode.MISSING_PARAMETER,
                $"Target framework cannot be null or empty. " +
                $"Supported frameworks: {string.Join(", ", Infrastructure.FrameworkSupport.FrameworkMoniker.SupportedFrameworks)}");
        }

        if (!Infrastructure.FrameworkSupport.FrameworkMoniker.IsSupported(targetFramework))
        {
            var normalized = Infrastructure.FrameworkSupport.FrameworkMoniker.Normalize(targetFramework);
            if (Infrastructure.FrameworkSupport.FrameworkMoniker.IsEndOfLife(normalized))
            {
                var suggestion = Infrastructure.FrameworkSupport.FrameworkMoniker.SuggestAlternative(normalized);
                return RefactoringResult.Failure(
                    ErrorCode.FRAMEWORK_SYNTAX_MISMATCH,
                    $"Target framework '{targetFramework}' is end-of-life and not supported. " +
                    $"Consider using '{suggestion ?? "net8.0"}' instead.");
            }
            return RefactoringResult.Failure(
                ErrorCode.FRAMEWORK_SYNTAX_MISMATCH,
                $"Target framework '{targetFramework}' is not supported. " +
                $"Supported frameworks: {string.Join(", ", Infrastructure.FrameworkSupport.FrameworkMoniker.SupportedFrameworks)}");
        }

        try
        {
            // Parse and validate syntax
            var parseResult = ParseAndValidateSyntax(sourceCode, out var root, out var syntaxTree);
            if (!parseResult.IsSuccess || root == null || syntaxTree == null)
            {
                return parseResult;
            }

            // Find the method containing the lines to extract
            var containingMethod = _codeSelector.FindContainingMethod(root, startLine, endLine);
            if (containingMethod == null)
            {
                return RefactoringResult.Failure(ErrorCode.REFACTORING_FAILED, $"No method found containing lines {startLine}-{endLine}.");
            }

            // Find statements to extract based on line range
            var statementsToExtract = _codeSelector.FindStatementsInLineRange(containingMethod, startLine, endLine);
            if (!statementsToExtract.Any())
            {
                return RefactoringResult.Failure(ErrorCode.NO_STATEMENTS_FOUND, $"No statements found in line range {startLine}-{endLine}.");
            }

            // Create compilation for semantic analysis
            var compilation = CreateCompilation(syntaxTree);
            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            // Analyze data flow for the selected statements
            var dataFlowAnalysis = _parameterExtractor.AnalyzeDataFlow(semanticModel, statementsToExtract, containingMethod);

            // Validate return info was successfully analyzed (CR Issue #3)
            if (dataFlowAnalysis.ReturnInfo == null)
            {
                return RefactoringResult.Failure(
                    ErrorCode.REFACTORING_FAILED,
                    "Failed to analyze return type for extracted method. " +
                    "The code may contain unsupported patterns.");
            }

            // Check for return type analysis errors (Issue #52, CR Issue #1)
            if (dataFlowAnalysis.ReturnInfo?.Kind == ReturnKind.Error)
            {
                if (string.IsNullOrEmpty(dataFlowAnalysis.ReturnInfo.ErrorMessage))
                {
                    return RefactoringResult.Failure(ErrorCode.REFACTORING_FAILED, "Return type analysis failed with unknown error.");
                }
                return RefactoringResult.Failure(ErrorCode.REFACTORING_FAILED, dataFlowAnalysis.ReturnInfo.ErrorMessage);
            }

            // Validate framework compatibility for return type (Issue #51, CR Issue #5)
            var languageVersion = Infrastructure.FrameworkSupport.FrameworkMoniker.GetLanguageVersion(targetFramework);
            if (dataFlowAnalysis.ReturnInfo?.Kind == ReturnKind.Multiple && languageVersion < LanguageVersion.CSharp7)
            {
                return RefactoringResult.Failure(
                    ErrorCode.FRAMEWORK_SYNTAX_MISMATCH,
                    $"Multiple return values detected but tuple syntax requires C# 7.0+. " +
                    $"Target framework '{targetFramework}' supports {languageVersion}. " +
                    $"Consider upgrading to .NET 8 (recommended), .NET Framework 4.7+, or .NET Standard 2.0+.");
            }

            // Build the new extracted method
            var extractedMethod = _methodGenerator.BuildExtractedMethod(
                newMethodName,
                statementsToExtract,
                dataFlowAnalysis,
                containingMethod,
                targetFramework
            );

            // Build the method call to replace the extracted statements
            var methodCall = _methodGenerator.BuildMethodCall(newMethodName, dataFlowAnalysis.Parameters, dataFlowAnalysis.ReturnInfo);

            // Find the containing class to insert the new method
            var containingClass = containingMethod.FirstAncestorOrSelf<ClassDeclarationSyntax>();
            if (containingClass == null)
            {
                return RefactoringResult.Failure(ErrorCode.REFACTORING_FAILED, "Could not find containing class.");
            }

            // Replace statements with method call and add extracted method to class
            var updatedMethod = _methodGenerator.ReplaceStatementsWithMethodCall(containingMethod, statementsToExtract, methodCall);
            var updatedClass = containingClass.ReplaceNode(containingMethod, updatedMethod);

            // Find the position of the original method by name
            var methodIndex = updatedClass.Members
                .Select((member, index) => new { member, index })
                .FirstOrDefault(x => x.member is MethodDeclarationSyntax method &&
                                     method.Identifier.Text == containingMethod.Identifier.Text)?.index ?? 0;

            // Add the extracted method after the updated method
            updatedClass = updatedClass.WithMembers(
                updatedClass.Members.Insert(methodIndex + 1, extractedMethod)
            );

            // Replace the class in the root
            var newRoot = root.ReplaceNode(containingClass, updatedClass);

            // Normalize whitespace to ensure proper formatting
            newRoot = NormalizeWhitespace(newRoot);

            return RefactoringResult.Success(
                newRoot.ToFullString(),
                $"Extracted method '{newMethodName}' from lines {startLine}-{endLine}."
            );
        }
        catch (Exception ex)
        {
            return HandleException(ex, "extract method");
        }
    }
}
