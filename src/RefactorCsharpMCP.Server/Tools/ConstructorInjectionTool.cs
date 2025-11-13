using RefactorCsharpMCP.Core;
using System.ComponentModel;
using ModelContextProtocol.Server;
using RefactorCsharpMCP.Core.Refactorings;
using RefactorCsharpMCP.Server.Utilities;

namespace RefactorCsharpMCP.Server.Tools;

/// <summary>
/// MCP tool for converting method parameters to constructor-injected fields or properties.
/// </summary>
[McpServerToolType]
public class ConstructorInjectionTool
{
    /// <summary>
    /// Converts method parameters to constructor-injected fields or properties.
    /// </summary>
    /// <param name="sourceCode">The complete C# source code.</param>
    /// <param name="className">The name of the class containing the method.</param>
    /// <param name="methodName">The name of the method with parameters to inject.</param>
    /// <param name="parameterNames">Comma-separated list of parameter names to inject.</param>
    /// <param name="useProperties">If true, generates properties; if false, generates fields (default).</param>
    /// <returns>A JSON object containing the refactored code and status.</returns>
    [McpServerTool]
    [Description("Converts method parameters to constructor-injected fields or properties. Useful for applying dependency injection patterns.")]
    public Task<object> ConstructorInjection(
        [Description("The complete C# source code")] string sourceCode,
        [Description("The name of the class containing the method")] string className,
        [Description("The name of the method with parameters to inject")] string methodName,
        [Description("Comma-separated parameter names to inject (e.g., 'logger,config')")] string parameterNames,
        [Description("Use properties instead of fields (default: false)")] bool useProperties = false)
    {
        // Input validation using shared validator
        var validation = ToolInputValidator.ValidateSourceCode(sourceCode, "Refactoring")
                         ?? ToolInputValidator.ValidateSourceCodeSize(sourceCode, "Refactoring")
                         ?? ToolInputValidator.ValidateIdentifier(className, "class name", "Refactoring")
                         ?? ToolInputValidator.ValidateIdentifier(methodName, "method name", "Refactoring");

        if (validation != null)
        {
            return Task.FromResult<object>(validation);
        }

        // Parse parameter names
        var paramNames = parameterNames
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .ToArray();

        if (paramNames.Length == 0 || paramNames.Length > 20)
        {
            return Task.FromResult<object>(new
            {
                success = false,
                error = "Must specify between 1 and 20 parameters",
                message = "Refactoring failed: Must specify between 1 and 20 parameters"
            });
        }

        // Execute the refactoring
        var injector = new ConstructorInjection();
        var result = injector.Execute(sourceCode, className, methodName, paramNames, useProperties);

        // Return result as an object that MCP can serialize
        if (result.IsSuccess)
        {
            return Task.FromResult<object>(new
            {
                success = true,
                message = result.Message,
                refactoredCode = result.RefactoredCode,
                injectedParameters = paramNames,
                injectionType = useProperties ? "properties" : "fields"
            });
        }
        else
        {
            return Task.FromResult<object>(new
            {
                success = false,
                message = result.Message,
                error = result.ErrorMessage
            });
        }
    }
}
