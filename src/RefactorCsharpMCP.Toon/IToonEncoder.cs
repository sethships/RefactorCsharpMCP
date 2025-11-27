namespace RefactorCsharpMCP.Toon;

/// <summary>
/// Provides TOON (Token-Oriented Object Notation) encoding for objects.
/// TOON is a compact, human-readable format optimized for LLM interactions,
/// achieving 30-60% token reduction compared to JSON.
/// </summary>
public interface IToonEncoder
{
    /// <summary>
    /// Encodes an object to TOON format using default options.
    /// </summary>
    /// <param name="value">The object to encode.</param>
    /// <returns>The TOON-encoded string representation.</returns>
    string Encode(object? value);

    /// <summary>
    /// Encodes an object to TOON format with custom options.
    /// </summary>
    /// <param name="value">The object to encode.</param>
    /// <param name="options">Encoding options.</param>
    /// <returns>The TOON-encoded string representation.</returns>
    string Encode(object? value, ToonEncoderOptions options);
}
