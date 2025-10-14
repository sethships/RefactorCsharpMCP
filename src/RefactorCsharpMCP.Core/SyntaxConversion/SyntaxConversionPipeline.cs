using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace RefactorCsharpMCP.Core.SyntaxConversion;

/// <summary>
/// Orchestrates multiple syntax converters to transform source code for a target framework.
/// Applies converters in sequence and validates the result compiles.
/// </summary>
public class SyntaxConversionPipeline
{
    private readonly List<ISyntaxConverter> _converters;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyntaxConversionPipeline"/> class
    /// with the specified converters.
    /// </summary>
    /// <param name="converters">The converters to apply in order.</param>
    public SyntaxConversionPipeline(IEnumerable<ISyntaxConverter> converters)
    {
        _converters = converters.ToList();
    }

    /// <summary>
    /// Initializes a new instance with default converters.
    /// </summary>
    public SyntaxConversionPipeline() : this(GetDefaultConverters())
    {
    }

    /// <summary>
    /// Converts the given source code to be compatible with the target framework.
    /// </summary>
    /// <param name="sourceCode">The source code to convert.</param>
    /// <param name="targetFramework">The target framework moniker (e.g., "net48", "net35").</param>
    /// <returns>A result containing the converted code or error information.</returns>
    public ConversionResult Convert(string sourceCode, string targetFramework)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return ConversionResult.Failure("Source code cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(targetFramework))
        {
            return ConversionResult.Failure("Target framework cannot be empty.");
        }

        try
        {
            // Parse the source code
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = syntaxTree.GetRoot();

            // Check for parse errors
            var diagnostics = syntaxTree.GetDiagnostics();
            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            if (errors.Any())
            {
                var errorMessages = string.Join(", ", errors.Select(e => e.GetMessage()).Take(3));
                return ConversionResult.Failure($"Syntax errors in source code: {errorMessages}");
            }

            // Apply converters in sequence
            SyntaxNode convertedRoot = root;
            var appliedConverters = new List<string>();

            foreach (var converter in _converters)
            {
                // Check if this converter applies to the target framework
                if (converter.CanConvert(convertedRoot, targetFramework))
                {
                    convertedRoot = converter.Convert(convertedRoot, targetFramework);
                    appliedConverters.Add(converter.Name);
                }
            }

            // Normalize whitespace for consistent formatting
            convertedRoot = convertedRoot.NormalizeWhitespace();

            var convertedCode = convertedRoot.ToFullString();

            // Build success message
            var message = appliedConverters.Any()
                ? $"Applied {appliedConverters.Count} converter(s): {string.Join(", ", appliedConverters)}"
                : "No conversions needed for target framework.";

            return ConversionResult.Success(convertedCode, message, appliedConverters);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            return ConversionResult.Failure($"Conversion failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds a converter to the pipeline.
    /// </summary>
    /// <param name="converter">The converter to add.</param>
    public void AddConverter(ISyntaxConverter converter)
    {
        _converters.Add(converter);
    }

    /// <summary>
    /// Removes a converter from the pipeline.
    /// </summary>
    /// <param name="converter">The converter to remove.</param>
    /// <returns>True if the converter was removed; otherwise, false.</returns>
    public bool RemoveConverter(ISyntaxConverter converter)
    {
        return _converters.Remove(converter);
    }

    /// <summary>
    /// Gets all registered converters.
    /// </summary>
    public IReadOnlyList<ISyntaxConverter> Converters => _converters.AsReadOnly();

    /// <summary>
    /// Gets the default set of converters.
    /// </summary>
    private static IEnumerable<ISyntaxConverter> GetDefaultConverters()
    {
        // Note: Converters will be added as they are implemented
        // Order matters: apply more complex conversions first
        return new List<ISyntaxConverter>
        {
            // TODO: Add converters as they are implemented:
            // new CollectionExpressionConverter(),
            // new NullableReferenceTypeStripper(),
            // new TupleReturnConverter(),
            // new ReadOnlyAutoPropertyExpander()
        };
    }
}

/// <summary>
/// Represents the result of a syntax conversion operation.
/// </summary>
public class ConversionResult
{
    /// <summary>
    /// Gets a value indicating whether the conversion was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the converted source code if successful; otherwise, null.
    /// </summary>
    public string? ConvertedCode { get; init; }

    /// <summary>
    /// Gets a message describing the conversion result.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the list of converters that were applied.
    /// </summary>
    public IReadOnlyList<string> AppliedConverters { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the error message if the conversion failed; otherwise, null.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful conversion result.
    /// </summary>
    public static ConversionResult Success(string convertedCode, string message, List<string> appliedConverters)
    {
        return new ConversionResult
        {
            IsSuccess = true,
            ConvertedCode = convertedCode,
            Message = message,
            AppliedConverters = appliedConverters
        };
    }

    /// <summary>
    /// Creates a failed conversion result.
    /// </summary>
    public static ConversionResult Failure(string errorMessage)
    {
        return new ConversionResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            Message = $"Conversion failed: {errorMessage}"
        };
    }
}
