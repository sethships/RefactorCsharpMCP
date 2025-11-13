using RefactorCsharpMCP.Core;
using System.ComponentModel;
using ModelContextProtocol.Server;
using RefactorCsharpMCP.Core.Refactorings;
using RefactorCsharpMCP.Server.Utilities;

namespace RefactorCsharpMCP.Server.Tools;

/// <summary>
/// MCP tool for safely deleting code elements.
/// </summary>
[McpServerToolType]
public class SafeDeleteTool
{
    /// <summary>
    /// Safely deletes a method if it has no references.
    /// </summary>
    /// <param name="sourceCode">The complete C# source code.</param>
    /// <param name="className">The name of the class containing the method.</param>
    /// <param name="methodName">The name of the method to delete.</param>
    /// <returns>A JSON object containing the refactored code and status.</returns>
    [McpServerTool]
    [Description("Safely deletes a method after verifying it has no references WITHIN THE SAME FILE. ⚠️ LIMITATION: Only checks single-file references. For multi-file projects, verify no cross-file references exist before using. Prevents breaking changes by checking for dependencies.")]
    public Task<object> SafeDeleteMethod(
        [Description("The complete C# source code")] string sourceCode,
        [Description("The name of the class containing the method")] string className,
        [Description("The name of the method to delete")] string methodName)
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

        // Execute the refactoring
        var refactoring = new SafeDelete();
        var result = refactoring.Execute(sourceCode, className, methodName);

        // Return result as an object that MCP can serialize
        if (result.IsSuccess)
        {
            return Task.FromResult<object>(new
            {
                success = true,
                message = result.Message,
                refactoredCode = result.RefactoredCode
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
