using RefactorCsharpMCP.Core;
using System.ComponentModel;
using ModelContextProtocol.Server;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Server.Tools;

/// <summary>
/// MCP tool for inlining a variable by replacing all uses with its initialization expression.
/// Maps to Roslyn diagnostics IDE0059 (unnecessary value assignment) and IDE0058 (expression value never used).
/// </summary>
[McpServerToolType]
public class InlineVariableTool
{
    /// <summary>
    /// Inlines a variable by replacing all its uses with its initialization expression.
    /// </summary>
    /// <param name="sourceCode">The source code containing the variable to inline.</param>
    /// <param name="lineNumber">The line number (1-based) where the variable is declared.</param>
    /// <param name="columnNumber">The column number (1-based) within the line where the variable name starts.</param>
    /// <param name="targetFramework">The target .NET framework (e.g., "net8.0", "net48", "netstandard2.0"). Defaults to "net8.0".</param>
    /// <returns>A JSON object containing the refactored code and status.</returns>
    [McpServerTool]
    [Description("Inlines a variable by replacing all its uses with its initialization expression, then removes the variable declaration. Maps to Roslyn diagnostics IDE0059 and IDE0058. Provide the source code, line and column position (1-based) of the variable declaration, and optionally target framework (defaults to net8.0).")]
    public async Task<object> InlineVariable(
        [Description("The complete C# source code")] string sourceCode,
        [Description("The line number (1-based) where the variable is declared")] int lineNumber,
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
        var inliner = new InlineVariable();
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
