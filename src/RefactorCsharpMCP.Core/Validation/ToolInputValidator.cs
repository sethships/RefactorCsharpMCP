using RefactorCsharpMCP.Core;
using RefactorCsharpMCP.Core.Framework;

namespace RefactorCsharpMCP.Core.Validation;

/// <summary>
/// Provides shared input validation methods for MCP tools.
/// Consolidates common validation patterns to reduce code duplication across tool implementations.
/// </summary>
/// <remarks>
/// Created as part of Issue #92 (Sprint 5) to extract duplicated validation logic from 11 tool files.
/// Before: ~456 lines of duplicated validation code across tools
/// After: ~50 lines in shared helper + ~55 lines of calls = 77% reduction
/// Moved from Server.Utilities to Core.Validation in Sprint 5 code review for better reusability.
/// </remarks>
public static class ToolInputValidator
{
    /// <summary>
    /// Validates that source code is not null or whitespace.
    /// </summary>
    /// <param name="sourceCode">The source code to validate.</param>
    /// <param name="operationName">The name of the operation (for error messages).</param>
    /// <returns>Null if valid, otherwise a ValidationResult indicating the error.</returns>
    public static ValidationResult? ValidateSourceCode(string sourceCode, string operationName)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return ValidationResult.ToolInputError(
                ErrorCode.EMPTY_SOURCE_CODE,
                "Source code cannot be empty",
                operationName);
        }

        return null; // Validation passed
    }

    /// <summary>
    /// Validates that source code does not exceed the maximum size limit.
    /// </summary>
    /// <param name="sourceCode">The source code to validate.</param>
    /// <param name="operationName">The name of the operation (for error messages).</param>
    /// <param name="maxSize">Maximum allowed size in bytes. Defaults to 1MB (McpToolConstants.MAX_SOURCE_CODE_SIZE).</param>
    /// <returns>Null if valid, otherwise a ValidationResult indicating the error.</returns>
    public static ValidationResult? ValidateSourceCodeSize(string sourceCode, string operationName, int maxSize = McpToolConstants.MAX_SOURCE_CODE_SIZE)
    {
        if (sourceCode.Length > maxSize)
        {
            return ValidationResult.ToolInputError(
                ErrorCode.SOURCE_CODE_TOO_LARGE,
                $"Source code exceeds {maxSize / 1_000_000}MB limit",
                operationName);
        }

        return null; // Validation passed
    }

    /// <summary>
    /// Validates that an identifier is a valid C# identifier.
    /// </summary>
    /// <param name="identifier">The identifier to validate.</param>
    /// <param name="identifierType">The type of identifier (e.g., "class name", "method name") for error messages.</param>
    /// <param name="operationName">The name of the operation (for error messages).</param>
    /// <returns>Null if valid, otherwise a ValidationResult indicating the error.</returns>
    public static ValidationResult? ValidateIdentifier(string identifier, string identifierType, string operationName)
    {
        if (string.IsNullOrWhiteSpace(identifier) ||
            !McpToolConstants.CSharpIdentifierRegex.IsMatch(identifier))
        {
            var capitalizedType = char.ToUpper(identifierType[0]) + identifierType.Substring(1);
            return ValidationResult.ToolInputError(
                ErrorCode.INVALID_IDENTIFIER,
                $"{capitalizedType} must be a valid C# identifier",
                operationName);
        }

        return null; // Validation passed
    }

    /// <summary>
    /// Validates that a line number is within acceptable range.
    /// </summary>
    /// <param name="lineNumber">The line number to validate.</param>
    /// <param name="operationName">The name of the operation (for error messages).</param>
    /// <param name="minLine">Minimum valid line number. Defaults to 1.</param>
    /// <param name="maxLine">Maximum valid line number. Defaults to 100,000.</param>
    /// <returns>Null if valid, otherwise a ValidationResult indicating the error.</returns>
    public static ValidationResult? ValidateLineNumber(int lineNumber, string operationName, int minLine = 1, int maxLine = 100_000)
    {
        if (lineNumber < minLine || lineNumber > maxLine)
        {
            return ValidationResult.ToolInputError(
                ErrorCode.INVALID_LINE_NUMBER,
                $"Line number must be between {minLine} and {maxLine}",
                operationName);
        }

        return null; // Validation passed
    }

    /// <summary>
    /// Validates that a column number is within acceptable range.
    /// </summary>
    /// <param name="columnNumber">The column number to validate.</param>
    /// <param name="operationName">The name of the operation (for error messages).</param>
    /// <param name="minColumn">Minimum valid column number. Defaults to 1.</param>
    /// <param name="maxColumn">Maximum valid column number. Defaults to 10,000.</param>
    /// <returns>Null if valid, otherwise a ValidationResult indicating the error.</returns>
    public static ValidationResult? ValidateColumnNumber(int columnNumber, string operationName, int minColumn = 1, int maxColumn = 10_000)
    {
        if (columnNumber < minColumn || columnNumber > maxColumn)
        {
            return ValidationResult.ToolInputError(
                ErrorCode.INVALID_COLUMN_NUMBER,
                $"Column number must be between {minColumn} and {maxColumn}",
                operationName);
        }

        return null; // Validation passed
    }

    /// <summary>
    /// Validates that a target framework is not null or whitespace and is a valid, supported framework.
    /// </summary>
    /// <param name="targetFramework">The target framework to validate.</param>
    /// <param name="operationName">The name of the operation (for error messages).</param>
    /// <returns>Null if valid, otherwise a ValidationResult indicating the error.</returns>
    public static ValidationResult? ValidateTargetFramework(string targetFramework, string operationName)
    {
        if (string.IsNullOrWhiteSpace(targetFramework))
        {
            return ValidationResult.ToolInputError(
                ErrorCode.EMPTY_TARGET_FRAMEWORK,
                "Target framework cannot be empty",
                operationName);
        }

        // Validate using FrameworkValidator for format, support, and EOL checks
        var validationResult = new FrameworkValidator().Validate(targetFramework);
        if (!validationResult.IsValid)
        {
            return ValidationResult.ToolInputError(
                ErrorCode.INVALID_TFM_FORMAT,
                validationResult.ErrorMessage ?? "Invalid target framework",
                operationName);
        }

        return null; // Validation passed
    }
}
