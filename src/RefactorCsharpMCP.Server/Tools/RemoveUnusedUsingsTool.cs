using RefactorCsharpMCP.Core;
using System.ComponentModel;
using ModelContextProtocol.Server;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Server.Tools;

/// <summary>
/// MCP tool for removing unused using directives from C# source code.
/// </summary>
[McpServerToolType]
public class RemoveUnusedUsingsTool
{
    /// <summary>
    /// Removes unused using directives from C# source code.
    /// Framework-aware: preserves global using directives (C# 10+) for net8.0, net9.0.
    /// </summary>
    /// <param name="sourceCode">The complete C# source code.</param>
    /// <param name="targetFramework">The target .NET framework (e.g., "net8.0", "net48", "netstandard2.0").</param>
    /// <returns>A JSON object containing the refactored code and status.</returns>
    [McpServerTool]
    [Description("Removes unused using directives detected via Roslyn diagnostics (IDE0005, CS8019). Framework-aware: preserves global using directives (C# 10+) for modern .NET frameworks. Validates code against target framework to ensure compatibility.")]
    public async Task<object> RemoveUnusedUsings(
        [Description("The complete C# source code")] string sourceCode,
        [Description("The target .NET framework (e.g., 'net8.0', 'net48', 'netstandard2.0')")] string targetFramework)
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return new
            {
                success = false,
                error = "Source code cannot be empty",
                message = "Refactoring failed: Source code cannot be empty"
            };
        }

        if (sourceCode.Length > McpToolConstants.MAX_SOURCE_CODE_SIZE)
        {
            return new
            {
                success = false,
                error = "Source code exceeds 1MB limit",
                message = "Refactoring failed: Source code exceeds 1MB limit"
            };
        }

        if (string.IsNullOrWhiteSpace(targetFramework))
        {
            return new
            {
                success = false,
                error = "Target framework cannot be empty",
                message = "Refactoring failed: Target framework cannot be empty"
            };
        }

        // Execute the refactoring
        var refactoring = new RemoveUnusedUsings();
        var result = await refactoring.ExecuteAsync(sourceCode, targetFramework);

        // Return result as an object that MCP can serialize
        if (result.IsSuccess)
        {
            return new
            {
                success = true,
                message = result.Message,
                refactoredCode = result.RefactoredCode
            };
        }
        else
        {
            return new
            {
                success = false,
                message = result.Message,
                error = result.ErrorMessage
            };
        }
    }
}
