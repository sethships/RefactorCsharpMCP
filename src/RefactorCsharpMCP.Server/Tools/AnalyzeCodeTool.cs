using RefactorCsharpMCP.Core;
using System.ComponentModel;
using ModelContextProtocol.Server;
using RefactorCsharpMCP.Core.Diagnostics;
using Microsoft.CodeAnalysis;
using RefactorCsharpMCP.Core.Validation;
using RefactorCsharpMCP.Server.Formatting;

namespace RefactorCsharpMCP.Server.Tools;

/// <summary>
/// MCP tool for analyzing C# source code and detecting diagnostics using Roslyn.
/// </summary>
[McpServerToolType]
public class AnalyzeCodeTool
{
    private readonly DiagnosticAnalyzer _analyzer;
    private readonly IResponseFormatter _formatter;

    /// <summary>
    /// Creates a new AnalyzeCodeTool with the specified response formatter.
    /// </summary>
    public AnalyzeCodeTool(IResponseFormatter formatter)
    {
        _analyzer = new DiagnosticAnalyzer();
        _formatter = formatter;
    }

    /// <summary>
    /// Analyzes C# source code for compiler warnings, style violations, and code quality issues.
    /// Uses full IDE analyzer support (IDE0001-IDE9999) including IDE0005 (unused usings), IDE0044 (readonly fields), and more.
    /// </summary>
    /// <param name="sourceCode">The complete C# source code to analyze.</param>
    /// <param name="targetFramework">The target .NET framework (e.g., "net8.0", "net48", "netstandard2.0").</param>
    /// <param name="minSeverity">Minimum severity level to report: "Error", "Warning", "Info", "Hidden" (default: "Warning").</param>
    /// <returns>A JSON object containing the list of diagnostics found or error information.</returns>
    [McpServerTool]
    [Description("Analyzes C# code for compiler warnings, style violations, and code quality issues using Roslyn diagnostics with full IDE analyzer support (IDE0001-IDE9999). Detects unused usings (IDE0005), readonly fields (IDE0044), code style issues, and more. Returns a list of issues with their locations, severity levels, and applicable refactorings. Framework-aware: analyzes code according to target framework capabilities (e.g., C# language version, available APIs).")]
    public async Task<object> AnalyzeCode(
        [Description("The complete C# source code to analyze")] string sourceCode,
        [Description("The target .NET framework (e.g., 'net8.0', 'net48', 'netstandard2.0')")] string targetFramework,
        [Description("Minimum severity level to report: 'Error', 'Warning', 'Info', 'Hidden' (default: 'Warning')")] string? minSeverity = null)
    {
        // Input validation using shared validator
        var validation = ToolInputValidator.ValidateSourceCode(sourceCode, "Analysis")
                         ?? ToolInputValidator.ValidateSourceCodeSize(sourceCode, "Analysis")
                         ?? ToolInputValidator.ValidateTargetFramework(targetFramework, "Analysis");

        if (validation != null)
        {
            return _formatter.Format(validation);
        }

        // Parse severity level
        var severity = ParseSeverity(minSeverity);

        // Execute the analysis
        var result = await _analyzer.AnalyzeCodeAsync(sourceCode, targetFramework, severity);

        // Return result as an object that MCP can serialize
        if (result.Success)
        {
            return _formatter.Format(new
            {
                success = true,
                diagnostics = result.Diagnostics.Select(d => new
                {
                    id = d.Id,
                    severity = d.Severity,
                    message = d.Message,
                    location = new
                    {
                        line = d.Location.Line,
                        column = d.Location.Column,
                        spanStart = d.Location.SpanStart,
                        spanLength = d.Location.SpanLength
                    },
                    category = d.Category,
                    applicableRefactorings = d.ApplicableRefactorings
                }).ToList(),
                summary = new
                {
                    totalDiagnostics = result.Summary.TotalDiagnostics,
                    errorCount = result.Summary.ErrorCount,
                    warningCount = result.Summary.WarningCount,
                    infoCount = result.Summary.InfoCount
                },
                message = $"Found {result.Summary.TotalDiagnostics} diagnostic(s): " +
                         $"{result.Summary.ErrorCount} error(s), " +
                         $"{result.Summary.WarningCount} warning(s), " +
                         $"{result.Summary.InfoCount} info(s)"
            });
        }
        else
        {
            return _formatter.Format(new
            {
                success = false,
                message = result.ErrorMessage,
                error = result.ErrorMessage
            });
        }
    }

    /// <summary>
    /// Parses the severity string into a DiagnosticSeverity enum value.
    /// </summary>
    private DiagnosticSeverity ParseSeverity(string? severity)
    {
        if (string.IsNullOrWhiteSpace(severity))
        {
            return DiagnosticSeverity.Warning; // Default
        }

        var result = severity.ToLowerInvariant() switch
        {
            "error" => DiagnosticSeverity.Error,
            "warning" => DiagnosticSeverity.Warning,
            "info" => DiagnosticSeverity.Info,
            "hidden" => DiagnosticSeverity.Hidden,
            _ => DiagnosticSeverity.Warning // Default for invalid values
        };

        // Log warning if invalid severity was provided
        if (result == DiagnosticSeverity.Warning && severity.ToLowerInvariant() != "warning")
        {
            System.Diagnostics.Debug.WriteLine(
                $"Warning: Invalid severity '{severity}' provided, defaulting to 'Warning'. " +
                "Valid values: Error, Warning, Info, Hidden");
        }

        return result;
    }
}
