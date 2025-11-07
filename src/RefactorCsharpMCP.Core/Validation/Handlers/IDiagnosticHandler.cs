using Microsoft.CodeAnalysis;

namespace RefactorCsharpMCP.Core.Validation.Handlers;

/// <summary>
/// Base interface for diagnostic handling using Strategy Pattern.
/// Implementations process Roslyn diagnostics and classify them for framework validation.
/// </summary>
public interface IDiagnosticHandler
{
    /// <summary>
    /// Handles diagnostics asynchronously and returns a validation result.
    /// </summary>
    /// <param name="diagnostics">Collection of Roslyn diagnostics to process.</param>
    /// <param name="targetFramework">Target framework moniker (e.g., "net8.0", "net48").</param>
    /// <param name="syntaxTree">Syntax tree for identifier extraction and error classification.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>ValidationResult indicating success or failure with details.</returns>
    Task<ValidationResult> HandleAsync(
        IEnumerable<Diagnostic> diagnostics,
        string targetFramework,
        SyntaxTree syntaxTree,
        CancellationToken cancellationToken = default);
}
