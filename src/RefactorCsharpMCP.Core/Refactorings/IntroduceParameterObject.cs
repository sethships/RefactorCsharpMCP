using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCsharpMCP.Core.Framework;
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

    /// <summary>
    /// Initializes a new instance of IntroduceParameterObject with optional dependencies.
    /// </summary>
    public IntroduceParameterObject(
        FrameworkValidator? frameworkValidator = null,
        LanguageVersionMapper? languageMapper = null)
    {
        _frameworkValidator = frameworkValidator ?? new FrameworkValidator();
        _languageMapper = languageMapper ?? new LanguageVersionMapper();
    }

    /// <summary>
    /// Replaces specified parameters with a parameter object with framework-aware validation.
    /// </summary>
    /// <param name="sourceCode">The source code containing the method.</param>
    /// <param name="className">The name of the class containing the method.</param>
    /// <param name="methodName">The name of the method with parameters to group.</param>
    /// <param name="parameterNames">The names of parameters to group into the parameter object.</param>
    /// <param name="newClassName">The name for the new parameter object class.</param>
    /// <param name="targetFramework">The target .NET framework (e.g., "net8.0", "net48").</param>
    /// <returns>A result containing the refactored code or error information.</returns>
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
    /// <param name="sourceCode">The source code containing the method.</param>
    /// <param name="className">The name of the class containing the method.</param>
    /// <param name="methodName">The name of the method with parameters to group.</param>
    /// <param name="parameterNames">The names of parameters to group into the parameter object.</param>
    /// <param name="newClassName">The name for the new parameter object class.</param>
    /// <param name="targetFramework">The target .NET framework (e.g., "net8.0", "net48").</param>
    /// <returns>A result containing the refactored code or error information.</returns>
    public RefactoringResult Execute(
        string sourceCode,
        string className,
        string methodName,
        string[] parameterNames,
        string newClassName,
        string targetFramework)
    {
        // Validate inputs
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
        {
            return RefactoringResult.Failure(ValidationErrorCode.MISSING_PARAMETER, "At least one parameter name must be specified.");
        }

        // Validate target framework
        var frameworkValidation = _frameworkValidator.Validate(targetFramework);
        if (frameworkValidation.ErrorCode != null)
        {
            // Map Framework.ErrorCode to Validation.ErrorCode (same numeric values)
            return RefactoringResult.Failure((ValidationErrorCode)frameworkValidation.ErrorCode.Value, frameworkValidation.ErrorMessage ?? "Invalid target framework.");
        }

        try
        {
            // STEP 1: Parse once and validate
            CurrentPhase = "Syntax Parsing";
            var parseResult = ParseAndValidateSyntax(sourceCode, out var root, out var syntaxTree);
            if (!parseResult.IsSuccess || root == null || syntaxTree == null)
            {
                return parseResult;
            }

            // STEP 2: Create compilation (leverage cache if available)
            CurrentPhase = "Semantic Analysis";
            var compilation = CreateCompilation(syntaxTree);
            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            // STEP 3: Find the class declaration
            CurrentPhase = "Class Discovery";
            var classDeclaration = FindClass(root, className);
            if (classDeclaration == null)
            {
                return RefactoringResult.Failure(ValidationErrorCode.NO_CLASS_FOUND, $"Class '{className}' not found in source code.");
            }

            // STEP 4: Check if parameter object class name already exists
            CurrentPhase = "Conflict Detection";
            if (ClassAlreadyExists(root, newClassName))
            {
                return RefactoringResult.Failure(ValidationErrorCode.DUPLICATE_CLASS_NAME, $"A class with the name '{newClassName}' already exists. Please choose a different name.");
            }

            // STEP 5: Find the method declaration and symbol
            CurrentPhase = "Method Discovery";
            var methodDeclaration = FindMethod(classDeclaration, methodName);
            if (methodDeclaration == null)
            {
                return RefactoringResult.Failure(ValidationErrorCode.NO_METHOD_FOUND, $"Method '{methodName}' not found in class '{className}'.");
            }

            var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration) as IMethodSymbol;
            if (methodSymbol == null)
            {
                return RefactoringResult.Failure(ValidationErrorCode.SEMANTIC_MODEL_ERROR, $"Unable to resolve symbol for method '{methodName}'.");
            }

            // STEP 6: Find the parameters to group
            CurrentPhase = "Parameter Resolution";
            var parametersToGroup = methodDeclaration.ParameterList.Parameters
                .Where(p => parameterNames.Contains(p.Identifier.Text))
                .ToList();

            if (parametersToGroup.Count != parameterNames.Length)
            {
                var foundParams = string.Join(", ", parametersToGroup.Select(p => p.Identifier.Text));
                return RefactoringResult.Failure(ValidationErrorCode.PARAMETER_NOT_FOUND, $"Not all specified parameters found. Found: {foundParams}");
            }

            // Get parameter symbols for semantic analysis
            var parameterSymbols = methodSymbol.Parameters
                .Where(p => parameterNames.Contains(p.Name))
                .ToList();

            if (parameterSymbols.Count != parameterNames.Length)
            {
                return RefactoringResult.Failure(ValidationErrorCode.PARAMETER_NOT_FOUND, "Unable to resolve all parameter symbols.");
            }

            // STEP 6a: Validate parameter types are compatible with parameter objects
            CurrentPhase = "Parameter Validation";

            // Check for ref/out parameters - not supported in records/classes
            var hasRefOut = parametersToGroup.Any(p =>
                p.Modifiers.Any(m =>
                    m.IsKind(SyntaxKind.RefKeyword) ||
                    m.IsKind(SyntaxKind.OutKeyword)));
            if (hasRefOut)
            {
                return RefactoringResult.Failure(
                    ValidationErrorCode.REF_OUT_PARAMETER_UNSUPPORTED,
                    "Cannot group ref or out parameters into a parameter object. " +
                    "These parameter types are incompatible with parameter objects.");
            }

            // Check for optional parameters - default values would be lost
            var hasOptional = parametersToGroup.Any(p => p.Default != null);
            if (hasOptional)
            {
                return RefactoringResult.Failure(
                    ValidationErrorCode.OPTIONAL_PARAMETER_UNSUPPORTED,
                    "Cannot group optional parameters - default values would be lost. " +
                    "This is not yet supported.");
            }

            // Check for params parameters - not supported in parameter objects
            var hasParams = parametersToGroup.Any(p =>
                p.Modifiers.Any(m => m.IsKind(SyntaxKind.ParamsKeyword)));
            if (hasParams)
            {
                return RefactoringResult.Failure(
                    ValidationErrorCode.PARAMS_PARAMETER_UNSUPPORTED,
                    "Cannot group params parameters into a parameter object. " +
                    "The params modifier is not supported in parameter objects.");
            }

            // STEP 7: Get language version to determine syntax features
            CurrentPhase = "Framework Analysis";
            var languageVersion = _languageMapper.GetLanguageVersion(targetFramework) ?? LanguageVersion.Default;
            var supportsRecords = languageVersion >= LanguageVersion.CSharp9 && !targetFramework.StartsWith("net4");

            // STEP 8: Generate the parameter object class
            CurrentPhase = "Parameter Object Generation";
            var parameterObjectClass = GenerateParameterObjectClass(
                newClassName,
                parametersToGroup,
                supportsRecords);

            // STEP 9: Update method signature
            CurrentPhase = "Method Signature Update";
            var updatedMethod = UpdateMethodSignature(
                methodDeclaration,
                parametersToGroup,
                newClassName,
                parameterNames);

            // STEP 10: Update method body to use parameter object
            CurrentPhase = "Method Body Update";
            var finalMethod = UpdateMethodBody(
                updatedMethod,
                parameterSymbols,
                methodSymbol,
                NamingHelper.ToCamelCase(newClassName),
                semanticModel);

            // STEP 11: Replace the original method with the updated one
            CurrentPhase = "Class Update";
            var updatedClass = classDeclaration.ReplaceNode(methodDeclaration, finalMethod);

            // STEP 12: Update all callers
            CurrentPhase = "Caller Update";
            var rootWithUpdatedClass = root.ReplaceNode(classDeclaration, updatedClass);
            var rootWithUpdatedCallers = UpdateCallers(
                rootWithUpdatedClass,
                methodSymbol,
                parameterSymbols,
                newClassName,
                supportsRecords,
                semanticModel);

            // STEP 13: Insert the parameter object class before the target class
            CurrentPhase = "Assembly";
            var finalRoot = InsertParameterObjectClass(
                rootWithUpdatedCallers,
                updatedClass,
                parameterObjectClass);

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
    /// Checks if a class with the given name already exists in the compilation unit.
    /// </summary>
    private bool ClassAlreadyExists(CompilationUnitSyntax root, string className)
    {
        return root.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .Any(t => t.Identifier.Text == className);
    }

    /// <summary>
    /// Generates a parameter object class or record based on the target framework.
    /// </summary>
    private MemberDeclarationSyntax GenerateParameterObjectClass(
        string className,
        List<ParameterSyntax> parameters,
        bool useRecord)
    {
        if (useRecord)
        {
            // Generate record with primary constructor for .NET 8+ (C# 9+)
            return GenerateRecordDeclaration(className, parameters);
        }
        else
        {
            // Generate traditional class for .NET Framework 4.8 (C# 7.3)
            return GenerateClassDeclaration(className, parameters);
        }
    }

    /// <summary>
    /// Generates a record declaration with primary constructor.
    /// Example: public record AddressInfo(string Street, string City, string Zip);
    /// </summary>
    private RecordDeclarationSyntax GenerateRecordDeclaration(
        string className,
        List<ParameterSyntax> parameters)
    {
        // Create parameter list for primary constructor
        var recordParameters = SyntaxFactory.SeparatedList(
            parameters.Select(p =>
                SyntaxFactory.Parameter(
                    SyntaxFactory.List<AttributeListSyntax>(),
                    SyntaxFactory.TokenList(),
                    p.Type,
                    SyntaxFactory.Identifier(NamingHelper.ToPascalCase(p.Identifier.Text)),
                    null)));

        return SyntaxFactory.RecordDeclaration(
            SyntaxFactory.Token(SyntaxKind.RecordKeyword),
            SyntaxFactory.Identifier(className))
            .WithModifiers(SyntaxFactory.TokenList(
                SyntaxFactory.Token(
                    SyntaxFactory.TriviaList(),
                    SyntaxKind.PublicKeyword,
                    SyntaxFactory.TriviaList(SyntaxFactory.Space))))
            .WithParameterList(
                SyntaxFactory.ParameterList(recordParameters))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
            .WithLeadingTrivia(SyntaxFactory.CarriageReturnLineFeed)
            .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed, SyntaxFactory.CarriageReturnLineFeed);
    }

    /// <summary>
    /// Generates a traditional class declaration with constructor and readonly properties.
    /// Example:
    /// public class AddressInfo
    /// {
    ///     public string Street { get; }
    ///     public string City { get; }
    ///     public string Zip { get; }
    ///     public AddressInfo(string street, string city, string zip)
    ///     {
    ///         Street = street;
    ///         City = city;
    ///         Zip = zip;
    ///     }
    /// }
    /// </summary>
    private ClassDeclarationSyntax GenerateClassDeclaration(
        string className,
        List<ParameterSyntax> parameters)
    {
        var members = new List<MemberDeclarationSyntax>();

        // Generate properties
        foreach (var param in parameters)
        {
            var propertyName = NamingHelper.ToPascalCase(param.Identifier.Text);
            var property = SyntaxFactory.PropertyDeclaration(
                param.Type ?? SyntaxFactory.ParseTypeName("object"),
                propertyName)
                .WithModifiers(SyntaxFactory.TokenList(
                    SyntaxFactory.Token(
                        SyntaxFactory.TriviaList(SyntaxFactory.Whitespace("    ")),
                        SyntaxKind.PublicKeyword,
                        SyntaxFactory.TriviaList(SyntaxFactory.Space))))
                .WithAccessorList(SyntaxFactory.AccessorList(
                    SyntaxFactory.SingletonList(
                        SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                    )))
                .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

            members.Add(property);
        }

        // Generate constructor
        var constructorParams = SyntaxFactory.SeparatedList(
            parameters.Select(p =>
                SyntaxFactory.Parameter(
                    SyntaxFactory.List<AttributeListSyntax>(),
                    SyntaxFactory.TokenList(),
                    p.Type,
                    SyntaxFactory.Identifier(NamingHelper.ToCamelCase(p.Identifier.Text)),
                    null)));

        var assignments = parameters.Select(p =>
        {
            var propertyName = NamingHelper.ToPascalCase(p.Identifier.Text);
            var paramName = NamingHelper.ToCamelCase(p.Identifier.Text);
            return (StatementSyntax)SyntaxFactory.ExpressionStatement(
                SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.IdentifierName(propertyName),
                    SyntaxFactory.IdentifierName(paramName)))
                .WithLeadingTrivia(SyntaxFactory.Whitespace("        "))
                .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);
        }).ToList();

        var constructor = SyntaxFactory.ConstructorDeclaration(className)
            .WithModifiers(SyntaxFactory.TokenList(
                SyntaxFactory.Token(
                    SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed, SyntaxFactory.Whitespace("    ")),
                    SyntaxKind.PublicKeyword,
                    SyntaxFactory.TriviaList(SyntaxFactory.Space))))
            .WithParameterList(SyntaxFactory.ParameterList(constructorParams))
            .WithBody(SyntaxFactory.Block(
                SyntaxFactory.Token(
                    SyntaxFactory.TriviaList(),
                    SyntaxKind.OpenBraceToken,
                    SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed)),
                SyntaxFactory.List(assignments),
                SyntaxFactory.Token(
                    SyntaxFactory.TriviaList(SyntaxFactory.Whitespace("    ")),
                    SyntaxKind.CloseBraceToken,
                    SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed))));

        members.Add(constructor);

        return SyntaxFactory.ClassDeclaration(className)
            .WithModifiers(SyntaxFactory.TokenList(
                SyntaxFactory.Token(
                    SyntaxFactory.TriviaList(),
                    SyntaxKind.PublicKeyword,
                    SyntaxFactory.TriviaList(SyntaxFactory.Space))))
            .WithMembers(SyntaxFactory.List(members))
            .WithOpenBraceToken(
                SyntaxFactory.Token(
                    SyntaxFactory.TriviaList(),
                    SyntaxKind.OpenBraceToken,
                    SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed)))
            .WithCloseBraceToken(
                SyntaxFactory.Token(
                    SyntaxFactory.TriviaList(),
                    SyntaxKind.CloseBraceToken,
                    SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed)))
            .WithLeadingTrivia(SyntaxFactory.CarriageReturnLineFeed)
            .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed, SyntaxFactory.CarriageReturnLineFeed);
    }

    /// <summary>
    /// Updates the method signature to replace grouped parameters with parameter object.
    /// </summary>
    private MethodDeclarationSyntax UpdateMethodSignature(
        MethodDeclarationSyntax method,
        List<ParameterSyntax> parametersToReplace,
        string parameterObjectClassName,
        string[] parameterNames)
    {
        // Create new parameter for the parameter object
        var paramObjectParamName = NamingHelper.ToCamelCase(parameterObjectClassName);
        var paramObjectType = SyntaxFactory.ParseTypeName(parameterObjectClassName);
        var paramObjectParam = SyntaxFactory.Parameter(
            SyntaxFactory.List<AttributeListSyntax>(),
            SyntaxFactory.TokenList(),
            paramObjectType,
            SyntaxFactory.Identifier(paramObjectParamName),
            null);

        // Keep parameters that are not being replaced
        var remainingParameters = method.ParameterList.Parameters
            .Where(p => !parameterNames.Contains(p.Identifier.Text))
            .ToList();

        // Build new parameter list: remaining parameters + parameter object
        var newParameters = new List<ParameterSyntax>(remainingParameters);
        newParameters.Add(paramObjectParam);

        var newParameterList = SyntaxFactory.ParameterList(
            SyntaxFactory.SeparatedList(newParameters));

        return method.WithParameterList(newParameterList);
    }

    /// <summary>
    /// Updates the method body to use parameter object properties instead of individual parameters.
    /// </summary>
    private MethodDeclarationSyntax UpdateMethodBody(
        MethodDeclarationSyntax method,
        List<IParameterSymbol> parameterSymbols,
        IMethodSymbol methodSymbol,
        string paramObjectName,
        SemanticModel semanticModel)
    {
        if (method.Body == null)
            return method;

        // Create a rewriter to replace parameter references using semantic model
        var rewriter = new ParameterReferenceRewriter(
            semanticModel,
            parameterSymbols,
            methodSymbol,
            paramObjectName);

        var newBody = (BlockSyntax)rewriter.Visit(method.Body);
        return method.WithBody(newBody);
    }

    /// <summary>
    /// Updates all callers of the method to pass parameter object.
    /// </summary>
    private CompilationUnitSyntax UpdateCallers(
        CompilationUnitSyntax root,
        IMethodSymbol methodSymbol,
        List<IParameterSymbol> parameterSymbols,
        string parameterObjectClassName,
        bool useRecord,
        SemanticModel semanticModel)
    {
        var rewriter = new InvocationRewriter(
            semanticModel,
            methodSymbol,
            parameterSymbols,
            parameterObjectClassName,
            useRecord);

        return (CompilationUnitSyntax)rewriter.Visit(root);
    }

    /// <summary>
    /// Inserts the parameter object class before the target class.
    /// </summary>
    private CompilationUnitSyntax InsertParameterObjectClass(
        CompilationUnitSyntax root,
        ClassDeclarationSyntax targetClass,
        MemberDeclarationSyntax parameterObjectClass)
    {
        // Find the namespace or use root
        var namespaceDecl = root.DescendantNodes()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault();

        if (namespaceDecl != null)
        {
            // Find the target class in the namespace
            var classInNamespace = namespaceDecl.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.Text == targetClass.Identifier.Text);

            if (classInNamespace != null)
            {
                // Insert before the class
                var index = namespaceDecl.Members.IndexOf(classInNamespace);
                if (index >= 0)
                {
                    var newMembers = namespaceDecl.Members.Insert(index, parameterObjectClass);
                    var newNamespace = namespaceDecl.WithMembers(newMembers);
                    return root.ReplaceNode(namespaceDecl, newNamespace);
                }
            }
        }

        // Insert at root level if no namespace
        var classAtRoot = root.Members.OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == targetClass.Identifier.Text);

        if (classAtRoot != null)
        {
            var classIndex = root.Members.IndexOf(classAtRoot);
            if (classIndex >= 0)
            {
                var rootMembers = root.Members.Insert(classIndex, parameterObjectClass);
                return root.WithMembers(rootMembers);
            }
        }

        // Fallback: append to end if we can't find the class (shouldn't happen)
        return root.WithMembers(root.Members.Add(parameterObjectClass));
    }

    /// <summary>
    /// Syntax rewriter to replace parameter references with parameter object property access.
    /// Uses semantic model to ensure only actual parameter references are replaced.
    /// </summary>
    private class ParameterReferenceRewriter : CSharpSyntaxRewriter
    {
        private readonly SemanticModel _semanticModel;
        private readonly HashSet<IParameterSymbol> _parameterSymbols;
        private readonly IMethodSymbol _targetMethod;
        private readonly string _paramObjectName;

        public ParameterReferenceRewriter(
            SemanticModel semanticModel,
            List<IParameterSymbol> parameterSymbols,
            IMethodSymbol targetMethod,
            string paramObjectName)
        {
            _semanticModel = semanticModel;
            _parameterSymbols = new HashSet<IParameterSymbol>(parameterSymbols, SymbolEqualityComparer.Default);
            _targetMethod = targetMethod;
            _paramObjectName = paramObjectName;
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            // Use semantic model to verify this identifier is actually a parameter reference
            var symbol = _semanticModel.GetSymbolInfo(node).Symbol as IParameterSymbol;

            if (symbol != null &&
                _parameterSymbols.Contains(symbol, SymbolEqualityComparer.Default) &&
                SymbolEqualityComparer.Default.Equals(symbol.ContainingSymbol, _targetMethod))
            {
                // Safe to replace - this is our target parameter
                var propertyName = NamingHelper.ToPascalCase(symbol.Name);
                return SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(_paramObjectName),
                    SyntaxFactory.IdentifierName(propertyName));
            }

            return base.VisitIdentifierName(node);
        }
    }

    /// <summary>
    /// Syntax rewriter to update method invocations to pass parameter object.
    /// Uses semantic model to accurately match arguments to parameters.
    /// </summary>
    private class InvocationRewriter : CSharpSyntaxRewriter
    {
        private readonly SemanticModel _semanticModel;
        private readonly IMethodSymbol _targetMethod;
        private readonly HashSet<IParameterSymbol> _parameterSymbols;
        private readonly string _parameterObjectClassName;
        private readonly bool _useRecord;

        public InvocationRewriter(
            SemanticModel semanticModel,
            IMethodSymbol targetMethod,
            List<IParameterSymbol> parameterSymbols,
            string parameterObjectClassName,
            bool useRecord)
        {
            _semanticModel = semanticModel;
            _targetMethod = targetMethod;
            _parameterSymbols = new HashSet<IParameterSymbol>(parameterSymbols, SymbolEqualityComparer.Default);
            _parameterObjectClassName = parameterObjectClassName;
            _useRecord = useRecord;
        }

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var symbolInfo = _semanticModel.GetSymbolInfo(node);
            var invokedMethod = symbolInfo.Symbol as IMethodSymbol;

            if (invokedMethod != null &&
                SymbolEqualityComparer.Default.Equals(invokedMethod, _targetMethod))
            {
                // Map arguments to parameters using semantic analysis
                var argumentMapping = GetArgumentParameterMapping(node, invokedMethod);

                var argumentsToGroup = argumentMapping
                    .Where(kvp => _parameterSymbols.Contains(kvp.Value, SymbolEqualityComparer.Default))
                    .Select(kvp => kvp.Key)
                    .ToList();

                var remainingArguments = argumentMapping
                    .Where(kvp => !_parameterSymbols.Contains(kvp.Value, SymbolEqualityComparer.Default))
                    .Select(kvp => kvp.Key)
                    .ToList();

                // Create parameter object instantiation
                var objectCreation = SyntaxFactory.ObjectCreationExpression(
                    SyntaxFactory.IdentifierName(_parameterObjectClassName))
                    .WithArgumentList(
                        SyntaxFactory.ArgumentList(
                            SyntaxFactory.SeparatedList(argumentsToGroup.Select(a =>
                                SyntaxFactory.Argument(a.Expression)))));

                var paramObjectArg = SyntaxFactory.Argument(objectCreation);

                // Build new argument list
                var newArguments = new List<ArgumentSyntax>(remainingArguments);
                newArguments.Add(paramObjectArg);

                return node.WithArgumentList(
                    SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(newArguments)));
            }

            return base.VisitInvocationExpression(node);
        }

        /// <summary>
        /// Maps arguments to parameters using semantic analysis.
        /// Handles named arguments, positional arguments, and optional parameters correctly.
        /// </summary>
        private Dictionary<ArgumentSyntax, IParameterSymbol> GetArgumentParameterMapping(
            InvocationExpressionSyntax invocation,
            IMethodSymbol method)
        {
            var mapping = new Dictionary<ArgumentSyntax, IParameterSymbol>();
            var arguments = invocation.ArgumentList.Arguments;

            for (int i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];
                IParameterSymbol? parameter = null;

                if (argument.NameColon != null)
                {
                    // Named argument - find by name
                    var paramName = argument.NameColon.Name.Identifier.Text;
                    parameter = method.Parameters.FirstOrDefault(p => p.Name == paramName);
                }
                else
                {
                    // Positional argument - use position
                    parameter = i < method.Parameters.Length ? method.Parameters[i] : null;
                }

                if (parameter != null)
                {
                    mapping[argument] = parameter;
                }
            }

            return mapping;
        }
    }
}
