using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorCsharpMCP.Core.Refactorings.IntroduceParameterObjectComponents;

/// <summary>
/// Syntax rewriter that updates method invocations to pass parameter objects.
/// Uses name and argument count matching to avoid SyntaxTree identity issues.
/// Includes signature validation to prevent matching wrong method overloads.
/// </summary>
public class InvocationTransformer : CSharpSyntaxRewriter
{
    private readonly string _targetMethodName;
    private readonly HashSet<string> _parameterNamesToGroup;
    private readonly List<string> _originalParameterOrder;  // All parameters in order
    private readonly HashSet<string> _allParameterNames;    // For validating named arguments
    private readonly string _parameterObjectClassName;
    private readonly bool _useRecord;

    /// <summary>
    /// Initializes a new instance of the InvocationTransformer.
    /// </summary>
    /// <param name="targetMethod">The method symbol for the method being refactored.</param>
    /// <param name="parameterSymbols">The parameter symbols being grouped into the parameter object.</param>
    /// <param name="parameterObjectClassName">The name of the parameter object class.</param>
    /// <param name="useRecord">True if using record syntax (C# 9+), false for class syntax.</param>
    public InvocationTransformer(
        IMethodSymbol targetMethod,
        List<IParameterSymbol> parameterSymbols,
        string parameterObjectClassName,
        bool useRecord)
    {
        _targetMethodName = targetMethod.Name;
        _parameterNamesToGroup = new HashSet<string>(parameterSymbols.Select(p => p.Name));
        _originalParameterOrder = targetMethod.Parameters.Select(p => p.Name).ToList();
        _allParameterNames = new HashSet<string>(_originalParameterOrder);
        _parameterObjectClassName = parameterObjectClassName;
        _useRecord = useRecord;
    }

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        // Extract method name from invocation
        string? methodName = node.Expression switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
            _ => null
        };

        // Match by method name and original argument count
        if (methodName == _targetMethodName &&
            node.ArgumentList.Arguments.Count == _originalParameterOrder.Count)
        {
            var arguments = node.ArgumentList.Arguments;

            // Signature validation: Verify named arguments match expected parameter names
            // This helps distinguish between overloaded methods with same name/count
            if (!ValidateArgumentSignature(arguments))
            {
                return base.VisitInvocationExpression(node);
            }

            var argumentsToGroup = new List<ArgumentSyntax>();
            var remainingArguments = new List<ArgumentSyntax>();

            // Process each argument positionally or by name
            for (int i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];
                string paramName;

                if (argument.NameColon != null)
                {
                    // Named argument
                    paramName = argument.NameColon.Name.Identifier.Text;
                }
                else
                {
                    // Positional argument - map to parameter by position
                    paramName = _originalParameterOrder[i];
                }

                // Check if this parameter should be grouped
                if (_parameterNamesToGroup.Contains(paramName))
                {
                    argumentsToGroup.Add(argument);
                }
                else
                {
                    remainingArguments.Add(argument);
                }
            }

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
    /// Validates that the invocation's arguments match the expected method signature.
    /// This helps distinguish between overloaded methods with the same name and argument count.
    /// </summary>
    /// <param name="arguments">The arguments from the invocation expression.</param>
    /// <returns>True if the arguments appear to match the target method signature.</returns>
    private bool ValidateArgumentSignature(SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        // Check all named arguments reference valid parameter names
        foreach (var argument in arguments)
        {
            if (argument.NameColon != null)
            {
                var namedParam = argument.NameColon.Name.Identifier.Text;
                if (!_allParameterNames.Contains(namedParam))
                {
                    // Named argument doesn't match any parameter - wrong overload
                    return false;
                }
            }
        }

        return true;
    }
}
