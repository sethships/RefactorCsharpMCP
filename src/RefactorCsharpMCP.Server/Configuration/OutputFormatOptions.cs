namespace RefactorCsharpMCP.Server.Configuration;

/// <summary>
/// Configuration options for MCP tool response output format.
/// </summary>
public class OutputFormatOptions
{
    /// <summary>
    /// The output format to use. Valid values: "json" (default), "toon".
    /// </summary>
    public string Format { get; set; } = "json";

    /// <summary>
    /// Returns true if TOON format is enabled.
    /// </summary>
    public bool IsToonEnabled => Format.Equals("toon", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns true if JSON format is enabled (default).
    /// </summary>
    public bool IsJsonEnabled => !IsToonEnabled;
}
