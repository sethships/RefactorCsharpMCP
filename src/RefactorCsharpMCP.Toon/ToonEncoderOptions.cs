namespace RefactorCsharpMCP.Toon;

/// <summary>
/// Configuration options for TOON encoding.
/// </summary>
public class ToonEncoderOptions
{
    /// <summary>
    /// Number of spaces for each indentation level. Default: 2.
    /// </summary>
    public int IndentSize { get; set; } = 2;

    /// <summary>
    /// Whether to Base64-encode string values containing newlines. Default: true.
    /// When true, multi-line strings are encoded as "base64:..." to preserve exact content.
    /// </summary>
    public bool Base64EncodeMultilineStrings { get; set; } = true;

    /// <summary>
    /// Prefix for Base64-encoded values. Default: "base64:".
    /// </summary>
    public string Base64Prefix { get; set; } = "base64:";

    /// <summary>
    /// Whether to use camelCase for property names. Default: true.
    /// When false, original property names are preserved.
    /// </summary>
    public bool UseCamelCase { get; set; } = true;

    /// <summary>
    /// Maximum nesting depth for objects. Default: 10.
    /// Prevents stack overflow on deeply nested or circular structures.
    /// </summary>
    public int MaxDepth { get; set; } = 10;

    /// <summary>
    /// Gets the default options instance.
    /// </summary>
    public static ToonEncoderOptions Default => new();
}
