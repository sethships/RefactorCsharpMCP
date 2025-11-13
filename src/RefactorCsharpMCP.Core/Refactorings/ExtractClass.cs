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

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractClass"/> class.
    /// </summary>
    public ExtractClass()
    {
        _symbolHelper = new SymbolResolutionHelper();
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
            return RefactoringResult.Failure("At least one field, method, or nested type name must be specified.");
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

            // Create compilation and semantic model for symbol resolution
            var compilation = CreateCompilation(syntaxTree);
            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            // Find the class declaration
            var classDeclaration = FindClass(root, className);
            if (classDeclaration == null)
            {
                return RefactoringResult.Failure($"Class '{className}' not found in source code.");
            }

            // Validate and find all members to extract
            var memberValidation = _memberSelector.ValidateAndFindMembers(
                classDeclaration,
                className,
                fieldsToExtract,
                methodsToExtract,
                nestedTypesToExtract,
                out var fieldsToExtractNodes,
                out var methodsToExtractNodes,
                out var nestedTypesToExtractNodes);

            if (memberValidation != null) // null = success, non-null = failure
            {
                return memberValidation;
            }

            // Get symbols for extracted members BEFORE any modifications
            var extractedSymbols = _referenceUpdater.GetExtractedSymbols(semanticModel, fieldsToExtractNodes, methodsToExtractNodes, nestedTypesToExtractNodes);

            // Get the source class symbol for semantic comparison
            var sourceClassSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
            if (sourceClassSymbol == null)
            {
                return RefactoringResult.Failure($"Could not resolve symbol for class '{className}'.");
            }

            // Create a field name for the new class instance
            var newClassFieldName = $"_{char.ToLower(newClassName[0])}{newClassName.Substring(1)}";

            // Find and categorize references BEFORE modifying the tree
            var (sameClassReferences, externalReferences) = _referenceUpdater.FindAndCategorizeReferences(
                extractedSymbols,
                compilation,
                sourceClassSymbol,
                root,
                classDeclaration);

            // Update references to use the new class field BEFORE any tree mutations
            // This preserves SyntaxTree identity for semantic analysis
            root = _referenceUpdater.UpdateSameClassReferences(
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
            // This will be added by the transformer in a single pass
            var newClassField = CompositionFieldGenerator.CreateCompositionField(newClassName, newClassFieldName);

            // Re-find members in the updated class (fresh nodes from mutated tree)
            var (fieldsToRemove, methodsToRemove) = _memberSelector.RefindMembersInUpdatedClass(
                classDeclaration,
                fieldsToExtract,
                methodsToExtract);

            // Create extraction context and select strategy
            var context = new ExtractionContext(
                classDeclaration,
                newClassName,
                fieldsToExtract,
                methodsToExtract,
                semanticModel,
                ExtractionMode.Default);

            var strategyFactory = new ExtractionStrategyFactory();
            var strategy = strategyFactory.SelectStrategy(context);

            // Create the new class with the ORIGINAL extracted member nodes using builder pattern
            var newClass = new ExtractedClassBuilder()
                .WithClassName(newClassName)
                .WithFields(fieldsToExtractNodes)
                .WithMethods(methodsToExtractNodes)
                .WithNestedTypes(nestedTypesToExtractNodes)
                .WithStrategy(strategy)
                .WithContext(context)
                .Build();

            // Single-pass transformation: removes extracted members, adds composition field, and adds extracted class
            // This eliminates stale reference issues from multi-mutation approach
            var transformer = new ExtractClassTransformer(
                className,
                fieldsToRemove,
                methodsToRemove,
                nestedTypesToExtract,
                newClassField,
                newClass);

            var newRoot = transformer.Visit(root);
            if (newRoot == null)
            {
                return RefactoringResult.Failure("Failed to transform source tree during extraction.");
            }

            // Normalize whitespace to ensure proper formatting
            newRoot = NormalizeWhitespace(newRoot);

            // NOTE: This synchronous Execute() method performs syntax validation only.
            // For comprehensive compilation validation with framework-specific BCL references,
            // use ExecuteAsync() with validateCompilation = true (default).
            // See ExecuteAsync() method documentation for details on validation options.

            // Build result message (warning only if external references exist)
            var resultMessage = _referenceUpdater.BuildExternalReferencesWarning(
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
}

internal class MemberSelector
{
    internal List<string> ParseNames(string names)
    {
        return names
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(n => n.Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();
    }

    /// <summary>
    /// Validates that all specified members exist in the class and returns their syntax nodes.
    /// </summary>
    /// <param name="classDeclaration">The class declaration to search.</param>
    /// <param name="className">The class name for error messages.</param>
    /// <param name="fieldNames">Field names to find.</param>
    /// <param name="methodNames">Method names to find.</param>
    /// <param name="nestedTypeNames">Nested type names to find.</param>
    /// <param name="fieldNodes">Output parameter for found field declarations.</param>
    /// <param name="methodNodes">Output parameter for found method declarations.</param>
    /// <param name="nestedTypeNodes">Output parameter for found nested type declarations.</param>
    /// <returns>Null if all members found successfully, or RefactoringResult.Failure with error message.</returns>
    internal RefactoringResult? ValidateAndFindMembers(
        ClassDeclarationSyntax classDeclaration,
        string className,
        List<string> fieldNames,
        List<string> methodNames,
        List<string> nestedTypeNames,
        out List<FieldDeclarationSyntax> fieldNodes,
        out List<MethodDeclarationSyntax> methodNodes,
        out List<BaseTypeDeclarationSyntax> nestedTypeNodes)
    {
        fieldNodes = new List<FieldDeclarationSyntax>();
        methodNodes = new List<MethodDeclarationSyntax>();
        nestedTypeNodes = new List<BaseTypeDeclarationSyntax>();

        // Validate and find fields
        foreach (var fieldName in fieldNames)
        {
            var fieldDeclaration = FindFieldDeclaration(classDeclaration, fieldName);
            if (fieldDeclaration == null)
            {
                return RefactoringResult.Failure($"Field '{fieldName}' not found in class '{className}'.");
            }
            fieldNodes.Add(fieldDeclaration);
        }

        // Validate and find methods
        foreach (var methodName in methodNames)
        {
            var methodDeclaration = classDeclaration.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.Text == methodName);

            if (methodDeclaration == null)
            {
                return RefactoringResult.Failure($"Method '{methodName}' not found in class '{className}'.");
            }
            methodNodes.Add(methodDeclaration);
        }

        // Validate and find nested types (with unsupported delegate check)
        foreach (var typeName in nestedTypeNames)
        {
            // Check for unsupported delegate types
            var delegateDeclaration = classDeclaration.Members
                .OfType<DelegateDeclarationSyntax>()
                .FirstOrDefault(d => d.Identifier.Text == typeName);

            if (delegateDeclaration != null)
            {
                return RefactoringResult.Failure(
                    $"Nested delegate extraction is not supported. Attempted to extract delegate '{typeName}'. " +
                    $"Delegates inherit from BaseMethodDeclarationSyntax, not BaseTypeDeclarationSyntax, and require specialized handling.");
            }

            var nestedType = FindNestedType(classDeclaration, typeName);
            if (nestedType == null)
            {
                return RefactoringResult.Failure($"Nested type '{typeName}' not found in class '{className}'.");
            }
            nestedTypeNodes.Add(nestedType);
        }

        return null; // Success - all members found
    }

    /// <summary>
    /// Re-finds members in an updated class declaration after tree mutations.
    /// Used to obtain fresh syntax nodes from a mutated syntax tree.
    /// </summary>
    /// <param name="classDeclaration">The updated class declaration.</param>
    /// <param name="fieldNames">Field names to re-find.</param>
    /// <param name="methodNames">Method names to re-find.</param>
    /// <returns>Tuple of field and method syntax nodes found in the updated class.</returns>
    /// <remarks>
    /// This method does not validate - it assumes members exist (validation happens earlier).
    /// It silently skips members not found, which should not occur in normal flow.
    /// </remarks>
    internal (List<FieldDeclarationSyntax> Fields, List<MethodDeclarationSyntax> Methods) RefindMembersInUpdatedClass(
        ClassDeclarationSyntax classDeclaration,
        List<string> fieldNames,
        List<string> methodNames)
    {
        var fields = new List<FieldDeclarationSyntax>();
        var methods = new List<MethodDeclarationSyntax>();

        // Re-find fields
        foreach (var fieldName in fieldNames)
        {
            var field = FindFieldDeclaration(classDeclaration, fieldName);
            if (field != null)
            {
                fields.Add(field);
            }
        }

        // Re-find methods
        foreach (var methodName in methodNames)
        {
            var method = classDeclaration.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.Text == methodName);
            if (method != null)
            {
                methods.Add(method);
            }
        }

        return (fields, methods);
    }

    internal FieldDeclarationSyntax? FindFieldDeclaration(ClassDeclarationSyntax classDeclaration, string fieldName)
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
    internal BaseTypeDeclarationSyntax? FindNestedType(ClassDeclarationSyntax classDeclaration, string typeName)
    {
        return classDeclaration.Members
            .OfType<BaseTypeDeclarationSyntax>()
            .FirstOrDefault(t => t.Identifier.Text == typeName);
    }
}
