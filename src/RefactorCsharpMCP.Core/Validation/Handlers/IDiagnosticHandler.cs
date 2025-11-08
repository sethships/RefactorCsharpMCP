using Microsoft.CodeAnalysis;

namespace RefactorCsharpMCP.Core.Validation.Handlers;

/// <summary>
/// Base interface for diagnostic handling using Strategy Pattern.
/// Implementations process Roslyn diagnostics and classify them for framework validation.
/// </summary>
/// <remarks>
/// Thread-safe: Implementations must be stateless and safe for concurrent calls.
/// Registered as singletons in DI container.
/// </remarks>
public interface IDiagnosticHandler
{
    /// <summary>
    /// Handles diagnostics and returns a validation result.
    /// </summary>
    /// <param name="diagnostics">Collection of Roslyn diagnostics to process.</param>
    /// <param name="targetFramework">Target framework moniker (e.g., "net8.0", "net48").</param>
    /// <param name="syntaxTree">Syntax tree for identifier extraction and error classification.</param>
    /// <returns>ValidationResult indicating success or failure with details.</returns>
    ValidationResult Handle(
        IEnumerable<Diagnostic> diagnostics,
        string targetFramework,
        SyntaxTree syntaxTree);
}
