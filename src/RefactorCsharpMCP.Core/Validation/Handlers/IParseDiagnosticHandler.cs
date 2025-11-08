namespace RefactorCsharpMCP.Core.Validation.Handlers;

/// <summary>
/// Specific interface for parse-time diagnostic handling (Interface Segregation Principle).
/// Handles syntax errors detected during parsing before semantic analysis.
/// </summary>
/// <remarks>
/// This marker interface extends IDiagnosticHandler to enable:
/// - Type-safe dependency injection registration
/// - Explicit separation between parse-time and semantic-time error handling
/// - Clear intent in code that depends on parse diagnostic processing
/// </remarks>
public interface IParseDiagnosticHandler : IDiagnosticHandler
{
    // Marker interface for type safety - no additional members needed
    // All functionality inherited from IDiagnosticHandler
}
