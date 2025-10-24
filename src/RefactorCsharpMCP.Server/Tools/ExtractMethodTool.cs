using RefactorCsharpMCP.Core;
using System.ComponentModel;
using ModelContextProtocol.Server;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Server.Tools;

/// <summary>
/// MCP tool for extracting code into a new method.
/// </summary>
[McpServerToolType]
public class ExtractMethodTool
{
    /// <summary>
    /// Extracts the specified lines of code into a new method.
    /// </summary>
    /// <param name="sourceCode">The source code containing the code to extract.</param>
    /// <param name="startLine">The starting line number (1-based) of the code to extract.</param>
    /// <param name="endLine">The ending line number (1-based) of the code to extract.</param>
    /// <param name="newMethodName">The name for the new extracted method.</param>
    /// <param name="targetFramework">The target .NET framework (e.g., "net8.0", "net48", "netstandard2.0"). Defaults to "net8.0".</param>
    /// <returns>A JSON object containing the refactored code and status.</returns>
    [McpServerTool]
    [Description("Extracts a block of code into a new private method with framework-aware return type detection. Provide the source code, line range (1-based), desired method name, and optionally target framework (defaults to net8.0).")]
    public Task<object> ExtractMethod(
        [Description("The complete C# source code")] string sourceCode,
        [Description("The starting line number (1-based) to extract")] int startLine,
        [Description("The ending line number (1-based) to extract")] int endLine,
        [Description("The name for the new method")] string newMethodName,
        [Description("The target .NET framework (e.g., 'net8.0', 'net48', 'netstandard2.0')")] string targetFramework = "net8.0")
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

        if (sourceCode.Length > 1_000_000) // 1MB limit
        {
            return Task.FromResult<object>(new
            {
                success = false,
                error = "Source code exceeds 1MB limit",
                message = "Refactoring failed: Source code exceeds 1MB limit"
            });
        }

        if (string.IsNullOrWhiteSpace(newMethodName) ||
            !McpToolConstants.CSharpIdentifierRegex.IsMatch(newMethodName))
        {
            return Task.FromResult<object>(new
            {
                success = false,
                error = "Method name must be a valid C# identifier",
                message = "Refactoring failed: Method name must be a valid C# identifier"
            });
        }

        if (startLine < 1 || endLine < 1 || endLine > 100000) // Reasonable line limit
        {
            return Task.FromResult<object>(new
            {
                success = false,
                error = "Invalid line range specified",
                message = "Refactoring failed: Invalid line range specified"
            });
        }

        if (string.IsNullOrWhiteSpace(targetFramework))
        {
            return Task.FromResult<object>(new
            {
                success = false,
                error = "Target framework cannot be empty",
                message = "Refactoring failed: Target framework cannot be empty"
            });
        }

        // Execute the refactoring
        var extractor = new ExtractMethod();
        var result = extractor.Execute(sourceCode, startLine, endLine, newMethodName, targetFramework);

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
