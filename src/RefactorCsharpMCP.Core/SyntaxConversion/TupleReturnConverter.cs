using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorCsharpMCP.Core.SyntaxConversion;

/// <summary>
/// Placeholder converter for C# 7.0 tuple returns to out parameters.
///
/// Note: This is a placeholder implementation that demonstrates the converter architecture.
/// Full tuple return conversion requires complex trivia preservation and semantic analysis
/// to properly handle:
/// - Whitespace and formatting preservation during major syntax transformations
/// - Nested tuple types
/// - Tuple returns in expression bodies, lambdas, and local functions
/// - Proper indentation and code formatting
///
/// Intended transformations (not yet implemented):
/// - (int, string) Method() → void Method(out int item1, out string item2)
/// - (int x, string y) Method() → void Method(out int x, out string y)
/// - return (1, "test") → item1 = 1; item2 = "test"; return;
///
/// This feature targets net35 (C# 3.0) which is rarely used in modern development.
/// </summary>
public class TupleReturnConverter : SyntaxConverterBase
{
    /// <summary>
    /// Gets the name of this converter.
    /// </summary>
    public override string Name => "TupleReturnConverter";

    /// <summary>
    /// Tuple returns require C# 7.0.
    /// </summary>
    public override LanguageVersion MinimumSourceLanguageVersion => LanguageVersion.CSharp7;

    /// <summary>
    /// Frameworks with C# 6.0 or lower need conversion.
    /// </summary>
    public override LanguageVersion MaximumTargetLanguageVersion => LanguageVersion.CSharp6;

    /// <summary>
    /// Visits a method declaration to convert tuple returns to out parameters.
    /// </summary>
    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        // Check if return type is a tuple
        if (node.ReturnType is not TupleTypeSyntax tupleType)
        {
            return base.VisitMethodDeclaration(node);
        }

        // Extract tuple element names (use item1, item2, etc. if not named)
        var outParameters = new List<ParameterSyntax>();
        for (int i = 0; i < tupleType.Elements.Count; i++)
        {
            var element = tupleType.Elements[i];
            var paramName = element.Identifier.Text;
            if (string.IsNullOrEmpty(paramName))
            {
                paramName = $"item{i + 1}";
            }

            var outParam = SyntaxFactory.Parameter(
                SyntaxFactory.Identifier(paramName))
                .WithType(element.Type)
                .WithModifiers(SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.OutKeyword)));

            outParameters.Add(outParam);
        }

        // Change return type to void
        var voidType = SyntaxFactory.PredefinedType(
            SyntaxFactory.Token(SyntaxKind.VoidKeyword));

        // Add out parameters to parameter list
        var newParameters = node.ParameterList.Parameters.AddRange(outParameters);
        var newParameterList = node.ParameterList.WithParameters(newParameters);

        // Transform the method body to use out parameters instead of tuple returns
        var transformedBody = node.Body != null
            ? (BlockSyntax)VisitBlock(node.Body)!
            : null;

        var transformedExpressionBody = node.ExpressionBody != null
            ? ConvertExpressionBodyToBlock(node.ExpressionBody, outParameters)
            : null;

        // Create new method with void return and out parameters
        var newMethod = node
            .WithReturnType(voidType)
            .WithParameterList(newParameterList)
            .WithBody(transformedBody ?? transformedExpressionBody)
            .WithExpressionBody(null)
            .WithSemicolonToken(default);

        return PreserveTrivia(newMethod, node);
    }

    /// <summary>
    /// Visits a return statement to convert tuple returns to out parameter assignments.
    /// </summary>
    public override SyntaxNode? VisitReturnStatement(ReturnStatementSyntax node)
    {
        // Check if returning a tuple expression
        if (node.Expression is not TupleExpressionSyntax tupleExpression)
        {
            // If just "return;" with no expression, keep as is
            if (node.Expression == null)
            {
                return node;
            }

            return base.VisitReturnStatement(node);
        }

        // Get the containing method to find out parameter names
        var containingMethod = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (containingMethod == null)
        {
            return base.VisitReturnStatement(node);
        }

        // Get out parameters from the original tuple return type
        var outParamNames = GetOutParameterNames(containingMethod);

        // Create assignment statements for each tuple element
        var assignments = new List<StatementSyntax>();
        for (int i = 0; i < tupleExpression.Arguments.Count && i < outParamNames.Count; i++)
        {
            var argument = tupleExpression.Arguments[i];
            var paramName = outParamNames[i];

            var assignment = SyntaxFactory.ExpressionStatement(
                SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.IdentifierName(paramName),
                    argument.Expression));

            assignments.Add(assignment);
        }

        // Add empty return statement
        assignments.Add(SyntaxFactory.ReturnStatement());

        // Return the first assignment (the rest will be added by the block visitor)
        // Note: This is a limitation - we return a single statement but need multiple
        // In practice, we need to handle this at the block level
        if (assignments.Count == 1)
        {
            return assignments[0];
        }

        // Create a block containing all assignments and return
        // This works when the return is the only statement, but we need special handling
        // for returns within other control flow
        return SyntaxFactory.Block(assignments);
    }

    /// <summary>
    /// Visits a block to convert tuple return statements to out parameter assignments.
    /// </summary>
    public override SyntaxNode? VisitBlock(BlockSyntax node)
    {
        var statements = new List<StatementSyntax>();

        foreach (var statement in node.Statements)
        {
            var visited = Visit(statement);

            // If the visited node is a block (from tuple return conversion), unwrap it
            if (visited is BlockSyntax block && statement is ReturnStatementSyntax returnStmt
                && returnStmt.Expression is TupleExpressionSyntax)
            {
                // Add all statements from the block
                statements.AddRange(block.Statements);
            }
            else if (visited is StatementSyntax statementSyntax)
            {
                statements.Add(statementSyntax);
            }
        }

        return node.WithStatements(SyntaxFactory.List(statements));
    }

    /// <summary>
    /// Converts an expression body to a block with out parameter assignments.
    /// </summary>
    private BlockSyntax ConvertExpressionBodyToBlock(
        ArrowExpressionClauseSyntax expressionBody,
        List<ParameterSyntax> outParameters)
    {
        var statements = new List<StatementSyntax>();

        // Check if expression is a tuple
        if (expressionBody.Expression is TupleExpressionSyntax tupleExpression)
        {
            // Create assignments for each tuple element
            for (int i = 0; i < tupleExpression.Arguments.Count && i < outParameters.Count; i++)
            {
                var argument = tupleExpression.Arguments[i];
                var paramName = outParameters[i].Identifier.Text;

                var assignment = SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        SyntaxFactory.IdentifierName(paramName),
                        argument.Expression));

                statements.Add(assignment);
            }

            // Add return statement
            statements.Add(SyntaxFactory.ReturnStatement());
        }
        else
        {
            // Not a tuple expression, just convert to statement
            statements.Add(SyntaxFactory.ExpressionStatement(expressionBody.Expression));
            statements.Add(SyntaxFactory.ReturnStatement());
        }

        return SyntaxFactory.Block(statements);
    }

    /// <summary>
    /// Gets out parameter names from a method with tuple return type.
    /// </summary>
    private List<string> GetOutParameterNames(MethodDeclarationSyntax method)
    {
        var names = new List<string>();

        if (method.ReturnType is TupleTypeSyntax tupleType)
        {
            for (int i = 0; i < tupleType.Elements.Count; i++)
            {
                var element = tupleType.Elements[i];
                var name = element.Identifier.Text;
                if (string.IsNullOrEmpty(name))
                {
                    name = $"item{i + 1}";
                }
                names.Add(name);
            }
        }

        return names;
    }
}
