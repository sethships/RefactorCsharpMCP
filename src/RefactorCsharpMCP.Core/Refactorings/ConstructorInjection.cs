using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCsharpMCP.Core.Validation;

namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Provides functionality to convert method parameters to constructor-injected fields or properties using Roslyn.
/// </summary>
public class ConstructorInjection : RefactoringBase
{
    /// <summary>
    /// Converts specified method parameters to constructor-injected fields or properties with framework-aware validation.
    /// </summary>
    /// <param name="sourceCode">The source code containing the method.</param>
    /// <param name="className">The name of the class containing the method.</param>
    /// <param name="methodName">The name of the method with parameters to inject.</param>
    /// <param name="parameterNames">The names of parameters to convert to constructor injection.</param>
    /// <param name="targetFramework">The target .NET framework (e.g., "net8.0", "net48").</param>
    /// <param name="useProperties">If true, generates properties; if false, generates fields.</param>
    /// <returns>A result containing the refactored code or error information.</returns>
    public async Task<RefactoringResult> ExecuteAsync(
        string sourceCode,
        string className,
        string methodName,
        string[] parameterNames,
        string targetFramework,
        bool useProperties = false)
    {
        return await ExecuteWithValidationAsync(
            sourceCode,
            targetFramework,
            async () => await Task.Run(() => Execute(sourceCode, className, methodName, parameterNames, useProperties)));
    }

    /// <summary>
    /// Converts specified method parameters to constructor-injected fields or properties.
    /// </summary>
    /// <param name="sourceCode">The source code containing the method.</param>
    /// <param name="className">The name of the class containing the method.</param>
    /// <param name="methodName">The name of the method with parameters to inject.</param>
    /// <param name="parameterNames">The names of parameters to convert to constructor injection.</param>
    /// <param name="useProperties">If true, generates properties; if false, generates fields.</param>
    /// <returns>A result containing the refactored code or error information.</returns>
    public RefactoringResult Execute(
        string sourceCode,
        string className,
        string methodName,
        string[] parameterNames,
        bool useProperties = false)
    {
        // Validate inputs
        var sourceValidation = ValidateNonEmpty(sourceCode, "Source code");
        if (!sourceValidation.IsSuccess) return sourceValidation;

        var classValidation = ValidateNonEmpty(className, "Class name");
        if (!classValidation.IsSuccess) return classValidation;

        var methodValidation = ValidateNonEmpty(methodName, "Method name");
        if (!methodValidation.IsSuccess) return methodValidation;

        if (parameterNames == null || parameterNames.Length == 0)
        {
            return RefactoringResult.Failure("At least one parameter name must be specified.");
        }

        try
        {
            // Parse and validate syntax
            var parseResult = ParseAndValidateSyntax(sourceCode, out var root, out var syntaxTree);
            if (!parseResult.IsSuccess || root == null || syntaxTree == null)
            {
                return parseResult;
            }

            // Find the class declaration
            var classDeclaration = FindClass(root, className);
            if (classDeclaration == null)
            {
                return RefactoringResult.Failure($"Class '{className}' not found in source code.");
            }

            // Find the method declaration
            var methodDeclaration = FindMethod(classDeclaration, methodName);
            if (methodDeclaration == null)
            {
                return RefactoringResult.Failure($"Method '{methodName}' not found in class '{className}'.");
            }

            // Find the parameters to inject
            var parametersToInject = methodDeclaration.ParameterList.Parameters
                .Where(p => parameterNames.Contains(p.Identifier.Text))
                .ToList();

            if (parametersToInject.Count != parameterNames.Length)
            {
                var foundParams = string.Join(", ", parametersToInject.Select(p => p.Identifier.Text));
                return RefactoringResult.Failure($"Not all specified parameters found. Found: {foundParams}");
            }

            // Generate fields or properties using Roslyn SyntaxFactory
            var newMembers = new List<MemberDeclarationSyntax>();
            var newConstructorParams = new List<ParameterSyntax>();
            var newAssignments = new List<StatementSyntax>();

            foreach (var param in parametersToInject)
            {
                var paramType = param.Type ?? SyntaxFactory.ParseTypeName("object");
                var paramName = param.Identifier.Text;
                var memberName = useProperties ? ToPascalCase(paramName) : $"_{paramName}";

                if (useProperties)
                {
                    // Generate read-only property using SyntaxFactory with proper spacing
                    var property = SyntaxFactory.PropertyDeclaration(paramType, memberName)
                        .WithModifiers(SyntaxFactory.TokenList(
                            SyntaxFactory.Token(
                                SyntaxFactory.TriviaList(),
                                SyntaxKind.PublicKeyword,
                                SyntaxFactory.TriviaList(SyntaxFactory.Space))))
                        .WithAccessorList(SyntaxFactory.AccessorList(
                            SyntaxFactory.SingletonList(
                                SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                            )
                        ))
                        .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

                    newMembers.Add(property);
                }
                else
                {
                    // Generate private readonly field using SyntaxFactory with proper spacing
                    var field = SyntaxFactory.FieldDeclaration(
                        SyntaxFactory.VariableDeclaration(paramType)
                            .WithVariables(SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(memberName))
                            ))
                        )
                        .WithModifiers(SyntaxFactory.TokenList(
                            SyntaxFactory.Token(
                                SyntaxFactory.TriviaList(),
                                SyntaxKind.PrivateKeyword,
                                SyntaxFactory.TriviaList(SyntaxFactory.Space)),
                            SyntaxFactory.Token(
                                SyntaxFactory.TriviaList(),
                                SyntaxKind.ReadOnlyKeyword,
                                SyntaxFactory.TriviaList(SyntaxFactory.Space))
                        ))
                        .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

                    newMembers.Add(field);
                }

                // Build constructor parameter
                var ctorParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier(paramName))
                    .WithType(paramType);
                newConstructorParams.Add(ctorParam);

                // Build assignment statement
                var assignment = SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        SyntaxFactory.IdentifierName(memberName),
                        SyntaxFactory.IdentifierName(paramName)
                    )
                );
                newAssignments.Add(assignment);
            }

            // Handle existing constructor or create new one
            var existingConstructor = classDeclaration.DescendantNodes()
                .OfType<ConstructorDeclarationSyntax>()
                .FirstOrDefault();

            ConstructorDeclarationSyntax newConstructor;
            if (existingConstructor != null)
            {
                // Merge with existing constructor
                var existingParams = existingConstructor.ParameterList.Parameters;
                var mergedParams = existingParams.AddRange(newConstructorParams);

                var existingBody = existingConstructor.Body?.Statements ?? SyntaxFactory.List<StatementSyntax>();
                var mergedStatements = newAssignments.Concat(existingBody);

                newConstructor = existingConstructor
                    .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(mergedParams)))
                    .WithBody(SyntaxFactory.Block(mergedStatements))
                    .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);
            }
            else
            {
                // Create new constructor with proper spacing
                newConstructor = SyntaxFactory.ConstructorDeclaration(className)
                    .WithModifiers(SyntaxFactory.TokenList(
                        SyntaxFactory.Token(
                            SyntaxFactory.TriviaList(),
                            SyntaxKind.PublicKeyword,
                            SyntaxFactory.TriviaList(SyntaxFactory.Space))))
                    .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(newConstructorParams)))
                    .WithBody(SyntaxFactory.Block(newAssignments))
                    .WithLeadingTrivia(SyntaxFactory.CarriageReturnLineFeed)
                    .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);
            }

            // Update method to remove injected parameters
            var remainingParams = methodDeclaration.ParameterList.Parameters
                .Where(p => !parameterNames.Contains(p.Identifier.Text))
                .ToList();

            var updatedMethod = methodDeclaration
                .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(remainingParams)));

            // Update method body to use fields/properties instead of parameters
            updatedMethod = UpdateMethodBodyReferences(updatedMethod, parametersToInject, useProperties);

            // Build updated class
            var updatedClass = classDeclaration;

            // Add new members at the beginning of the class
            if (newMembers.Any())
            {
                updatedClass = updatedClass.WithMembers(
                    updatedClass.Members.InsertRange(0, newMembers)
                );
            }

            // Replace or add constructor
            if (existingConstructor != null)
            {
                updatedClass = updatedClass.ReplaceNode(
                    updatedClass.DescendantNodes().OfType<ConstructorDeclarationSyntax>().First(),
                    newConstructor
                );
            }
            else
            {
                // Insert constructor after fields/properties
                var insertIndex = newMembers.Count;
                updatedClass = updatedClass.WithMembers(
                    updatedClass.Members.Insert(insertIndex, newConstructor)
                );
            }

            // Replace the method
            updatedClass = updatedClass.ReplaceNode(
                updatedClass.DescendantNodes().OfType<MethodDeclarationSyntax>().First(m => m.Identifier.Text == methodName),
                updatedMethod
            );

            // Replace class in root
            var newRoot = root.ReplaceNode(classDeclaration, updatedClass);

            // Normalize whitespace to ensure proper formatting
            newRoot = NormalizeWhitespace(newRoot);

            var injectionType = useProperties ? "properties" : "fields";
            return RefactoringResult.Success(
                newRoot.ToFullString(),
                $"Converted {parameterNames.Length} parameter(s) to constructor-injected {injectionType} in '{className}.{methodName}'."
            );
        }
        catch (Exception ex)
        {
            return HandleException(ex, "constructor injection");
        }
    }

    private string ToPascalCase(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return char.ToUpper(text[0]) + text.Substring(1);
    }

    private MethodDeclarationSyntax UpdateMethodBodyReferences(
        MethodDeclarationSyntax method,
        List<ParameterSyntax> injectedParams,
        bool useProperties)
    {
        if (method.Body == null) return method;

        var updatedBody = method.Body;

        foreach (var param in injectedParams)
        {
            var paramName = param.Identifier.Text;
            var memberName = useProperties ? ToPascalCase(paramName) : $"_{paramName}";

            // Replace all references to the parameter with the field/property name
            updatedBody = updatedBody.ReplaceNodes(
                updatedBody.DescendantNodes().OfType<IdentifierNameSyntax>()
                    .Where(id => id.Identifier.Text == paramName),
                (original, rewritten) => SyntaxFactory.IdentifierName(memberName)
            );
        }

        return method.WithBody(updatedBody);
    }
}
