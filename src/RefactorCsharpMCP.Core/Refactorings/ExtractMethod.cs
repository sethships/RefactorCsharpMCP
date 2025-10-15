using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCsharpMCP.Core.Validation;
using System.Text.RegularExpressions;

namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Provides functionality to extract a block of code into a new method using Roslyn semantic analysis.
/// </summary>
public class ExtractMethod : RefactoringBase
{
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
            async () => await Task.Run(() => Execute(sourceCode, startLine, endLine, newMethodName)));
    }

    /// <summary>
    /// Extracts the specified lines of code into a new method (without validation).
    /// Use ExecuteAsync() for framework-aware validation.
    /// </summary>
    /// <param name="sourceCode">The source code containing the code to extract.</param>
    /// <param name="startLine">The starting line number (1-based) of the code to extract.</param>
    /// <param name="endLine">The ending line number (1-based) of the code to extract.</param>
    /// <param name="newMethodName">The name for the new extracted method.</param>
    /// <returns>A result containing the refactored code or error information.</returns>
    public RefactoringResult Execute(string sourceCode, int startLine, int endLine, string newMethodName)
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
            return RefactoringResult.Failure("Method name must be a valid C# identifier.");
        }

        if (startLine < 1 || endLine < startLine)
        {
            return RefactoringResult.Failure($"Invalid line range: {startLine}-{endLine}");
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
            var containingMethod = FindContainingMethod(root, startLine, endLine);
            if (containingMethod == null)
            {
                return RefactoringResult.Failure($"No method found containing lines {startLine}-{endLine}.");
            }

            // Find statements to extract based on line range
            var statementsToExtract = FindStatementsInLineRange(containingMethod, startLine, endLine);
            if (!statementsToExtract.Any())
            {
                return RefactoringResult.Failure($"No statements found in line range {startLine}-{endLine}.");
            }

            // Create compilation for semantic analysis
            var compilation = CreateCompilation(syntaxTree);
            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            // Analyze data flow for the selected statements
            var dataFlowAnalysis = AnalyzeDataFlow(semanticModel, statementsToExtract, containingMethod);

            // Build the new extracted method
            var extractedMethod = BuildExtractedMethod(
                newMethodName,
                statementsToExtract,
                dataFlowAnalysis,
                containingMethod
            );

            // Build the method call to replace the extracted statements
            var methodCall = BuildMethodCall(newMethodName, dataFlowAnalysis.Parameters);

            // Find the containing class to insert the new method
            var containingClass = containingMethod.FirstAncestorOrSelf<ClassDeclarationSyntax>();
            if (containingClass == null)
            {
                return RefactoringResult.Failure("Could not find containing class.");
            }

            // Replace statements with method call and add extracted method to class
            var updatedMethod = ReplaceStatementsWithMethodCall(containingMethod, statementsToExtract, methodCall);
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

    private MethodDeclarationSyntax? FindContainingMethod(CompilationUnitSyntax root, int startLine, int endLine)
    {
        return root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(method =>
            {
                var methodSpan = method.GetLocation().GetLineSpan();
                return methodSpan.StartLinePosition.Line + 1 <= startLine &&
                       methodSpan.EndLinePosition.Line + 1 >= endLine;
            });
    }

    private List<StatementSyntax> FindStatementsInLineRange(MethodDeclarationSyntax method, int startLine, int endLine)
    {
        if (method.Body == null) return new List<StatementSyntax>();

        return method.Body.Statements
            .Where(statement =>
            {
                var stmtSpan = statement.GetLocation().GetLineSpan();
                var stmtStart = stmtSpan.StartLinePosition.Line + 1;
                var stmtEnd = stmtSpan.EndLinePosition.Line + 1;

                // Statement is within or overlaps the line range
                return (stmtStart >= startLine && stmtStart <= endLine) ||
                       (stmtEnd >= startLine && stmtEnd <= endLine) ||
                       (stmtStart <= startLine && stmtEnd >= endLine);
            })
            .ToList();
    }

    private DataFlowInfo AnalyzeDataFlow(
        SemanticModel semanticModel,
        List<StatementSyntax> statements,
        MethodDeclarationSyntax containingMethod)
    {
        var dataFlow = new DataFlowInfo();

        if (!statements.Any()) return dataFlow;

        try
        {
            var firstStatement = statements.First();
            var lastStatement = statements.Last();

            var analysis = semanticModel.AnalyzeDataFlow(firstStatement, lastStatement);

            if (analysis == null || !analysis.Succeeded)
            {
                return dataFlow;
            }

            // Variables that flow into the selection (need to be parameters)
            // Exclude instance members (fields, properties) - they're accessible from the new method
            // Exclude 'this' parameter - instance methods have access to instance members
            dataFlow.Parameters = analysis.DataFlowsIn
                .Where(symbol => !analysis.VariablesDeclared.Contains(symbol))
                .Where(symbol => symbol is ILocalSymbol or IParameterSymbol) // Only locals and parameters
                .Where(symbol => symbol is not IParameterSymbol param || !param.IsThis) // Exclude 'this'
                .Select(symbol => new ParameterInfo
                {
                    Name = symbol.Name,
                    Type = GetSymbolType(symbol)
                })
                .ToList();

            // Variables that flow out (might need return value or out parameter)
            // Include variables that are assigned within the region but declared outside
            dataFlow.OutputVariables = analysis.DataFlowsOut
                .Where(symbol => symbol is ILocalSymbol) // Only local variables can flow out
                .Select(symbol => symbol.Name)
                .ToList();

            // Variables declared outside but assigned inside need to be captured
            // Exclude variables already in the parameter list to avoid duplicates
            dataFlow.AssignedOutsideVariables = analysis.WrittenInside
                .Where(symbol => !analysis.VariablesDeclared.Contains(symbol))
                .Where(symbol => !analysis.DataFlowsIn.Contains(symbol)) // Exclude parameters
                .Where(symbol => symbol is ILocalSymbol)
                .Select(symbol => new ParameterInfo
                {
                    Name = symbol.Name,
                    Type = GetSymbolType(symbol)
                })
                .ToList();
        }
        catch (Exception ex)
        {
            // Data flow analysis failed - log for debugging but continue with best effort
            // In production, consider returning an error instead of degraded behavior
            System.Diagnostics.Debug.WriteLine($"Data flow analysis failed: {ex.Message}");
        }

        return dataFlow;
    }

    private string GetSymbolType(ISymbol symbol)
    {
        // Use FullyQualifiedFormat to ensure we get complete type information
        // This includes namespace and generic type parameters
        var format = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            memberOptions: SymbolDisplayMemberOptions.None,
            parameterOptions: SymbolDisplayParameterOptions.None,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
        );

        var typeString = symbol switch
        {
            ILocalSymbol local => local.Type.ToDisplayString(format),
            IParameterSymbol param => param.Type.ToDisplayString(format),
            IFieldSymbol field => field.Type.ToDisplayString(format),
            IPropertySymbol prop => prop.Type.ToDisplayString(format),
            _ => "object"
        };

        // Fallback to "object" if we got an empty string
        return string.IsNullOrWhiteSpace(typeString) ? "object" : typeString;
    }

    private MethodDeclarationSyntax BuildExtractedMethod(
        string methodName,
        List<StatementSyntax> statements,
        DataFlowInfo dataFlowInfo,
        MethodDeclarationSyntax containingMethod)
    {
        // Build parameter list
        var parameters = SyntaxFactory.ParameterList(
            SyntaxFactory.SeparatedList(
                dataFlowInfo.Parameters.Select(p =>
                    SyntaxFactory.Parameter(SyntaxFactory.Identifier(p.Name))
                        .WithType(SyntaxFactory.ParseTypeName(p.Type))
                )
            )
        );

        // For now, use void return type (enhancement: detect return type from data flow)
        var returnType = SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword));

        // Check if containing method is static
        bool isStatic = containingMethod.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));

        // Add local variable declarations for variables assigned inside but declared outside
        var localDeclarations = dataFlowInfo.AssignedOutsideVariables
            .Select(v => SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName(v.Type))
                    .WithVariables(SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(v.Name))
                    ))
            ))
            .ToList<StatementSyntax>();

        // Combine local declarations with extracted statements
        var allStatements = localDeclarations.Concat(statements).ToList();

        // Build method body with the extracted statements
        var body = SyntaxFactory.Block(allStatements);

        // Build modifiers list (private, and static if needed)
        var modifiers = new List<SyntaxToken>
        {
            SyntaxFactory.Token(
                SyntaxFactory.TriviaList(),
                SyntaxKind.PrivateKeyword,
                SyntaxFactory.TriviaList(SyntaxFactory.Space))
        };

        if (isStatic)
        {
            modifiers.Add(SyntaxFactory.Token(
                SyntaxFactory.TriviaList(),
                SyntaxKind.StaticKeyword,
                SyntaxFactory.TriviaList(SyntaxFactory.Space)));
        }

        return SyntaxFactory.MethodDeclaration(returnType, methodName)
            .WithModifiers(SyntaxFactory.TokenList(modifiers))
            .WithParameterList(parameters)
            .WithBody(body)
            .WithLeadingTrivia(SyntaxFactory.CarriageReturnLineFeed)
            .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);
    }

    private StatementSyntax BuildMethodCall(string methodName, List<ParameterInfo> parameters)
    {
        var arguments = SyntaxFactory.ArgumentList(
            SyntaxFactory.SeparatedList(
                parameters.Select(p =>
                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName(p.Name))
                )
            )
        );

        var invocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.IdentifierName(methodName),
            arguments
        );

        return SyntaxFactory.ExpressionStatement(invocation);
    }

    private MethodDeclarationSyntax ReplaceStatementsWithMethodCall(
        MethodDeclarationSyntax method,
        List<StatementSyntax> statementsToRemove,
        StatementSyntax methodCall)
    {
        if (method.Body == null) return method;

        var newStatements = new List<StatementSyntax>();
        bool replacementMade = false;

        foreach (var statement in method.Body.Statements)
        {
            if (!replacementMade && statementsToRemove.Contains(statement))
            {
                // First statement to remove: replace with method call
                newStatements.Add(methodCall);
                replacementMade = true;
            }
            else if (!statementsToRemove.Contains(statement))
            {
                // Keep statements that aren't being extracted
                newStatements.Add(statement);
            }
            // Skip other statements being removed
        }

        return method.WithBody(SyntaxFactory.Block(newStatements));
    }

    private class DataFlowInfo
    {
        public List<ParameterInfo> Parameters { get; set; } = new();
        public List<string> OutputVariables { get; set; } = new();
        public List<ParameterInfo> AssignedOutsideVariables { get; set; } = new();
    }

    private class ParameterInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "object";
    }
}
