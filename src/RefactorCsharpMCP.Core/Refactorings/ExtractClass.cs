using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using RefactorCsharpMCP.Core.Refactorings.ExtractClassComponents;
using RefactorCsharpMCP.Core.Refactorings.ExtractClassComponents.Strategies;
using RefactorCsharpMCP.Core.Utilities;
using RefactorCsharpMCP.Core.Validation;

namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Provides functionality to extract fields and methods into a new class.
/// </summary>
public class ExtractClass : RefactoringBase
{
    private readonly MemberSelector _memberSelector = new MemberSelector();
    private readonly ReferenceUpdater _referenceUpdater = new ReferenceUpdater();
    private readonly SymbolResolutionHelper _symbolHelper;
    private readonly ExtractClassOrchestrator _orchestrator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractClass"/> class.
    /// </summary>
    public ExtractClass()
    {
        _symbolHelper = new SymbolResolutionHelper();
        _orchestrator = new ExtractClassOrchestrator(_memberSelector, _referenceUpdater, _symbolHelper);
    }

    /// <summary>
    /// Extracts specified fields and methods into a new class with optional framework-aware compilation validation.
    /// </summary>
    /// <param name="sourceCode">The source code containing the class.</param>
    /// <param name="className">The name of the source class.</param>
    /// <param name="newClassName">The name of the new class to create.</param>
    /// <param name="fieldNames">Comma or semicolon-separated field names to extract. Optional if methodNames is provided; at least one of fieldNames or methodNames must be non-empty.</param>
    /// <param name="targetFramework">The target .NET framework (e.g., "net8.0", "net48", "netstandard2.0"). Used for compilation validation when enabled.</param>
    /// <param name="validateCompilation">Enable full compilation validation with framework-specific reference assemblies. Default: true. When enabled, validates that extracted code compiles successfully with complete BCL references for the target framework.</param>
    /// <param name="methodNames">Comma or semicolon-separated method names to extract. Optional if fieldNames is provided; at least one of fieldNames or methodNames must be non-empty.</param>
    /// <param name="nestedTypeNames">Comma or semicolon-separated nested type names to extract. Optional.</param>
    /// <param name="additionalReferences">Optional metadata references for custom assemblies not in the BCL. Reserved for future use.</param>
    /// <returns>A result containing the refactored code or error information.</returns>
    /// <remarks>
    /// <para><strong>Validation Behavior:</strong></para>
    /// <list type="bullet">
    ///   <item>When <paramref name="validateCompilation"/> is true (default): Performs comprehensive semantic validation using framework-specific BCL references. Catches type resolution errors, missing assemblies, and semantic issues.</item>
    ///   <item>When <paramref name="validateCompilation"/> is false: Performs syntax validation only. Faster but may miss semantic errors.</item>
    /// </list>
    /// <para><strong>Backward Compatibility:</strong></para>
    /// <para>
    /// The synchronous <see cref="Execute"/> method remains unchanged and performs syntax validation only.
    /// Use this async variant when you need comprehensive compilation validation for production code.
    /// </para>
    /// </remarks>
    public async Task<RefactoringResult> ExecuteAsync(
        string sourceCode,
        string className,
        string newClassName,
        string? fieldNames,
        string targetFramework = "net8.0",
        bool validateCompilation = true,
        string? methodNames = null,
        string? nestedTypeNames = null,
        IEnumerable<MetadataReference>? additionalReferences = null)
    {
        // Perform the refactoring operation synchronously
        var refactoringResult = Execute(sourceCode, className, newClassName, fieldNames, methodNames, nestedTypeNames);

        // If refactoring failed or validation is disabled, return immediately
        if (!refactoringResult.IsSuccess || !validateCompilation)
        {
            return refactoringResult;
        }

        // Perform framework-aware compilation validation on the refactored code
        var validationResult = await ValidateCompilationWithFrameworkAsync(
            refactoringResult.RefactoredCode!,
            targetFramework,
            additionalReferences);

        // If validation failed, return the validation failure
        if (!validationResult.IsSuccess)
        {
            return validationResult;
        }

        // Validation succeeded - update the success message to indicate validation passed
        return RefactoringResult.Success(
            refactoringResult.RefactoredCode!,
            $"{refactoringResult.Message} Compilation validation passed for framework {targetFramework}.");
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
            return RefactoringResult.Failure(ErrorCode.MISSING_PARAMETER, "At least one field, method, or nested type name must be specified.");
        }

        try
        {
            // Parse field, method, and nested type names
            var fieldsToExtract = string.IsNullOrWhiteSpace(fieldNames)
                ? new List<string>()
                : _memberSelector.ParseNames(fieldNames);
            var methodsToExtract = string.IsNullOrWhiteSpace(methodNames)
                ? new List<string>()
                : _memberSelector.ParseNames(methodNames);
            var nestedTypesToExtract = string.IsNullOrWhiteSpace(nestedTypeNames)
                ? new List<string>()
                : _memberSelector.ParseNames(nestedTypeNames);

            // Parse and validate syntax
            var parseResult = ParseAndValidateSyntax(sourceCode, out var root, out var syntaxTree);
            if (!parseResult.IsSuccess || root == null || syntaxTree == null)
            {
                return parseResult;
            }

            // Create compilation for semantic analysis
            var compilation = CreateCompilation(syntaxTree);

            // NOTE: This synchronous Execute() method performs syntax validation only.
            // For comprehensive compilation validation with framework-specific BCL references,
            // use ExecuteAsync() with validateCompilation = true (default).
            // See ExecuteAsync() method documentation for details on validation options.

            // Delegate to orchestrator for core extraction logic
            return _orchestrator.ExecuteExtraction(
                root,
                syntaxTree,
                compilation,
                className,
                newClassName,
                fieldsToExtract,
                methodsToExtract,
                nestedTypesToExtract,
                node => NormalizeWhitespace(node));
        }
        catch (Exception ex)
        {
            return HandleException(ex, "extract class");
        }
    }
}
