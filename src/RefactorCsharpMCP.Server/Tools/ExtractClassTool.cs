using RefactorCsharpMCP.Core;
using System.ComponentModel;
using ModelContextProtocol.Server;
using RefactorCsharpMCP.Core.Refactorings;
using RefactorCsharpMCP.Server.Utilities;

namespace RefactorCsharpMCP.Server.Tools;

/// <summary>
/// MCP tool for extracting fields and methods into a new class.
/// </summary>
[McpServerToolType]
public class ExtractClassTool
{
    /// <summary>
    /// Extracts specified fields, methods, and nested types into a new class with optional compilation validation.
    /// </summary>
    /// <param name="sourceCode">The complete C# source code.</param>
    /// <param name="className">The name of the source class.</param>
    /// <param name="newClassName">The name of the new class to create.</param>
    /// <param name="fieldNames">Comma or semicolon-separated field names to extract (optional if methodNames or nestedTypeNames provided).</param>
    /// <param name="methodNames">Comma or semicolon-separated method names to extract (optional if fieldNames or nestedTypeNames provided).</param>
    /// <param name="nestedTypeNames">Comma or semicolon-separated nested type names to extract (optional).</param>
    /// <param name="targetFramework">Target .NET framework for validation (e.g., "net8.0", "net48", "netstandard2.0"). Default: "net8.0".</param>
    /// <param name="validateCompilation">Enable compilation validation with framework-specific BCL references. Default: false. Validates that extracted code compiles successfully when enabled.</param>
    /// <returns>A JSON object containing the refactored code and status.</returns>
    [McpServerTool]
    [Description("Extracts fields, methods, and/or nested types into a new class with composition pattern. Supports service class extraction (methods-only) and nested type extraction. ⚠️ IMPORTANT: This creates the new class and composition field, automatically updating references to extracted members within the same class. Qualified nested type references (e.g., OriginalClass.NestedType) are transformed to NewClass.NestedType. Includes optional compilation validation with framework-specific BCL references (disabled by default, can be explicitly enabled). Useful for breaking down large classes and improving separation of concerns.")]
    public async Task<object> ExtractClass(
        [Description("The complete C# source code")] string sourceCode,
        [Description("The name of the source class")] string className,
        [Description("The name of the new class to create")] string newClassName,
        [Description("Comma or semicolon-separated field names to extract (optional if methodNames or nestedTypeNames provided)")] string fieldNames = "",
        [Description("Comma or semicolon-separated method names to extract (optional if fieldNames or nestedTypeNames provided)")] string? methodNames = null,
        [Description("Comma or semicolon-separated nested type names to extract (optional)")] string? nestedTypeNames = null,
        [Description("Target .NET framework for validation (e.g., 'net8.0', 'net48', 'netstandard2.0'). Default: 'net8.0'")] string targetFramework = "net8.0",
        [Description("Enable compilation validation with framework-specific BCL references. Default: false. When enabled, validates that extracted code compiles successfully with complete BCL references for the target framework.")] bool validateCompilation = false)
    {
        // Input validation using shared validator
        var validation = ToolInputValidator.ValidateSourceCode(sourceCode, "Refactoring")
                         ?? ToolInputValidator.ValidateSourceCodeSize(sourceCode, "Refactoring")
                         ?? ToolInputValidator.ValidateIdentifier(className, "class name", "Refactoring")
                         ?? ToolInputValidator.ValidateIdentifier(newClassName, "new class name", "Refactoring");

        if (validation != null)
        {
            return validation;
        }

        // Execute the refactoring with optional compilation validation
        // (Core layer validates that at least one of fieldNames, methodNames, or nestedTypeNames is provided)
        var refactoring = new ExtractClass();
        var result = await refactoring.ExecuteAsync(
            sourceCode,
            className,
            newClassName,
            fieldNames,
            targetFramework,
            validateCompilation,
            methodNames,
            nestedTypeNames);

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
