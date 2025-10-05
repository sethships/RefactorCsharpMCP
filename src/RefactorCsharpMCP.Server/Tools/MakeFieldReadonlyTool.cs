using RefactorCsharpMCP.Core;
using System.ComponentModel;
using ModelContextProtocol.Server;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Server.Tools;

/// <summary>
/// MCP tool for making fields readonly.
/// </summary>
[McpServerToolType]
public class MakeFieldReadonlyTool
{
    /// <summary>
    /// Makes the specified field readonly if it's only assigned in constructors.
    /// </summary>
    /// <param name="sourceCode">The complete C# source code.</param>
    /// <param name="className">The name of the class containing the field.</param>
    /// <param name="fieldName">The name of the field to make readonly.</param>
    /// <returns>A JSON object containing the refactored code and status.</returns>
    [McpServerTool]
    [Description("Makes a field readonly if it is only assigned in constructors. ⚠️ LIMITATION: Does not detect C# 7.0+ local function captures. Improves immutability and prevents accidental modifications.")]
    public Task<object> MakeFieldReadonly(
        [Description("The complete C# source code")] string sourceCode,
        [Description("The name of the class containing the field")] string className,
        [Description("The name of the field to make readonly")] string fieldName)
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return Task.FromResult<object>(new
            {
                success = false,
                error = "Source code cannot be empty",
                message = "Refactoring failed: Source code cannot be empty"
            });
        }

        if (sourceCode.Length > McpToolConstants.MAX_SOURCE_CODE_SIZE)
        {
            return Task.FromResult<object>(new
            {
                success = false,
                error = "Source code exceeds 1MB limit",
                message = "Refactoring failed: Source code exceeds 1MB limit"
            });
        }

        if (string.IsNullOrWhiteSpace(className) ||
            !McpToolConstants.CSharpIdentifierRegex.IsMatch(className))
        {
            return Task.FromResult<object>(new
            {
                success = false,
                error = "Class name must be a valid C# identifier",
                message = "Refactoring failed: Class name must be a valid C# identifier"
            });
        }

        if (string.IsNullOrWhiteSpace(fieldName) ||
            !McpToolConstants.CSharpIdentifierRegex.IsMatch(fieldName))
        {
            return Task.FromResult<object>(new
            {
                success = false,
                error = "Field name must be a valid C# identifier",
                message = "Refactoring failed: Field name must be a valid C# identifier"
            });
        }

        // Execute the refactoring
        var refactoring = new MakeFieldReadonly();
        var result = refactoring.Execute(sourceCode, className, fieldName);

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
