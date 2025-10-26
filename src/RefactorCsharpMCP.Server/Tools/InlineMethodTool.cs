using RefactorCsharpMCP.Core;
using System.ComponentModel;
using ModelContextProtocol.Server;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Server.Tools;

/// <summary>
/// MCP tool for inlining a method by replacing all invocations with the method's body.
/// Part 2: Supports multiple call sites with automatic identifier conflict resolution.
/// </summary>
[McpServerToolType]
public class InlineMethodTool
{
    /// <summary>
    /// Inlines a method by replacing all invocations with the method's body, then removes the method declaration.
    /// Part 2 capabilities: void methods, multiple call sites, automatic identifier conflict resolution.
    /// Simple parameters (primitives and string) are supported.
    /// </summary>
    /// <param name="sourceCode">The source code containing the method to inline.</param>
    /// <param name="lineNumber">The line number (1-based) where the method is declared.</param>
    /// <param name="columnNumber">The column number (1-based) within the line where the method name starts.</param>
    /// <param name="targetFramework">The target .NET framework (e.g., "net8.0", "net48", "netstandard2.0"). Defaults to "net8.0".</param>
    /// <returns>A JSON object containing the refactored code and status.</returns>
    [McpServerTool]
    [Description("Inlines a method by replacing all invocations with the method's body, then removes the method declaration. Part 2: Supports void methods with multiple call sites and automatic identifier conflict resolution (conflicting variables renamed with _1 suffix). Provide the source code, line and column position (1-based) of the method declaration, and optionally target framework (defaults to net8.0).")]
    public async Task<object> InlineMethod(
        [Description("The complete C# source code")] string sourceCode,
        [Description("The line number (1-based) where the method is declared")] int lineNumber,
        [Description("The column number (1-based) within the line")] int columnNumber,
        [Description("The target .NET framework (e.g., 'net8.0', 'net48', 'netstandard2.0')")] string targetFramework = "net8.0")
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

        if (sourceCode.Length > 1_000_000) // 1MB limit
        {
            return new
            {
                success = false,
                error = "Source code exceeds 1MB limit",
                message = "Refactoring failed: Source code exceeds 1MB limit"
            };
        }

        if (lineNumber < 1 || lineNumber > 100000) // Reasonable line limit
        {
            return new
            {
                success = false,
                error = "Line number must be between 1 and 100000",
                message = "Refactoring failed: Invalid line number specified"
            };
        }

        if (columnNumber < 1 || columnNumber > 10000) // Reasonable column limit
        {
            return new
            {
                success = false,
                error = "Column number must be between 1 and 10000",
                message = "Refactoring failed: Invalid column number specified"
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

        // Execute the refactoring with framework-aware validation
        var inliner = new InlineMethod();
        var result = await inliner.ExecuteAsync(sourceCode, lineNumber, columnNumber, targetFramework);

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
