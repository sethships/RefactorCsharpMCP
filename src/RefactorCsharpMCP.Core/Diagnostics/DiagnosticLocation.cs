namespace RefactorCsharpMCP.Core.Diagnostics;

/// <summary>
/// Represents the location of a diagnostic in source code.
/// </summary>
public class DiagnosticLocation
{
    /// <summary>
    /// Gets the line number where the diagnostic occurs (1-based).
    /// </summary>
    public int Line { get; init; }

    /// <summary>
    /// Gets the column number where the diagnostic occurs (1-based).
    /// </summary>
    public int Column { get; init; }

    /// <summary>
    /// Gets the start position of the diagnostic span in the source text.
    /// </summary>
    public int SpanStart { get; init; }

    /// <summary>
    /// Gets the length of the diagnostic span in the source text.
    /// </summary>
    public int SpanLength { get; init; }

    /// <summary>
    /// Creates a new DiagnosticLocation.
    /// </summary>
    /// <param name="line">The line number (1-based).</param>
    /// <param name="column">The column number (1-based).</param>
    /// <param name="spanStart">The start position of the span.</param>
    /// <param name="spanLength">The length of the span.</param>
    public DiagnosticLocation(int line, int column, int spanStart, int spanLength)
    {
        Line = line;
        Column = column;
        SpanStart = spanStart;
        SpanLength = spanLength;
    }
}
