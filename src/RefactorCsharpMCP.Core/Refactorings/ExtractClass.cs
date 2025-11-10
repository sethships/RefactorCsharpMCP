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

            // Validate and find all members to extract
            var memberValidation = ValidateAndFindMembers(
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
            // This will be added by the transformer in a single pass
            var newClassField = CompositionFieldGenerator.CreateCompositionField(newClassName, newClassFieldName);

            // Re-find members in the updated class (fresh nodes from mutated tree)
            var (fieldsToRemove, methodsToRemove) = RefindMembersInUpdatedClass(
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
    private RefactoringResult? ValidateAndFindMembers(
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
    private (List<FieldDeclarationSyntax> Fields, List<MethodDeclarationSyntax> Methods) RefindMembersInUpdatedClass(
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
        var rewriter = new ReferenceTransformer(
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
}
