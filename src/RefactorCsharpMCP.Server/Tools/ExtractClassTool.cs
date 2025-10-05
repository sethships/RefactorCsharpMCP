using RefactorCsharpMCP.Core;
using System.ComponentModel;
using ModelContextProtocol.Server;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Server.Tools;

/// <summary>
/// MCP tool for extracting fields and methods into a new class.
/// </summary>
[McpServerToolType]
public class ExtractClassTool
{
    /// <summary>
    /// Extracts specified fields and methods into a new class.
    /// </summary>
    /// <param name="sourceCode">The complete C# source code.</param>
    /// <param name="className">The name of the source class.</param>
    /// <param name="newClassName">The name of the new class to create.</param>
    /// <param name="fieldNames">Comma or semicolon-separated field names to extract.</param>
    /// <param name="methodNames">Comma or semicolon-separated method names to extract (optional).</param>
    /// <returns>A JSON object containing the refactored code and status.</returns>
    [McpServerTool]
    [Description("Extracts fields and methods into a new class with composition pattern. ⚠️ IMPORTANT: This creates the new class and composition field, but you must manually update all references to extracted members. Useful for breaking down large classes and improving separation of concerns.")]
    public Task<object> ExtractClass(
        [Description("The complete C# source code")] string sourceCode,
        [Description("The name of the source class")] string className,
        [Description("The name of the new class to create")] string newClassName,
        [Description("Comma or semicolon-separated field names to extract")] string fieldNames,
        [Description("Comma or semicolon-separated method names to extract (optional)")] string? methodNames = null)
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

        if (string.IsNullOrWhiteSpace(newClassName) ||
            !McpToolConstants.CSharpIdentifierRegex.IsMatch(newClassName))
        {
            return Task.FromResult<object>(new
            {
                success = false,
                error = "New class name must be a valid C# identifier",
                message = "Refactoring failed: New class name must be a valid C# identifier"
            });
        }

        if (string.IsNullOrWhiteSpace(fieldNames))
        {
            return Task.FromResult<object>(new
            {
                success = false,
                error = "Field names cannot be empty",
                message = "Refactoring failed: At least one field name must be specified"
            });
        }

        // Execute the refactoring
        var refactoring = new ExtractClass();
        var result = refactoring.Execute(sourceCode, className, newClassName, fieldNames, methodNames);

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
