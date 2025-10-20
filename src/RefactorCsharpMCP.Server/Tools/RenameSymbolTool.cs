using RefactorCsharpMCP.Core;
using System.ComponentModel;
using ModelContextProtocol.Server;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Server.Tools;

/// <summary>
/// MCP tool for renaming symbols (local variables, parameters, private methods, private fields).
/// </summary>
[McpServerToolType]
public class RenameSymbolTool
{
    /// <summary>
    /// Renames a symbol at the specified position throughout the file.
    /// </summary>
    /// <param name="sourceCode">The complete C# source code.</param>
    /// <param name="lineNumber">The 1-based line number of the symbol to rename.</param>
    /// <param name="columnNumber">The 1-based column number of the symbol to rename.</param>
    /// <param name="newName">The new identifier name.</param>
    /// <returns>A JSON object containing the refactored code and status.</returns>
    [McpServerTool]
    [Description("Renames a symbol (local variable, parameter, private field, or private method) at a specific position. Uses position-based resolution for precise symbol identification. Updates all references within the same file. ⚠️ LIMITATION: Single-file scope only. Cannot rename public/protected members or symbols used across multiple files.")]
    public Task<object> RenameSymbol(
        [Description("The complete C# source code")] string sourceCode,
        [Description("The 1-based line number of the symbol to rename")] int lineNumber,
        [Description("The 1-based column number of the symbol to rename")] int columnNumber,
        [Description("The new identifier name (must be a valid C# identifier)")] string newName)
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

        if (lineNumber < 1)
        {
            return Task.FromResult<object>(new
            {
                success = false,
                error = "Line number must be >= 1",
                message = "Refactoring failed: Line number must be >= 1"
            });
        }

        if (columnNumber < 1)
        {
            return Task.FromResult<object>(new
            {
                success = false,
                error = "Column number must be >= 1",
                message = "Refactoring failed: Column number must be >= 1"
            });
        }

        if (string.IsNullOrWhiteSpace(newName) ||
            !McpToolConstants.CSharpIdentifierRegex.IsMatch(newName))
        {
            return Task.FromResult<object>(new
            {
                success = false,
                error = "New name must be a valid C# identifier (start with letter or underscore, followed by letters, digits, or underscores)",
                message = "Refactoring failed: New name must be a valid C# identifier (start with letter or underscore, followed by letters, digits, or underscores)"
            });
        }

        // Execute the refactoring
        var refactoring = new RenameSymbol();
        var result = refactoring.Execute(sourceCode, lineNumber, columnNumber, newName);

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
