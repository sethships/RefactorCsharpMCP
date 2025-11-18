using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCsharpMCP.Core.Framework;
using RefactorCsharpMCP.Core.Validation;

namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Provides functionality to replace a group of parameters with a parameter object using Roslyn.
/// Generates framework-aware parameter objects (record for .NET 8+, class for .NET Framework 4.8).
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
            return RefactoringResult.Failure(ErrorCode.MISSING_PARAMETER, "At least one parameter name must be specified.");
        }

        // Validate target framework
        var frameworkValidation = _frameworkValidator.Validate(targetFramework);
        if (!frameworkValidation.IsSuccess)
        {
            return RefactoringResult.Failure(ErrorCode.INVALID_FRAMEWORK, frameworkValidation.ErrorMessage ?? "Invalid target framework.");
        }

        try
        {
            CurrentPhase = "Syntax Parsing";

            // Parse and validate syntax
            var parseResult = ParseAndValidateSyntax(sourceCode, out var root, out var syntaxTree);
            if (!parseResult.IsSuccess || root == null || syntaxTree == null)
            {
                return parseResult;
            }

            CurrentPhase = "Method Discovery";

            // Find the class declaration
            var classDeclaration = FindClass(root, className);
            if (classDeclaration == null)
            {
                return RefactoringResult.Failure(ErrorCode.NO_CLASS_FOUND, $"Class '{className}' not found in source code.");
            }

            // Find the method declaration
            var methodDeclaration = FindMethod(classDeclaration, methodName);
            if (methodDeclaration == null)
            {
                return RefactoringResult.Failure(ErrorCode.NO_METHOD_FOUND, $"Method '{methodName}' not found in class '{className}'.");
            }

            CurrentPhase = "Parameter Extraction";

            // Find the parameters to group
            var parametersToGroup = methodDeclaration.ParameterList.Parameters
                .Where(p => parameterNames.Contains(p.Identifier.Text))
                .ToList();

            if (parametersToGroup.Count != parameterNames.Length)
            {
                var foundParams = string.Join(", ", parametersToGroup.Select(p => p.Identifier.Text));
                return RefactoringResult.Failure(ErrorCode.PARAMETER_NOT_FOUND, $"Not all specified parameters found. Found: {foundParams}");
            }

            CurrentPhase = "Framework Analysis";

            // Get language version to determine syntax features
            var languageVersion = _languageMapper.GetLanguageVersion(targetFramework) ?? LanguageVersion.CSharp12;
            var supportsRecords = languageVersion >= LanguageVersion.CSharp9;

            CurrentPhase = "Parameter Object Generation";

            // Generate the parameter object class
            var parameterObjectClass = GenerateParameterObjectClass(
                newClassName,
                parametersToGroup,
                supportsRecords);

            CurrentPhase = "Method Signature Update";

            // Update method signature
            var updatedMethod = UpdateMethodSignature(
                methodDeclaration,
                parametersToGroup,
                newClassName,
                parameterNames);

            CurrentPhase = "Method Body Update";

            // Update method body to use parameter object
            var finalMethod = UpdateMethodBody(
                updatedMethod,
                parametersToGroup,
                GetParameterObjectParameterName(updatedMethod, newClassName));

            CurrentPhase = "Caller Update";

            // Replace the original method with the updated one
            var updatedClass = classDeclaration.ReplaceNode(methodDeclaration, finalMethod);

            // Update all callers
            var rootWithUpdatedClass = root.ReplaceNode(classDeclaration, updatedClass);
            var rootWithUpdatedCallers = UpdateCallers(
                rootWithUpdatedClass,
                methodName,
                parametersToGroup,
                newClassName,
                supportsRecords);

            CurrentPhase = "Assembly";

            // Insert the parameter object class before the target class
            var finalRoot = InsertParameterObjectClass(
                rootWithUpdatedCallers,
                updatedClass,
                parameterObjectClass);

            return RefactoringResult.Success(
                finalRoot.ToFullString(),
                $"Successfully introduced parameter object '{newClassName}' for {parametersToGroup.Count} parameters in method '{methodName}'.");
        }
        catch (Exception ex)
        {
            return HandleException(ex, "introduce parameter object");
        }
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
                    SyntaxFactory.Identifier(ToPascalCase(p.Identifier.Text)),
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
            var propertyName = ToPascalCase(param.Identifier.Text);
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
                    SyntaxFactory.Identifier(ToCamelCase(p.Identifier.Text)),
                    null)));

        var assignments = parameters.Select(p =>
        {
            var propertyName = ToPascalCase(p.Identifier.Text);
            var paramName = ToCamelCase(p.Identifier.Text);
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
        var paramObjectParamName = ToCamelCase(parameterObjectClassName);
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
        List<ParameterSyntax> parametersToReplace,
        string paramObjectName)
    {
        if (method.Body == null)
            return method;

        // Create a rewriter to replace parameter references
        var rewriter = new ParameterReferenceRewriter(
            parametersToReplace.Select(p => p.Identifier.Text).ToHashSet(),
            paramObjectName);

        var newBody = (BlockSyntax)rewriter.Visit(method.Body);
        return method.WithBody(newBody);
    }

    /// <summary>
    /// Gets the parameter object parameter name from the updated method.
    /// </summary>
    private string GetParameterObjectParameterName(
        MethodDeclarationSyntax method,
        string parameterObjectClassName)
    {
        return ToCamelCase(parameterObjectClassName);
    }

    /// <summary>
    /// Updates all callers of the method to pass parameter object.
    /// </summary>
    private CompilationUnitSyntax UpdateCallers(
        CompilationUnitSyntax root,
        string methodName,
        List<ParameterSyntax> parametersToGroup,
        string parameterObjectClassName,
        bool useRecord)
    {
        var rewriter = new InvocationRewriter(
            methodName,
            parametersToGroup.Select(p => p.Identifier.Text).ToHashSet(),
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
                var newMembers = namespaceDecl.Members.Insert(index, parameterObjectClass);
                var newNamespace = namespaceDecl.WithMembers(newMembers);
                return root.ReplaceNode(namespaceDecl, newNamespace);
            }
        }

        // Insert at root level if no namespace
        var classIndex = root.Members.IndexOf(root.Members.OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == targetClass.Identifier.Text));
        var rootMembers = root.Members.Insert(classIndex, parameterObjectClass);
        return root.WithMembers(rootMembers);
    }

    /// <summary>
    /// Converts a name to PascalCase.
    /// </summary>
    private string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        return char.ToUpperInvariant(name[0]) + name.Substring(1);
    }

    /// <summary>
    /// Converts a name to camelCase.
    /// </summary>
    private string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    /// <summary>
    /// Syntax rewriter to replace parameter references with parameter object property access.
    /// </summary>
    private class ParameterReferenceRewriter : CSharpSyntaxRewriter
    {
        private readonly HashSet<string> _parameterNames;
        private readonly string _paramObjectName;

        public ParameterReferenceRewriter(HashSet<string> parameterNames, string paramObjectName)
        {
            _parameterNames = parameterNames;
            _paramObjectName = paramObjectName;
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            // Check if this identifier is one of the parameters we're replacing
            if (_parameterNames.Contains(node.Identifier.Text))
            {
                // Replace with paramObject.PropertyName
                var propertyName = char.ToUpperInvariant(node.Identifier.Text[0]) + node.Identifier.Text.Substring(1);
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
    /// </summary>
    private class InvocationRewriter : CSharpSyntaxRewriter
    {
        private readonly string _methodName;
        private readonly HashSet<string> _parameterNames;
        private readonly string _parameterObjectClassName;
        private readonly bool _useRecord;

        public InvocationRewriter(
            string methodName,
            HashSet<string> parameterNames,
            string parameterObjectClassName,
            bool useRecord)
        {
            _methodName = methodName;
            _parameterNames = parameterNames;
            _parameterObjectClassName = parameterObjectClassName;
            _useRecord = useRecord;
        }

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            // Check if this is an invocation of our target method
            var methodIdentifier = GetMethodIdentifier(node.Expression);
            if (methodIdentifier != null && methodIdentifier == _methodName)
            {
                // Extract arguments for parameters being grouped
                var argumentsToGroup = new List<ArgumentSyntax>();
                var remainingArguments = new List<ArgumentSyntax>();

                foreach (var argument in node.ArgumentList.Arguments)
                {
                    // Simple heuristic: group first N arguments matching parameter count
                    if (argumentsToGroup.Count < _parameterNames.Count)
                    {
                        argumentsToGroup.Add(argument);
                    }
                    else
                    {
                        remainingArguments.Add(argument);
                    }
                }

                // Create parameter object instantiation
                ArgumentSyntax paramObjectArg;
                if (_useRecord)
                {
                    // new AddressInfo(street, city, zip)
                    var objectCreation = SyntaxFactory.ObjectCreationExpression(
                        SyntaxFactory.IdentifierName(_parameterObjectClassName))
                        .WithArgumentList(
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SeparatedList(argumentsToGroup.Select(a =>
                                    SyntaxFactory.Argument(a.Expression)))));
                    paramObjectArg = SyntaxFactory.Argument(objectCreation);
                }
                else
                {
                    // new AddressInfo(street, city, zip) - same syntax for class
                    var objectCreation = SyntaxFactory.ObjectCreationExpression(
                        SyntaxFactory.IdentifierName(_parameterObjectClassName))
                        .WithArgumentList(
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SeparatedList(argumentsToGroup.Select(a =>
                                    SyntaxFactory.Argument(a.Expression)))));
                    paramObjectArg = SyntaxFactory.Argument(objectCreation);
                }

                // Build new argument list
                var newArguments = new List<ArgumentSyntax>(remainingArguments);
                newArguments.Add(paramObjectArg);

                var newArgumentList = SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList(newArguments));

                return node.WithArgumentList(newArgumentList);
            }

            return base.VisitInvocationExpression(node);
        }

        private string? GetMethodIdentifier(ExpressionSyntax expression)
        {
            return expression switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.Text,
                MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
                _ => null
            };
        }
    }
}
