using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCsharpMCP.Core.Framework;
using RefactorCsharpMCP.Core.Refactorings.IntroduceParameterObjectComponents;
using RefactorCsharpMCP.Core.Utilities;
using ValidationErrorCode = RefactorCsharpMCP.Core.Validation.ErrorCode;

namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Provides functionality to replace a group of parameters with a parameter object using Roslyn.
/// Generates framework-aware parameter objects (record for .NET 8+, class for .NET Framework 4.8).
///
/// <para>
/// <strong>Known Limitations:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Partial classes are not supported. The refactoring will only process the single class declaration provided in the source code.</description></item>
/// <item><description>Users are responsible for ensuring parameter types are appropriate for grouping. No validation is performed on type complexity or relationships.</description></item>
/// </list>
/// </summary>
public class IntroduceParameterObject : RefactoringBase
{
    private readonly FrameworkValidator _frameworkValidator;
    private readonly LanguageVersionMapper _languageMapper;
    private readonly ParameterObjectGenerator _generator;
    private readonly MethodSignatureUpdater _signatureUpdater;
    private readonly TypeInsertionHelper _typeInsertionHelper;

    /// <summary>
    /// Initializes a new instance of IntroduceParameterObject with optional dependencies.
    /// </summary>
    public IntroduceParameterObject(
        FrameworkValidator? frameworkValidator = null,
        LanguageVersionMapper? languageMapper = null,
        ParameterObjectGenerator? generator = null,
        MethodSignatureUpdater? signatureUpdater = null,
        TypeInsertionHelper? typeInsertionHelper = null)
    {
        _frameworkValidator = frameworkValidator ?? new FrameworkValidator();
        _languageMapper = languageMapper ?? new LanguageVersionMapper();
        _generator = generator ?? new ParameterObjectGenerator();
        _signatureUpdater = signatureUpdater ?? new MethodSignatureUpdater();
        _typeInsertionHelper = typeInsertionHelper ?? new TypeInsertionHelper();
    }

    /// <summary>
    /// Replaces specified parameters with a parameter object with framework-aware validation.
    /// </summary>
    public async Task<RefactoringResult> ExecuteAsync(
        string sourceCode,
        string className,
        string methodName,
        string[] parameterNames,
        string newClassName,
        string targetFramework)
    {
        return await ExecuteWithValidationAsync(
            sourceCode,
            targetFramework,
            async () => await Task.Run(() => Execute(sourceCode, className, methodName, parameterNames, newClassName, targetFramework)));
    }

    /// <summary>
    /// Replaces specified parameters with a parameter object.
    /// </summary>
    public RefactoringResult Execute(
        string sourceCode,
        string className,
        string methodName,
        string[] parameterNames,
        string newClassName,
        string targetFramework)
    {
        // PHASE 1: Input Validation
        var validationResult = ValidateInputs(sourceCode, className, methodName, parameterNames, newClassName, targetFramework);
        if (validationResult != null)
            return validationResult;

        try
        {
            // PHASE 2: Syntax Parsing
            CurrentPhase = "Syntax Parsing";
            var parseResult = ParseAndValidateSyntax(sourceCode, out var root, out var syntaxTree);
            if (!parseResult.IsSuccess || root == null || syntaxTree == null)
                return parseResult;

            // PHASE 3: Semantic Analysis
            CurrentPhase = "Semantic Analysis";
            var compilation = CreateCompilation(syntaxTree);
            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            // PHASE 4: Class Discovery
            CurrentPhase = "Class Discovery";
            var classDeclaration = FindClass(root, className);
            if (classDeclaration == null)
                return RefactoringResult.Failure(ValidationErrorCode.NO_CLASS_FOUND, $"Class '{className}' not found in source code.");

            // PHASE 5: Conflict Detection
            CurrentPhase = "Conflict Detection";
            if (ClassAlreadyExists(root, newClassName))
                return RefactoringResult.Failure(ValidationErrorCode.DUPLICATE_CLASS_NAME, $"A class with the name '{newClassName}' already exists. Please choose a different name.");

            // PHASE 6: Method Discovery
            CurrentPhase = "Method Discovery";
            var methodDeclaration = FindMethod(classDeclaration, methodName);
            if (methodDeclaration == null)
                return RefactoringResult.Failure(ValidationErrorCode.NO_METHOD_FOUND, $"Method '{methodName}' not found in class '{className}'.");

            var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration) as IMethodSymbol;
            if (methodSymbol == null)
                return RefactoringResult.Failure(ValidationErrorCode.SEMANTIC_MODEL_ERROR, $"Unable to resolve symbol for method '{methodName}'.");

            // PHASE 7: Parameter Resolution & Validation
            var parameterResult = ResolveAndValidateParameters(methodDeclaration, methodSymbol, parameterNames);
            if (!parameterResult.IsSuccess)
                return parameterResult.ErrorResult!;

            var parametersToGroup = parameterResult.Parameters!;
            var parameterSymbols = parameterResult.ParameterSymbols!;

            // PHASE 8: Framework Analysis
            CurrentPhase = "Framework Analysis";
            var languageVersion = _languageMapper.GetLanguageVersion(targetFramework) ?? LanguageVersion.Default;
            var supportsRecords = languageVersion >= LanguageVersion.CSharp9 && !targetFramework.StartsWith("net4");

            // PHASE 9: Generate Parameter Object
            CurrentPhase = "Parameter Object Generation";
            var parameterObjectClass = _generator.Generate(newClassName, parametersToGroup, supportsRecords);

            // PHASE 10: Update Method Signature
            CurrentPhase = "Method Signature Update";
            var updatedMethod = _signatureUpdater.UpdateSignature(methodDeclaration, parametersToGroup, newClassName, parameterNames);

            // PHASE 11: Update Method Body
            CurrentPhase = "Method Body Update";
            var finalMethod = UpdateMethodBody(updatedMethod, parameterSymbols, NamingHelper.ToCamelCase(newClassName));

            // PHASE 12: Replace Method in Class
            CurrentPhase = "Class Update";
            var updatedClass = classDeclaration.ReplaceNode(methodDeclaration, finalMethod);

            // PHASE 13: Update Callers
            CurrentPhase = "Caller Update";
            var rootWithUpdatedClass = root.ReplaceNode(classDeclaration, updatedClass);
            var rootWithUpdatedCallers = UpdateCallers(rootWithUpdatedClass, methodSymbol, parameterSymbols, newClassName, supportsRecords);

            // PHASE 14: Insert Parameter Object
            CurrentPhase = "Assembly";
            var finalRoot = _typeInsertionHelper.InsertParameterObjectClass(rootWithUpdatedCallers, updatedClass, parameterObjectClass);

            return RefactoringResult.Success(
                finalRoot.NormalizeWhitespace().ToFullString(),
                $"Successfully introduced parameter object '{newClassName}' for {parametersToGroup.Count} parameters in method '{methodName}'.");
        }
        catch (Exception ex)
        {
            return HandleException(ex, "introduce parameter object");
        }
    }

    /// <summary>
    /// Validates input parameters. Returns null if validation passes, or a failure result if validation fails.
    /// </summary>
    private RefactoringResult? ValidateInputs(string sourceCode, string className, string methodName, string[] parameterNames, string newClassName, string targetFramework)
    {
        CurrentPhase = "Input Validation";

        var sourceValidation = ValidateNonEmpty(sourceCode, "Source code");
        if (!sourceValidation.IsSuccess) return sourceValidation;

        var classValidation = ValidateNonEmpty(className, "Class name");
        if (!classValidation.IsSuccess) return classValidation;

        var methodValidation = ValidateNonEmpty(methodName, "Method name");
        if (!methodValidation.IsSuccess) return methodValidation;

        var newClassValidation = ValidateNonEmpty(newClassName, "New class name");
        if (!newClassValidation.IsSuccess) return newClassValidation;

        if (parameterNames == null || parameterNames.Length == 0)
            return RefactoringResult.Failure(ValidationErrorCode.MISSING_PARAMETER, "At least one parameter name must be specified.");

        var frameworkValidation = _frameworkValidator.Validate(targetFramework);
        if (frameworkValidation.ErrorCode != null)
            return RefactoringResult.Failure((ValidationErrorCode)frameworkValidation.ErrorCode.Value, frameworkValidation.ErrorMessage ?? "Invalid target framework.");

        return null; // Validation passed
    }

    private bool ClassAlreadyExists(CompilationUnitSyntax root, string className)
    {
        return root.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .Any(t => t.Identifier.Text == className);
    }

    private ParameterResolutionResult ResolveAndValidateParameters(MethodDeclarationSyntax methodDeclaration, IMethodSymbol methodSymbol, string[] parameterNames)
    {
        CurrentPhase = "Parameter Resolution";
        var parametersToGroup = methodDeclaration.ParameterList.Parameters
            .Where(p => parameterNames.Contains(p.Identifier.Text))
            .ToList();

        if (parametersToGroup.Count != parameterNames.Length)
        {
            var foundParams = string.Join(", ", parametersToGroup.Select(p => p.Identifier.Text));
            return ParameterResolutionResult.Error(RefactoringResult.Failure(ValidationErrorCode.PARAMETER_NOT_FOUND, $"Not all specified parameters found. Found: {foundParams}"));
        }

        var parameterSymbols = methodSymbol.Parameters
            .Where(p => parameterNames.Contains(p.Name))
            .ToList();

        if (parameterSymbols.Count != parameterNames.Length)
            return ParameterResolutionResult.Error(RefactoringResult.Failure(ValidationErrorCode.PARAMETER_NOT_FOUND, "Unable to resolve all parameter symbols."));

        CurrentPhase = "Parameter Validation";

        if (parametersToGroup.Any(p => p.Modifiers.Any(m => m.IsKind(SyntaxKind.RefKeyword) || m.IsKind(SyntaxKind.OutKeyword))))
            return ParameterResolutionResult.Error(RefactoringResult.Failure(ValidationErrorCode.REF_OUT_PARAMETER_UNSUPPORTED, "Cannot group ref or out parameters into a parameter object. These parameter types are incompatible with parameter objects."));

        if (parametersToGroup.Any(p => p.Default != null))
            return ParameterResolutionResult.Error(RefactoringResult.Failure(ValidationErrorCode.OPTIONAL_PARAMETER_UNSUPPORTED, "Cannot group optional parameters - default values would be lost. This is not yet supported."));

        if (parametersToGroup.Any(p => p.Modifiers.Any(m => m.IsKind(SyntaxKind.ParamsKeyword))))
            return ParameterResolutionResult.Error(RefactoringResult.Failure(ValidationErrorCode.PARAMS_PARAMETER_UNSUPPORTED, "Cannot group params parameters into a parameter object. The params modifier is not supported in parameter objects."));

        return ParameterResolutionResult.Ok(parametersToGroup, parameterSymbols);
    }

    private MethodDeclarationSyntax UpdateMethodBody(MethodDeclarationSyntax method, List<IParameterSymbol> parameterSymbols, string paramObjectName)
    {
        if (method.Body == null)
            return method;

        var rewriter = new ShadowingAwareRewriter(parameterSymbols, paramObjectName);
        var newBody = (BlockSyntax)rewriter.Visit(method.Body);
        return method.WithBody(newBody);
    }

    private CompilationUnitSyntax UpdateCallers(CompilationUnitSyntax root, IMethodSymbol methodSymbol, List<IParameterSymbol> parameterSymbols, string parameterObjectClassName, bool useRecord)
    {
        var rewriter = new InvocationTransformer(methodSymbol, parameterSymbols, parameterObjectClassName, useRecord);
        return (CompilationUnitSyntax)rewriter.Visit(root);
    }

    /// <summary>
    /// Internal result type for parameter resolution to avoid multiple returns.
    /// </summary>
    private class ParameterResolutionResult
    {
        public bool IsSuccess { get; private set; }
        public List<ParameterSyntax>? Parameters { get; private set; }
        public List<IParameterSymbol>? ParameterSymbols { get; private set; }
        public RefactoringResult? ErrorResult { get; private set; }

        public static ParameterResolutionResult Ok(List<ParameterSyntax> parameters, List<IParameterSymbol> symbols)
            => new() { IsSuccess = true, Parameters = parameters, ParameterSymbols = symbols };

        public static ParameterResolutionResult Error(RefactoringResult error)
            => new() { IsSuccess = false, ErrorResult = error };
    }
}
