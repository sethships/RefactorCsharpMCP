using RefactorCsharpMCP.Core;
using System.ComponentModel;
using ModelContextProtocol.Server;
using RefactorCsharpMCP.Core.Refactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorCsharpMCP.Server.Tools;

/// <summary>
/// MCP tool for automatically fixing Roslyn diagnostics by dispatching to appropriate refactoring tools.
/// </summary>
[McpServerToolType]
public class FixDiagnosticTool
{
    /// <summary>
    /// Automatically fixes a specific Roslyn diagnostic by applying the appropriate refactoring.
    /// </summary>
    /// <param name="sourceCode">The complete C# source code containing the diagnostic.</param>
    /// <param name="diagnosticId">The Roslyn diagnostic ID to fix (e.g., "IDE0005", "IDE0044", "CS8019").</param>
    /// <param name="line">The line number where the diagnostic occurs (1-based).</param>
    /// <param name="column">The column number where the diagnostic occurs (1-based).</param>
    /// <param name="targetFramework">The target .NET framework (e.g., "net8.0", "net48", "netstandard2.0").</param>
    /// <returns>A JSON object containing the refactored code or error information.</returns>
    [McpServerTool]
    [Description("Automatically fixes a specific Roslyn diagnostic by applying the appropriate refactoring. Supports: IDE0005/CS8019 (unused usings), IDE0044 (readonly fields). Framework-aware: applies fixes according to target framework capabilities.")]
    public async Task<object> FixDiagnostic(
        [Description("The complete C# source code containing the diagnostic")] string sourceCode,
        [Description("The Roslyn diagnostic ID to fix (e.g., 'IDE0005', 'IDE0044', 'CS8019')")] string diagnosticId,
        [Description("The line number where the diagnostic occurs (1-based)")] int line,
        [Description("The column number where the diagnostic occurs (1-based)")] int column,
        [Description("The target .NET framework (e.g., 'net8.0', 'net48', 'netstandard2.0')")] string targetFramework)
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return new
            {
                success = false,
                error = "Source code cannot be empty",
                message = "Fix failed: Source code cannot be empty"
            };
        }

        if (sourceCode.Length > McpToolConstants.MAX_SOURCE_CODE_SIZE)
        {
            return new
            {
                success = false,
                error = "Source code exceeds 1MB limit",
                message = "Fix failed: Source code exceeds 1MB limit"
            };
        }

        if (string.IsNullOrWhiteSpace(diagnosticId))
        {
            return new
            {
                success = false,
                error = "Diagnostic ID cannot be empty",
                message = "Fix failed: Diagnostic ID cannot be empty"
            };
        }

        // Validate diagnostic ID pattern
        if (!IsValidDiagnosticIdPattern(diagnosticId, out var validationError))
        {
            return new
            {
                success = false,
                error = validationError,
                message = $"Fix failed: {validationError}"
            };
        }

        if (string.IsNullOrWhiteSpace(targetFramework))
        {
            return new
            {
                success = false,
                error = "Target framework cannot be empty",
                message = "Fix failed: Target framework cannot be empty"
            };
        }

        // Dispatch to appropriate refactoring based on diagnostic ID
        try
        {
            RefactoringResult result = diagnosticId.ToUpperInvariant() switch
            {
                // Unused using directives
                "IDE0005" or "CS8019" => await FixUnusedUsings(sourceCode, targetFramework),

                // Field can be made readonly
                "IDE0044" => await FixReadonlyField(sourceCode, line, column, targetFramework),

                // Unsupported diagnostic
                // TODO (#49): Add support for additional diagnostics in future versions:
                // - IDE0001, IDE0002: Simplify name/member access
                // - IDE0022: Use expression body
                // - CA diagnostics: Code analysis rules
                // See https://github.com/sethb75/RefactorCsharpMCP/issues/49
                _ => RefactoringResult.Failure(
                    $"No refactoring available for diagnostic '{diagnosticId}'. " +
                    $"Supported diagnostics: IDE0005 (unused usings), CS8019 (unused usings), IDE0044 (readonly fields). " +
                    "See documentation for planned diagnostic support.")
            };

            // Return result
            if (result.IsSuccess)
            {
                return new
                {
                    success = true,
                    message = result.Message,
                    refactoredCode = result.RefactoredCode,
                    diagnosticId = diagnosticId,
                    appliedRefactoring = GetRefactoringName(diagnosticId)
                };
            }
            else
            {
                return new
                {
                    success = false,
                    message = result.Message,
                    error = result.ErrorMessage,
                    diagnosticId = diagnosticId
                };
            }
        }
        catch (Exception ex)
        {
            return new
            {
                success = false,
                message = $"Unexpected error applying fix for {diagnosticId}: {ex.Message}",
                error = ex.Message,
                diagnosticId = diagnosticId
            };
        }
    }

    /// <summary>
    /// Fixes unused using directives (IDE0005, CS8019).
    /// </summary>
    private async Task<RefactoringResult> FixUnusedUsings(string sourceCode, string targetFramework)
    {
        var refactoring = new RemoveUnusedUsings();
        return await refactoring.ExecuteAsync(sourceCode, targetFramework);
    }

    /// <summary>
    /// Fixes a field that can be made readonly (IDE0044).
    /// </summary>
    private async Task<RefactoringResult> FixReadonlyField(
        string sourceCode,
        int line,
        int column,
        string targetFramework)
    {
        // Parse the code to extract the field name and class name at the specified location
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = await syntaxTree.GetRootAsync();

        // Convert 1-based line/column to 0-based position
        var linePosition = new Microsoft.CodeAnalysis.Text.LinePosition(line - 1, column - 1);
        var lines = syntaxTree.GetText().Lines;

        // Validate line number is within bounds
        if (linePosition.Line < 0 || linePosition.Line >= lines.Count)
        {
            return RefactoringResult.Failure(
                $"Line {line} is out of range. File has {lines.Count} line(s).");
        }

        // Validate column position is within line length
        var targetLine = lines[linePosition.Line];
        if (linePosition.Character < 0 || linePosition.Character > targetLine.Span.Length)
        {
            return RefactoringResult.Failure(
                $"Column {column} is out of range for line {line} (line length: {targetLine.Span.Length}).");
        }

        var position = targetLine.Start + linePosition.Character;

        // Find the field declaration at this position
        var node = root.FindNode(new Microsoft.CodeAnalysis.Text.TextSpan(position, 1));
        var fieldDeclaration = node.AncestorsAndSelf().OfType<FieldDeclarationSyntax>().FirstOrDefault();

        if (fieldDeclaration == null)
        {
            return RefactoringResult.Failure(
                $"Could not find field declaration at line {line}, column {column}. " +
                "The diagnostic location must point to a field declaration.");
        }

        // Get the field name (handle multiple variables in one declaration)
        var variable = fieldDeclaration.Declaration.Variables.FirstOrDefault();
        if (variable == null)
        {
            return RefactoringResult.Failure(
                "Field declaration has no variables. This should not happen.");
        }

        var fieldName = variable.Identifier.Text;

        // Find the containing class
        var classDeclaration = node.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (classDeclaration == null)
        {
            return RefactoringResult.Failure(
                $"Could not find containing class for field '{fieldName}'. " +
                "The field must be declared inside a class.");
        }

        var className = classDeclaration.Identifier.Text;

        // Apply the refactoring
        var refactoring = new MakeFieldReadonly();
        return await refactoring.ExecuteAsync(sourceCode, className, fieldName, targetFramework);
    }

    /// <summary>
    /// Gets the friendly name of the refactoring applied for a diagnostic ID.
    /// </summary>
    private string GetRefactoringName(string diagnosticId)
    {
        return diagnosticId.ToUpperInvariant() switch
        {
            "IDE0005" or "CS8019" => "remove_unused_usings",
            "IDE0044" => "make_field_readonly",
            _ => "unknown"
        };
    }

    /// <summary>
    /// Validates that the diagnostic ID follows expected patterns.
    /// </summary>
    /// <param name="diagnosticId">The diagnostic ID to validate.</param>
    /// <param name="error">Output parameter containing the error message if validation fails.</param>
    /// <returns>True if the diagnostic ID is valid; otherwise, false.</returns>
    private static bool IsValidDiagnosticIdPattern(string diagnosticId, out string error)
    {
        if (string.IsNullOrWhiteSpace(diagnosticId))
        {
            error = "Diagnostic ID cannot be empty";
            return false;
        }

        // Valid patterns: IDE####, CS####, CA####
        if (!System.Text.RegularExpressions.Regex.IsMatch(
            diagnosticId,
            @"^(IDE|CS|CA)\d{4}$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            error = $"Diagnostic ID '{diagnosticId}' does not match expected pattern (IDE####, CS####, or CA####). " +
                    "Examples: IDE0005, CS8019, CA1031";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
