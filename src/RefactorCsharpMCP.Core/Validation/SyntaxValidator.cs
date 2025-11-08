using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RefactorCsharpMCP.Core.Infrastructure.FrameworkSupport;
using RefactorCsharpMCP.Core.Validation.Handlers;

namespace RefactorCsharpMCP.Core.Validation;

/// <summary>
/// Validates C# source code syntax compatibility with target .NET frameworks.
/// Performs both pre-refactoring (input) and post-refactoring (output) validation.
/// Implements Facade Pattern - delegates diagnostic handling to specialized handlers.
/// Implements IDisposable to properly clean up reference assembly resolver resources.
/// </summary>
public class SyntaxValidator : IDisposable
{
    private readonly ReferenceAssemblyResolver _referenceResolver;
    private readonly IParseDiagnosticHandler _parseHandler;
    private readonly ISemanticDiagnosticHandler _semanticHandler;
    private readonly bool _ownsResolver;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyntaxValidator"/> class.
    /// </summary>
    /// <param name="referenceResolver">Optional reference assembly resolver for testing.</param>
    /// <param name="parseHandler">Optional parse diagnostic handler for testing.</param>
    /// <param name="semanticHandler">Optional semantic diagnostic handler for testing.</param>
    public SyntaxValidator(
        ReferenceAssemblyResolver? referenceResolver = null,
        IParseDiagnosticHandler? parseHandler = null,
        ISemanticDiagnosticHandler? semanticHandler = null)
    {
        _referenceResolver = referenceResolver ?? new ReferenceAssemblyResolver();
        _parseHandler = parseHandler ?? new ParseDiagnosticHandler();
        _semanticHandler = semanticHandler ?? new SemanticDiagnosticHandler();
        _ownsResolver = referenceResolver == null;
    }

    /// <summary>
    /// Validates that input source code is compatible with the target framework.
    /// Checks if the code uses C# features supported by the framework's language version.
    /// </summary>
    /// <param name="sourceCode">The C# source code to validate.</param>
    /// <param name="targetFramework">The target framework moniker (e.g., "net8.0", "net48").</param>
    /// <returns>A ValidationResult indicating success or describing the incompatibility.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the validator has been disposed.</exception>
    public async Task<ValidationResult> ValidateInputAsync(string sourceCode, string targetFramework)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return await ValidateCompilationAsync(
            sourceCode,
            targetFramework,
            ErrorCode.INPUT_SYNTAX_MISMATCH,
            "Input code");
    }

    /// <summary>
    /// Validates that refactored output code is compatible with the target framework.
    /// Checks if the generated code uses C# features supported by the framework's language version.
    /// </summary>
    /// <param name="sourceCode">The refactored C# source code to validate.</param>
    /// <param name="targetFramework">The target framework moniker (e.g., "net8.0", "net48").</param>
    /// <returns>A ValidationResult indicating success or describing the incompatibility.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the validator has been disposed.</exception>
    public async Task<ValidationResult> ValidateOutputAsync(string sourceCode, string targetFramework)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return await ValidateCompilationAsync(
            sourceCode,
            targetFramework,
            ErrorCode.FRAMEWORK_SYNTAX_MISMATCH,
            "Refactored output");
    }

    /// <summary>
    /// Core validation logic: attempts to compile source code with target framework's language version.
    /// </summary>
    private async Task<ValidationResult> ValidateCompilationAsync(
        string sourceCode,
        string targetFramework,
        ErrorCode mismatchErrorCode,
        string codeDescription)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return ValidationResult.Failure(
                ErrorCode.SYNTAX_ERROR,
                "Source code cannot be empty.",
                "Provide valid C# source code.");
        }

        try
        {
            // Normalize framework moniker
            targetFramework = FrameworkMoniker.Normalize(targetFramework);

            // Validate framework is supported
            if (!FrameworkMoniker.IsSupported(targetFramework))
            {
                return ValidationResult.Failure(
                    ErrorCode.UNKNOWN_FRAMEWORK,
                    $"Unsupported framework: {targetFramework}",
                    "Use a Microsoft-supported framework version.");
            }

            // Get language version for target framework
            var languageVersion = FrameworkMoniker.GetLanguageVersion(targetFramework);
            var preprocessorSymbols = FrameworkMoniker.GetPreprocessorSymbols(targetFramework);

            // Create parse options with framework-specific language version
            var parseOptions = new CSharpParseOptions(
                languageVersion: languageVersion,
                kind: SourceCodeKind.Regular,
                documentationMode: DocumentationMode.None,
                preprocessorSymbols: preprocessorSymbols);

            // Parse source code
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, parseOptions);

            // Delegate parse diagnostic handling to specialized handler (Strategy Pattern)
            var parseDiagnostics = syntaxTree.GetDiagnostics();
            var parseResult = _parseHandler.Handle(parseDiagnostics, targetFramework, syntaxTree);

            if (!parseResult.IsValid)
            {
                // Convert INPUT_SYNTAX_MISMATCH to FRAMEWORK_SYNTAX_MISMATCH for refactored output validation
                if (parseResult.ErrorCode == ErrorCode.INPUT_SYNTAX_MISMATCH &&
                    mismatchErrorCode == ErrorCode.FRAMEWORK_SYNTAX_MISMATCH)
                {
                    return ValidationResult.Failure(ErrorCode.FRAMEWORK_SYNTAX_MISMATCH,
                        parseResult.ErrorMessage ?? "Syntax validation failed", parseResult.SuggestedAction);
                }
                return parseResult;
            }

            // Get framework-specific metadata references for semantic validation
            var references = await _referenceResolver.GetReferenceAssembliesAsync(targetFramework);

            // Create compilation options
            var compilationOptions = new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: FrameworkMoniker.GetNullableContextOptions(targetFramework),
                allowUnsafe: false,
                optimizationLevel: OptimizationLevel.Debug);

            // Create compilation for semantic analysis
            var compilation = CSharpCompilation.Create(
                "ValidationAssembly",
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: compilationOptions);

            // Delegate semantic diagnostic handling to specialized handler (Strategy Pattern)
            var semanticDiagnostics = compilation.GetDiagnostics();
            return _semanticHandler.Handle(semanticDiagnostics, targetFramework, syntaxTree);
        }
        catch (ArgumentException ex)
        {
            return ValidationResult.Failure(
                ErrorCode.MISSING_PARAMETER,
                "Invalid parameter provided",
                ex.ParamName != null ? $"Check parameter: {ex.ParamName}" : "Check input parameters are valid.");
        }
        catch (NotSupportedException ex)
        {
            return ValidationResult.Failure(
                ErrorCode.UNKNOWN_FRAMEWORK,
                ex.Message,
                "Use a supported .NET framework version.");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("reference assembl", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Failure(
                ErrorCode.REFACTORING_FAILED,
                "Unable to resolve reference assemblies for target framework",
                "Ensure the target framework is properly installed or try clearing the assembly cache.");
        }
        catch (InvalidOperationException)
        {
            return ValidationResult.Failure(
                ErrorCode.REFACTORING_FAILED,
                "Validation encountered an unexpected state",
                "Check source code syntax and ensure target framework is valid.");
        }
    }

    /// <summary>
    /// Releases resources used by the SyntaxValidator.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and optionally managed resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing && _ownsResolver)
        {
            _referenceResolver?.Dispose();
        }

        _disposed = true;
    }
}
