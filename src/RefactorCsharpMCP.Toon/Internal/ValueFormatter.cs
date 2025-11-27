using System.Text;

namespace RefactorCsharpMCP.Toon.Internal;

/// <summary>
/// Handles formatting and escaping of individual values for TOON encoding.
/// </summary>
internal static class ValueFormatter
{
    /// <summary>
    /// Formats a primitive value for TOON output.
    /// </summary>
    public static string FormatPrimitive(object? value, ToonEncoderOptions options)
    {
        return value switch
        {
            null => "null",
            bool b => b ? "true" : "false",
            string s => FormatString(s, options),
            char c => c.ToString(),
            // Numeric types - use invariant culture
            sbyte or byte or short or ushort or int or uint or long or ulong
                or float or double or decimal => FormatNumber(value),
            DateTime dt => dt.ToString("O"), // ISO 8601
            DateTimeOffset dto => dto.ToString("O"),
            Guid g => g.ToString(),
            Enum e => e.ToString(),
            _ => value.ToString() ?? "null"
        };
    }

    /// <summary>
    /// Formats a string value, applying Base64 encoding for multi-line strings if enabled.
    /// </summary>
    public static string FormatString(string value, ToonEncoderOptions options)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // Check if multi-line and Base64 encoding is enabled
        if (options.Base64EncodeMultilineStrings && ContainsNewline(value))
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            var base64 = Convert.ToBase64String(bytes);
            return $"{options.Base64Prefix}{base64}";
        }

        // Escape special characters that could break TOON parsing
        return EscapeString(value);
    }

    /// <summary>
    /// Checks if a string contains newline characters.
    /// </summary>
    public static bool ContainsNewline(string value)
    {
        return value.Contains('\n') || value.Contains('\r');
    }

    /// <summary>
    /// Escapes special characters in a string value.
    /// </summary>
    private static string EscapeString(string value)
    {
        // TOON strings don't need quotes, but we need to escape:
        // - Commas (used in tabular data)
        // - Colons at the start (could be confused with key-value)
        // - Leading/trailing whitespace preservation
        var sb = new StringBuilder(value.Length);

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            switch (c)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case ',':
                    // Only escape commas - they're significant in tabular rows
                    sb.Append("\\,");
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats a numeric value using invariant culture.
    /// </summary>
    private static string FormatNumber(object value)
    {
        return value switch
        {
            float f => f.ToString(System.Globalization.CultureInfo.InvariantCulture),
            double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
            decimal m => m.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "0"
        };
    }

    /// <summary>
    /// Converts a property name to camelCase if enabled.
    /// </summary>
    public static string FormatPropertyName(string name, ToonEncoderOptions options)
    {
        if (!options.UseCamelCase || string.IsNullOrEmpty(name))
            return name;

        // Already lowercase first char
        if (char.IsLower(name[0]))
            return name;

        // Convert first char to lowercase
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }
}
