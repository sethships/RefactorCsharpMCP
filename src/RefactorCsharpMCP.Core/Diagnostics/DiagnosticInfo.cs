namespace RefactorCsharpMCP.Core.Diagnostics;

/// <summary>
/// Represents information about a single diagnostic issue found in source code.
/// </summary>
public class DiagnosticInfo
{
    /// <summary>
    /// Gets the diagnostic identifier (e.g., "IDE0005", "CS8019").
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets the severity level of the diagnostic (e.g., "Error", "Warning", "Info", "Hidden").
    /// </summary>
    public string Severity { get; init; } = string.Empty;

    /// <summary>
    /// Gets the diagnostic message describing the issue.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the location of the diagnostic in the source code.
    /// </summary>
    public DiagnosticLocation Location { get; init; } = null!;

    /// <summary>
    /// Gets the category of the diagnostic (e.g., "Style", "Quality", "Performance", "Security", "Design").
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Gets the list of refactoring tool names that can fix this diagnostic.
    /// </summary>
    public List<string> ApplicableRefactorings { get; init; } = new();

    /// <summary>
    /// Creates a new DiagnosticInfo.
    /// </summary>
    public DiagnosticInfo()
    {
    }

    /// <summary>
    /// Creates a new DiagnosticInfo with specified values.
    /// </summary>
    /// <param name="id">The diagnostic ID.</param>
    /// <param name="severity">The severity level.</param>
    /// <param name="message">The diagnostic message.</param>
    /// <param name="location">The location in source code.</param>
    /// <param name="category">The diagnostic category.</param>
    /// <param name="applicableRefactorings">List of refactorings that can fix this diagnostic.</param>
    public DiagnosticInfo(
        string id,
        string severity,
        string message,
        DiagnosticLocation location,
        string category,
        List<string> applicableRefactorings)
    {
        Id = id;
        Severity = severity;
        Message = message;
        Location = location;
        Category = category;
        ApplicableRefactorings = applicableRefactorings;
    }
}
