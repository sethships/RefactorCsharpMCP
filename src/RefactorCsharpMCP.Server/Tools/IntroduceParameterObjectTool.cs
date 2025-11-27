using RefactorCsharpMCP.Core;
using System.ComponentModel;
using ModelContextProtocol.Server;
using RefactorCsharpMCP.Core.Refactorings;
using RefactorCsharpMCP.Core.Validation;
using RefactorCsharpMCP.Server.Formatting;

namespace RefactorCsharpMCP.Server.Tools;

/// <summary>
/// MCP tool for introducing parameter objects to replace groups of related parameters.
/// </summary>
[McpServerToolType]
public class IntroduceParameterObjectTool
{
    private readonly IResponseFormatter _formatter;

    /// <summary>
    /// Creates a new IntroduceParameterObjectTool with the specified response formatter.
    /// </summary>
    public IntroduceParameterObjectTool(IResponseFormatter formatter)
    {
        _formatter = formatter;
    }

    /// <summary>
    /// Replaces a group of method parameters with a parameter object.
    /// Generates framework-aware parameter objects (record for .NET 8+, class for .NET Framework 4.8).
    /// </summary>
    /// <param name="sourceCode">The complete C# source code.</param>
    /// <param name="className">The name of the class containing the method.</param>
    /// <param name="methodName">The name of the method with parameters to group.</param>
    /// <param name="parameterNames">Comma-separated list of parameter names to group into the parameter object.</param>
    /// <param name="newClassName">The name for the new parameter object class.</param>
    /// <param name="targetFramework">The target .NET framework (e.g., "net8.0", "net48", "netstandard2.0"). Defaults to "net8.0".</param>
    /// <returns>A JSON object containing the refactored code and status.</returns>
    [McpServerTool]
    [Description("Replaces a group of method parameters with a parameter object. Generates framework-aware classes (record for .NET 8+, class for .NET Framework 4.8).")]
    public Task<object> IntroduceParameterObject(
        [Description("The complete C# source code")] string sourceCode,
        [Description("The name of the class containing the method")] string className,
        [Description("The name of the method with parameters to group")] string methodName,
        [Description("Comma-separated parameter names to group (e.g., 'street,city,zip')")] string parameterNames,
        [Description("The name for the new parameter object class")] string newClassName,
        [Description("The target .NET framework (e.g., 'net8.0', 'net48', 'netstandard2.0')")] string targetFramework = "net8.0")
    {
        // Input validation using shared validator
        var validation = ToolInputValidator.ValidateSourceCode(sourceCode, "Refactoring")
                         ?? ToolInputValidator.ValidateSourceCodeSize(sourceCode, "Refactoring")
                         ?? ToolInputValidator.ValidateIdentifier(className, "class name", "Refactoring")
                         ?? ToolInputValidator.ValidateIdentifier(methodName, "method name", "Refactoring")
                         ?? ToolInputValidator.ValidateIdentifier(newClassName, "new class name", "Refactoring")
                         ?? ToolInputValidator.ValidateTargetFramework(targetFramework, "Refactoring");

        if (validation != null)
        {
            return Task.FromResult(_formatter.Format(validation));
        }

        // Parse parameter names
        var paramNames = parameterNames
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .ToArray();

        if (paramNames.Length == 0 || paramNames.Length > 20)
        {
            return Task.FromResult(_formatter.Format(new
            {
                success = false,
                error = "Must specify between 1 and 20 parameters",
                message = "Refactoring failed: Must specify between 1 and 20 parameters"
            }));
        }

        // Execute the refactoring
        var refactoring = new IntroduceParameterObject();
        var result = refactoring.Execute(sourceCode, className, methodName, paramNames, newClassName, targetFramework);

        // Return result as an object that MCP can serialize
        if (result.IsSuccess)
        {
            return Task.FromResult(_formatter.Format(new
            {
                success = true,
                message = result.Message,
                refactoredCode = result.RefactoredCode,
                parameterObjectClassName = newClassName,
                groupedParameters = paramNames,
                targetFramework = targetFramework
            }));
        }
        else
        {
            return Task.FromResult(_formatter.Format(new
            {
                success = false,
                message = result.Message,
                error = result.ErrorMessage
            }));
        }
    }
}
