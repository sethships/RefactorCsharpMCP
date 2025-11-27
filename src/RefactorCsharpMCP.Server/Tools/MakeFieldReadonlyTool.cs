using RefactorCsharpMCP.Core;
using System.ComponentModel;
using ModelContextProtocol.Server;
using RefactorCsharpMCP.Core.Refactorings;
using RefactorCsharpMCP.Core.Validation;
using RefactorCsharpMCP.Server.Formatting;

namespace RefactorCsharpMCP.Server.Tools;

/// <summary>
/// MCP tool for making fields readonly.
/// </summary>
[McpServerToolType]
public class MakeFieldReadonlyTool
{
    private readonly IResponseFormatter _formatter;

    /// <summary>
    /// Creates a new MakeFieldReadonlyTool with the specified response formatter.
    /// </summary>
    public MakeFieldReadonlyTool(IResponseFormatter formatter)
    {
        _formatter = formatter;
    }

    /// <summary>
    /// Makes the specified field readonly if it's only assigned in constructors.
    /// </summary>
    /// <param name="sourceCode">The complete C# source code.</param>
    /// <param name="className">The name of the class containing the field.</param>
    /// <param name="fieldName">The name of the field to make readonly.</param>
    /// <param name="targetFramework">The target .NET framework (e.g., "net8.0", "net48", "netstandard2.0"). Defaults to "net8.0".</param>
    /// <returns>A JSON object containing the refactored code and status.</returns>
    [McpServerTool]
    [Description("Makes a field readonly if it is only assigned in constructors. ⚠️ LIMITATION: Does not detect C# 7.0+ local function captures. Improves immutability and prevents accidental modifications.")]
    public Task<object> MakeFieldReadonly(
        [Description("The complete C# source code")] string sourceCode,
        [Description("The name of the class containing the field")] string className,
        [Description("The name of the field to make readonly")] string fieldName,
        [Description("The target .NET framework (e.g., 'net8.0', 'net48', 'netstandard2.0')")] string targetFramework = "net8.0")
    {
        // Input validation using shared validator
        var validation = ToolInputValidator.ValidateSourceCode(sourceCode, "Refactoring")
                         ?? ToolInputValidator.ValidateSourceCodeSize(sourceCode, "Refactoring")
                         ?? ToolInputValidator.ValidateIdentifier(className, "class name", "Refactoring")
                         ?? ToolInputValidator.ValidateIdentifier(fieldName, "field name", "Refactoring")
                         ?? ToolInputValidator.ValidateTargetFramework(targetFramework, "Refactoring");

        if (validation != null)
        {
            return Task.FromResult(_formatter.Format(validation));
        }

        // Execute the refactoring
        var refactoring = new MakeFieldReadonly();
        var result = refactoring.Execute(sourceCode, className, fieldName, targetFramework);

        // Return result as an object that MCP can serialize
        if (result.IsSuccess)
        {
            return Task.FromResult(_formatter.Format(new
            {
                success = true,
                message = result.Message,
                refactoredCode = result.RefactoredCode
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
