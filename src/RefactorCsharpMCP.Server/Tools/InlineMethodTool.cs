using RefactorCsharpMCP.Core;
using System.ComponentModel;
using ModelContextProtocol.Server;
using RefactorCsharpMCP.Core.Refactorings;
using RefactorCsharpMCP.Core.Validation;
using RefactorCsharpMCP.Server.Formatting;

namespace RefactorCsharpMCP.Server.Tools;

/// <summary>
/// MCP tool for inlining a method by replacing all invocations with the method's body.
/// Part 2: Supports multiple call sites with automatic identifier conflict resolution.
/// </summary>
[McpServerToolType]
public class InlineMethodTool
{
    private readonly IResponseFormatter _formatter;

    /// <summary>
    /// Creates a new InlineMethodTool with the specified response formatter.
    /// </summary>
    public InlineMethodTool(IResponseFormatter formatter)
    {
        _formatter = formatter;
    }

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
        // Input validation using shared validator
        var validation = ToolInputValidator.ValidateSourceCode(sourceCode, "Refactoring")
                         ?? ToolInputValidator.ValidateSourceCodeSize(sourceCode, "Refactoring")
                         ?? ToolInputValidator.ValidateLineNumber(lineNumber, "Refactoring")
                         ?? ToolInputValidator.ValidateColumnNumber(columnNumber, "Refactoring")
                         ?? ToolInputValidator.ValidateTargetFramework(targetFramework, "Refactoring");

        if (validation != null)
        {
            return _formatter.Format(validation);
        }

        // Execute the refactoring with framework-aware validation
        var inliner = new InlineMethod();
        var result = await inliner.ExecuteAsync(sourceCode, lineNumber, columnNumber, targetFramework);

        // Return result as an object that MCP can serialize
        if (result.IsSuccess)
        {
            return _formatter.Format(new
            {
                success = true,
                message = result.Message,
                refactoredCode = result.RefactoredCode
            });
        }
        else
        {
            return _formatter.Format(new
            {
                success = false,
                message = result.Message,
                error = result.ErrorMessage
            });
        }
    }
}
