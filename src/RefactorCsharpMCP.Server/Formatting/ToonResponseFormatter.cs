using RefactorCsharpMCP.Toon;

namespace RefactorCsharpMCP.Server.Formatting;

/// <summary>
/// Response formatter that encodes responses in TOON format.
/// Returns a wrapper object containing the format identifier and TOON-encoded content.
/// </summary>
public class ToonResponseFormatter : IResponseFormatter
{
    private readonly IToonEncoder _encoder;

    /// <summary>
    /// Creates a new TOON response formatter.
    /// </summary>
    /// <param name="encoder">The TOON encoder to use.</param>
    public ToonResponseFormatter(IToonEncoder encoder)
    {
        _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
    }

    /// <inheritdoc />
    public object Format(object response)
    {
        var toonContent = _encoder.Encode(response);

        // Return a wrapper that identifies this as TOON format
        // The MCP SDK will serialize this wrapper as JSON, but the content is TOON
        return new
        {
            format = "toon",
            content = toonContent
        };
    }
}
