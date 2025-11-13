using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Generates method declarations and method calls for extracted code.
/// Handles framework-aware syntax generation including tuple returns for C# 7+.
/// </summary>
internal class MethodGenerator
{
    /// <summary>
    /// Builds the extracted method declaration with proper signature and body.
    /// </summary>
    /// <param name="methodName">Name for the new method</param>
    /// <param name="statements">Statements to include in the method body</param>
    /// <param name="dataFlowInfo">Data flow information for parameters and return values</param>
    /// <param name="containingMethod">The method from which code is being extracted</param>
    /// <param name="targetFramework">Target framework for compatibility</param>
    /// <returns>The generated method declaration</returns>
    internal MethodDeclarationSyntax BuildExtractedMethod(
        string methodName,
        List<StatementSyntax> statements,
        DataFlowInfo dataFlowInfo,
        MethodDeclarationSyntax containingMethod,
        string targetFramework)
    {
        // Get language version for framework-aware syntax generation
        var languageVersion = Infrastructure.FrameworkSupport.FrameworkMoniker.GetLanguageVersion(targetFramework);

        // Note: Framework compatibility validation performed in Execute method (Issue #51)
        // This method should only be called if validation passed

        // Build parameter list
        var parameters = SyntaxFactory.ParameterList(
            SyntaxFactory.SeparatedList(
                dataFlowInfo.Parameters.Select(p =>
                    SyntaxFactory.Parameter(SyntaxFactory.Identifier(p.Name))
                        .WithType(SyntaxFactory.ParseTypeName(p.Type))
                )
            )
        );

        // Generate framework-aware return type based on data flow analysis
        var returnType = GenerateReturnType(dataFlowInfo.ReturnInfo, languageVersion);

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

        // Add return statement if method has return value
        if (dataFlowInfo.ReturnInfo != null && dataFlowInfo.ReturnInfo.Kind != ReturnKind.Void)
        {
            var returnStatement = GenerateReturnStatement(dataFlowInfo.ReturnInfo);
            allStatements.Add(returnStatement);
        }

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

    /// <summary>
    /// Builds the method call statement that replaces the extracted code.
    /// Handles void, single return, and tuple returns.
    /// </summary>
    /// <param name="methodName">The name of the method to call.</param>
    /// <param name="parameters">The list of parameters to pass to the method call.</param>
    /// <param name="returnInfo">Information about the return type; determines how the call is structured.</param>
    /// <returns>A statement syntax node representing the method call with appropriate return handling.</returns>
    internal StatementSyntax BuildMethodCall(
        string methodName,
        List<ParameterInfo> parameters,
        ReturnTypeInfo? returnInfo)
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

        // Void method - just call it
        if (returnInfo == null || returnInfo.Kind == ReturnKind.Void)
        {
            return SyntaxFactory.ExpressionStatement(invocation);
        }

        // Single return value - assign to variable
        if (returnInfo.Kind == ReturnKind.Single)
        {
            var variableName = returnInfo.SingleReturnName ?? "result";
            var assignment = SyntaxFactory.AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                SyntaxFactory.IdentifierName(variableName),
                invocation
            );
            return SyntaxFactory.ExpressionStatement(assignment);
        }

        // Multiple return values - tuple deconstruction
        if (returnInfo.Kind == ReturnKind.Multiple)
        {
            var tupleElements = returnInfo.MultipleReturns
                .Select(r => SyntaxFactory.Argument(
                    SyntaxFactory.IdentifierName(r.Name)))
                .ToArray();

            var tupleExpression = SyntaxFactory.TupleExpression(
                SyntaxFactory.SeparatedList(tupleElements));

            var assignment = SyntaxFactory.AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                tupleExpression,
                invocation
            );
            return SyntaxFactory.ExpressionStatement(assignment);
        }

        // Fallback - void call
        return SyntaxFactory.ExpressionStatement(invocation);
    }

    /// <summary>
    /// Replaces the extracted statements in the original method with a call to the new method.
    /// </summary>
    /// <param name="method">The method containing statements to replace</param>
    /// <param name="statementsToRemove">The statements being extracted</param>
    /// <param name="methodCall">The method call to insert</param>
    /// <returns>Updated method with statements replaced</returns>
    internal MethodDeclarationSyntax ReplaceStatementsWithMethodCall(
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

    /// <summary>
    /// Generates the return type syntax for the extracted method based on return info and language version.
    /// </summary>
    /// <param name="returnInfo">Information about the return type detected from data flow analysis.</param>
    /// <param name="languageVersion">The C# language version for framework compatibility.</param>
    /// <returns>A TypeSyntax representing the return type.</returns>
    internal TypeSyntax GenerateReturnType(ReturnTypeInfo? returnInfo, LanguageVersion languageVersion)
    {
        // Default to void if no return info available
        if (returnInfo == null || returnInfo.Kind == ReturnKind.Void)
        {
            return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword));
        }

        // Handle single return value
        if (returnInfo.Kind == ReturnKind.Single)
        {
            var typeString = returnInfo.SingleReturnType ?? "object";
            return SyntaxFactory.ParseTypeName(typeString);
        }

        // Handle multiple return values (tuple)
        if (returnInfo.Kind == ReturnKind.Multiple)
        {
            // Note: Framework compatibility validated in BuildExtractedMethod (Issue #51)
            // This code path should only be reached if languageVersion >= CSharp7

            // Build value tuple type: (int x, string y)
            var tupleElements = returnInfo.MultipleReturns
                .Select(r => SyntaxFactory.TupleElement(
                    SyntaxFactory.ParseTypeName(r.Type),
                    SyntaxFactory.Identifier(r.Name)))
                .ToArray();

            return SyntaxFactory.TupleType(
                SyntaxFactory.SeparatedList(tupleElements));
        }

        // Fallback to void
        return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword));
    }

    /// <summary>
    /// Generates the return statement for the extracted method based on return info.
    /// </summary>
    /// <param name="returnInfo">Information about what should be returned.</param>
    /// <returns>A return statement syntax node.</returns>
    internal StatementSyntax GenerateReturnStatement(ReturnTypeInfo returnInfo)
    {
        // Single return value
        if (returnInfo.Kind == ReturnKind.Single)
        {
            var variableName = returnInfo.SingleReturnName ?? "result";
            return SyntaxFactory.ReturnStatement(
                SyntaxFactory.IdentifierName(variableName));
        }

        // Multiple return values (tuple)
        if (returnInfo.Kind == ReturnKind.Multiple)
        {
            var tupleArguments = returnInfo.MultipleReturns
                .Select(r => SyntaxFactory.Argument(
                    SyntaxFactory.IdentifierName(r.Name)))
                .ToArray();

            var tupleExpression = SyntaxFactory.TupleExpression(
                SyntaxFactory.SeparatedList(tupleArguments));

            return SyntaxFactory.ReturnStatement(tupleExpression);
        }

        // Fallback - no return (shouldn't reach here)
        throw new System.InvalidOperationException("Cannot generate return statement for void return type");
    }
}
