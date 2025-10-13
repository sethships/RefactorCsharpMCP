using Microsoft.CodeAnalysis;

namespace RefactorCsharpMCP.Tests.Infrastructure;

/// <summary>
/// Utilities for validating Roslyn compilations in tests.
/// Provides helpers for checking compilation errors and formatting diagnostic messages.
/// </summary>
public static class CompilationValidator
{
    /// <summary>
    /// Validates that a compilation has no errors.
    /// </summary>
    public static bool HasNoErrors(Microsoft.CodeAnalysis.CSharp.CSharpCompilation compilation)
    {
        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        return !errors.Any();
    }

    /// <summary>
    /// Gets all compilation errors from a compilation.
    /// </summary>
    public static IEnumerable<Diagnostic> GetErrors(Microsoft.CodeAnalysis.CSharp.CSharpCompilation compilation)
    {
        return compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// Formats diagnostic messages for display in test failures.
    /// </summary>
    public static string FormatDiagnostics(IEnumerable<Diagnostic> diagnostics)
    {
        return string.Join(Environment.NewLine,
            diagnostics.Select(d => $"  {d.Id} at {d.Location.GetLineSpan().StartLinePosition}: {d.GetMessage()}"));
    }

    /// <summary>
    /// Asserts that a compilation has no errors.
    /// Throws an exception with formatted error messages if errors are found.
    /// </summary>
    public static void AssertNoErrors(Microsoft.CodeAnalysis.CSharp.CSharpCompilation compilation, string context = "")
    {
        var errors = GetErrors(compilation).ToList();

        if (errors.Any())
        {
            var message = string.IsNullOrEmpty(context)
                ? "Compilation has errors:"
                : $"Compilation has errors ({context}):";

            message += Environment.NewLine + FormatDiagnostics(errors);

            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// Asserts that source code compiles successfully for a given framework.
    /// </summary>
    public static async Task AssertCompilesAsync(
        string targetFramework,
        string sourceCode,
        CompilationFactory? factory = null)
    {
        factory ??= new CompilationFactory();

        try
        {
            var compilation = await factory.CreateCompilationAsync(targetFramework, sourceCode);
            AssertNoErrors(compilation, $"framework {targetFramework}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to compile source code for framework {targetFramework}: {ex.Message}",
                ex);
        }
    }
}
