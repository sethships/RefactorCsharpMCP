namespace RefactorCsharpMCP.Server.Configuration;

/// <summary>
/// Loads output format configuration from environment variables and CLI arguments.
/// </summary>
public static class OutputFormatConfiguration
{
    /// <summary>
    /// Environment variable name for output format setting.
    /// </summary>
    public const string EnvVarName = "REFACTOR_CSHARP_OUTPUT_FORMAT";

    /// <summary>
    /// CLI argument prefix for output format setting.
    /// </summary>
    public const string CliArgPrefix = "--output-format";

    /// <summary>
    /// JSON output format identifier (default).
    /// </summary>
    public const string FormatJson = "json";

    /// <summary>
    /// TOON output format identifier.
    /// </summary>
    public const string FormatToon = "toon";

    /// <summary>
    /// Set of valid output format values (case-insensitive comparison).
    /// </summary>
    private static readonly HashSet<string> ValidFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        FormatJson,
        FormatToon
    };

    /// <summary>
    /// Loads output format options from environment and CLI arguments.
    /// Precedence: CLI > Environment > Default (json)
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Configured output format options.</returns>
    public static OutputFormatOptions Load(string[] args)
    {
        var options = new OutputFormatOptions();

        // 1. Check environment variable
        var envFormat = Environment.GetEnvironmentVariable(EnvVarName);
        if (!string.IsNullOrWhiteSpace(envFormat))
        {
            options.Format = envFormat.Trim();
        }

        // 2. Check CLI args (takes precedence over env var)
        var cliFormat = ParseCliArg(args, CliArgPrefix);
        if (!string.IsNullOrWhiteSpace(cliFormat))
        {
            options.Format = cliFormat;
        }

        // 3. Validate format and default to JSON if invalid
        if (!ValidFormats.Contains(options.Format))
        {
            Console.Error.WriteLine(
                $"Warning: Invalid output format '{options.Format}' specified. " +
                $"Valid formats: {string.Join(", ", ValidFormats)}. Defaulting to '{FormatJson}'.");
            options.Format = FormatJson;
        }

        return options;
    }

    /// <summary>
    /// Parses a CLI argument value.
    /// Supports: "--output-format toon" and "--output-format=toon"
    /// </summary>
    private static string? ParseCliArg(string[] args, string prefix)
    {
        for (int i = 0; i < args.Length; i++)
        {
            // Check for "--output-format value" pattern
            if (args[i].Equals(prefix, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                return args[i + 1].Trim();
            }

            // Check for "--output-format=value" pattern
            if (args[i].StartsWith($"{prefix}=", StringComparison.OrdinalIgnoreCase))
            {
                return args[i].Substring(prefix.Length + 1).Trim();
            }
        }

        return null;
    }
}
