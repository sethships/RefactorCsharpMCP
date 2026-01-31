using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCsharpMCP.Core.Utilities;

namespace RefactorCsharpMCP.Core.Refactorings.IntroduceParameterObjectComponents;

/// <summary>
/// Updates method signatures to replace grouped parameters with a parameter object parameter.
/// </summary>
public class MethodSignatureUpdater
{
    /// <summary>
    /// Updates the method signature to replace grouped parameters with a parameter object.
    /// </summary>
    /// <param name="method">The method declaration to update.</param>
    /// <param name="parametersToReplace">The parameters being grouped into the parameter object.</param>
    /// <param name="parameterObjectClassName">The name of the parameter object class.</param>
    /// <param name="parameterNames">The names of parameters being replaced.</param>
    /// <returns>The updated method declaration with the new signature.</returns>
    public MethodDeclarationSyntax UpdateSignature(
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
}
