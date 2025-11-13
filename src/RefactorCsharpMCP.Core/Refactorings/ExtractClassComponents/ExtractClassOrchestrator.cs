using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCsharpMCP.Core.Refactorings.ExtractClassComponents.Strategies;
using RefactorCsharpMCP.Core.Utilities;
using RefactorCsharpMCP.Core.Validation;

namespace RefactorCsharpMCP.Core.Refactorings.ExtractClassComponents;

/// <summary>
/// Orchestrates the Extract Class refactoring workflow.
/// Coordinates member selection, symbol resolution, reference updates, and tree transformation
/// to extract fields, methods, and nested types into a new class.
/// </summary>
internal class ExtractClassOrchestrator
{
    private readonly MemberSelector _memberSelector;
    private readonly ReferenceUpdater _referenceUpdater;
    private readonly SymbolResolutionHelper _symbolHelper;

    public ExtractClassOrchestrator(
        MemberSelector memberSelector,
        ReferenceUpdater referenceUpdater,
        SymbolResolutionHelper symbolHelper)
    {
        _memberSelector = memberSelector;
        _referenceUpdater = referenceUpdater;
        _symbolHelper = symbolHelper;
    }

    /// <summary>
    /// Executes the Extract Class refactoring operation.
    /// </summary>
    /// <param name="root">The parsed compilation unit root.</param>
    /// <param name="syntaxTree">The syntax tree for semantic analysis.</param>
    /// <param name="compilation">The compilation for symbol resolution.</param>
    /// <param name="className">The name of the source class.</param>
    /// <param name="newClassName">The name of the new class to create.</param>
    /// <param name="fieldsToExtract">List of field names to extract.</param>
    /// <param name="methodsToExtract">List of method names to extract.</param>
    /// <param name="nestedTypesToExtract">List of nested type names to extract.</param>
    /// <param name="normalizeWhitespace">Function to normalize whitespace in the result.</param>
    /// <returns>A result containing the refactored code or error information.</returns>
    public RefactoringResult ExecuteExtraction(
        SyntaxNode root,
        SyntaxTree syntaxTree,
        Compilation compilation,
        string className,
        string newClassName,
        List<string> fieldsToExtract,
        List<string> methodsToExtract,
        List<string> nestedTypesToExtract,
        Func<SyntaxNode, SyntaxNode> normalizeWhitespace)
    {
        // Create semantic model for symbol resolution
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
        var extractedSymbols = _referenceUpdater.GetExtractedSymbols(
            semanticModel,
            fieldsToExtractNodes,
            methodsToExtractNodes,
            nestedTypesToExtractNodes);

        // Get the source class symbol for semantic comparison
        var sourceClassSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
        if (sourceClassSymbol == null)
        {
            return RefactoringResult.Failure($"Could not resolve symbol for class '{className}'.");
        }

        // Defensive validation: Ensure newClassName is not empty before creating field name
        // Note: ExtractClass.cs validates at line 120, but orchestrator validates for defensive programming
        if (string.IsNullOrEmpty(newClassName))
        {
            return RefactoringResult.Failure("New class name cannot be empty.");
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
        newRoot = normalizeWhitespace(newRoot);

        // Build result message (warning only if external references exist)
        var resultMessage = _referenceUpdater.BuildExternalReferencesWarning(
            externalReferences,
            fieldsToExtract.Count,
            methodsToExtract.Count,
            nestedTypesToExtract.Count,
            newClassName);

        return RefactoringResult.Success(
            newRoot.ToFullString(),
            resultMessage);
    }

    /// <summary>
    /// Finds a class declaration by name in the syntax tree.
    /// </summary>
    private ClassDeclarationSyntax? FindClass(SyntaxNode root, string className)
    {
        return root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == className);
    }
}
