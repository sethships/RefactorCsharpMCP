using RefactorCsharpMCP.Core;
using System.ComponentModel;
using ModelContextProtocol.Server;
using RefactorCsharpMCP.Core.Refactorings;
using RefactorCsharpMCP.Core.Validation;

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
        // Input validation using shared validator
        var validation = ToolInputValidator.ValidateSourceCode(sourceCode, "Refactoring")
                         ?? ToolInputValidator.ValidateSourceCodeSize(sourceCode, "Refactoring")
                         ?? ToolInputValidator.ValidateIdentifier(newMethodName, "method name", "Refactoring")
                         ?? ToolInputValidator.ValidateTargetFramework(targetFramework, "Refactoring");

        if (validation != null)
        {
            return Task.FromResult<object>(validation);
        }

        // Validate line range using shared validator first
        var lineValidation = ToolInputValidator.ValidateLineNumber(startLine, "Refactoring", 1, 100_000)
                           ?? ToolInputValidator.ValidateLineNumber(endLine, "Refactoring", 1, 100_000);
        if (lineValidation != null)
        {
            return Task.FromResult<object>(lineValidation);
        }

        // Additional range validation (tool-specific)
        if (startLine > endLine)
        {
            return Task.FromResult<object>(new
            {
                success = false,
                error = "Start line must be less than or equal to end line",
                message = $"Refactoring failed: Start line {startLine} is greater than end line {endLine}"
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
