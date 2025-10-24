namespace RefactorCsharpMCP.Core.Diagnostics;

/// <summary>
/// Represents the result of a diagnostic analysis operation.
/// </summary>
public class DiagnosticResult
{
    /// <summary>
    /// Gets a value indicating whether the diagnostic analysis was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the list of diagnostics found in the source code.
    /// </summary>
    public List<DiagnosticInfo> Diagnostics { get; init; } = new();

    /// <summary>
    /// Gets a summary of the diagnostic analysis.
    /// </summary>
    public DiagnosticSummary Summary { get; init; } = null!;

    /// <summary>
    /// Gets the error message if the analysis failed; otherwise, null.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful diagnostic analysis result.
    /// </summary>
    /// <param name="diagnostics">The list of diagnostics found.</param>
    /// <returns>A successful <see cref="DiagnosticResult"/>.</returns>
    public static DiagnosticResult CreateSuccess(List<DiagnosticInfo> diagnostics)
    {
        var summary = new DiagnosticSummary
        {
            TotalDiagnostics = diagnostics.Count,
            ErrorCount = diagnostics.Count(d => d.Severity == "Error"),
            WarningCount = diagnostics.Count(d => d.Severity == "Warning"),
            InfoCount = diagnostics.Count(d => d.Severity == "Info" || d.Severity == "Hidden")
        };

        return new DiagnosticResult
        {
            Success = true,
            Diagnostics = diagnostics,
            Summary = summary
        };
    }

    /// <summary>
    /// Creates a failed diagnostic analysis result.
    /// </summary>
    /// <param name="errorMessage">The error message describing why the analysis failed.</param>
    /// <returns>A failed <see cref="DiagnosticResult"/>.</returns>
    public static DiagnosticResult CreateFailure(string errorMessage)
    {
        return new DiagnosticResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            Diagnostics = new List<DiagnosticInfo>(),
            Summary = new DiagnosticSummary()
        };
    }
}

/// <summary>
/// Represents a summary of diagnostic analysis results.
/// </summary>
public class DiagnosticSummary
{
    /// <summary>
    /// Gets the total number of diagnostics found.
    /// </summary>
    public int TotalDiagnostics { get; init; }

    /// <summary>
    /// Gets the number of error-level diagnostics.
    /// </summary>
    public int ErrorCount { get; init; }

    /// <summary>
    /// Gets the number of warning-level diagnostics.
    /// </summary>
    public int WarningCount { get; init; }

    /// <summary>
    /// Gets the number of info-level diagnostics.
    /// </summary>
    public int InfoCount { get; init; }
}
