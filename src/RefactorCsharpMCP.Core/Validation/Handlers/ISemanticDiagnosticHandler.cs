namespace RefactorCsharpMCP.Core.Validation.Handlers;

/// <summary>
/// Specific interface for semantic-time diagnostic handling (Interface Segregation Principle).
/// Handles errors detected during semantic analysis (compilation) after parsing succeeds.
/// </summary>
/// <remarks>
/// This marker interface extends IDiagnosticHandler to enable:
/// - Type-safe dependency injection registration
/// - Explicit separation between parse-time and semantic-time error handling
/// - Clear intent in code that depends on semantic diagnostic processing
/// - Supports API availability checking and typo detection heuristics
/// </remarks>
public interface ISemanticDiagnosticHandler : IDiagnosticHandler
{
    // Marker interface for type safety - no additional members needed
    // All functionality inherited from IDiagnosticHandler
}
