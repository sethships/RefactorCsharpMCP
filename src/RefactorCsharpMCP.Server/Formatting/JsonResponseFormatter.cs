namespace RefactorCsharpMCP.Server.Formatting;

/// <summary>
/// Default response formatter that passes objects through for JSON serialization by MCP SDK.
/// </summary>
public class JsonResponseFormatter : IResponseFormatter
{
    /// <inheritdoc />
    public object Format(object response)
    {
        // MCP SDK handles JSON serialization automatically
        return response;
    }
}
