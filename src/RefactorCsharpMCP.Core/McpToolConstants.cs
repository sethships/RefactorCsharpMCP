using System.Text.RegularExpressions;

namespace RefactorCsharpMCP.Core;

/// <summary>
/// Shared constants for MCP tool implementations.
/// </summary>
public static class McpToolConstants
{
    /// <summary>
    /// Maximum allowed source code size in bytes (1MB).
    /// </summary>
    public const int MAX_SOURCE_CODE_SIZE = 1_000_000;

    /// <summary>
    /// Regular expression pattern for validating C# identifiers.
    /// </summary>
    public const string CSHARP_IDENTIFIER_PATTERN = @"^[a-zA-Z_][a-zA-Z0-9_]*$";

    /// <summary>
    /// Compiled regex for C# identifier validation (performance optimized).
    /// </summary>
    public static readonly Regex CSharpIdentifierRegex = new(
        CSHARP_IDENTIFIER_PATTERN,
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100)); // Timeout to prevent ReDoS
}
