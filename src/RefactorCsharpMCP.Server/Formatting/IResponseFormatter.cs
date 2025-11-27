namespace RefactorCsharpMCP.Server.Formatting;

/// <summary>
/// Formats MCP tool responses for output.
/// </summary>
public interface IResponseFormatter
{
    /// <summary>
    /// Formats a response object for MCP output.
    /// </summary>
    /// <param name="response">The response object to format.</param>
    /// <returns>The formatted response for MCP serialization.</returns>
    object Format(object response);
}
