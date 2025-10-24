using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using RefactorCsharpMCP.Core.Validation;

namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Base class for all refactoring operations, providing common infrastructure and utilities.
/// Reduces boilerplate by centralizing validation, error handling, and Roslyn operations.
/// Includes compilation caching to improve performance when processing the same source code multiple times.
/// Provides structured error logging with context for debugging while returning sanitized user messages.
/// </summary>
public abstract class RefactoringBase
{
    /// <summary>
    /// Cache for compilations using ConditionalWeakTable for object identity caching.
    /// Automatically manages lifetime and prevents hash collision issues.
    /// Thread-safe and allows garbage collection when SyntaxTree is no longer referenced.
    /// </summary>
    private static readonly ConditionalWeakTable<SyntaxTree, CSharpCompilation> _compilationCache = new();

    /// <summary>
    /// Number of successful cache hits (thread-safe counter).
    /// </summary>
    private static int _cacheHits = 0;

    /// <summary>
    /// Number of cache misses requiring new compilation creation (thread-safe counter).
    /// </summary>
    private static int _cacheMisses = 0;

    /// <summary>
    /// Optional logger for structured error logging and telemetry.
    /// </summary>
    protected ILogger? Logger { get; set; }

    /// <summary>
    /// Tracks the current phase of the refactoring operation for error context.
    /// </summary>
    protected string CurrentPhase { get; set; } = "Initialization";

    /// <summary>
    /// Optional metrics tracker for performance monitoring.
    /// When set, refactoring operations will automatically record timing and complexity metrics.
    /// </summary>
    protected RefactoringMetricsTracker? MetricsTracker { get; set; }
    /// <summary>
    /// Validates that a string parameter is not null or whitespace.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="parameterName">The parameter name for error messages.</param>
    /// <returns>A RefactoringResult indicating success or failure.</returns>
    protected RefactoringResult ValidateNonEmpty(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return RefactoringResult.Failure($"{parameterName} cannot be empty.");
        }
        // Return validation success without refactored code (validation helper, not actual refactoring)
        return new RefactoringResult
        {
            IsSuccess = true,
            RefactoredCode = null,
            Message = "Validation passed"
        };
    }

    /// <summary>
    /// Parses source code into a syntax tree and checks for syntax errors.
    /// </summary>
    /// <param name="sourceCode">The source code to parse.</param>
    /// <param name="root">The parsed compilation unit root (output parameter).</param>
    /// <param name="syntaxTree">The parsed syntax tree (output parameter).</param>
    /// <returns>A RefactoringResult indicating success or failure. If failed, contains error details.</returns>
    protected RefactoringResult ParseAndValidateSyntax(
        string sourceCode,
        out CompilationUnitSyntax? root,
        out SyntaxTree? syntaxTree)
    {
        CurrentPhase = "Syntax Parsing";
        root = null;
        syntaxTree = null;

        try
        {
            syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            root = (CompilationUnitSyntax)syntaxTree.GetRoot();

            // Check for parse errors
            var diagnostics = syntaxTree.GetDiagnostics();
            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            if (errors.Any())
            {
                var errorMessages = string.Join(", ", errors.Select(e => e.GetMessage()).Take(3));
                Logger?.LogWarning("Syntax parsing found {Count} errors: {Errors}", errors.Count, errorMessages);
                return RefactoringResult.Failure($"Syntax errors in source code: {errorMessages}");
            }

            // Return validation success without refactored code (validation helper, not actual refactoring)
            return new RefactoringResult
            {
                IsSuccess = true,
                RefactoredCode = null,
                Message = "Syntax validation passed"
            };
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    /// <summary>
    /// Gets or creates a compilation for semantic analysis with common assembly references.
    /// Uses ConditionalWeakTable for object identity caching to improve performance when processing
    /// the same syntax tree multiple times. The cache automatically handles garbage collection and
    /// prevents hash collision issues. Tracks cache hit/miss metrics for performance monitoring.
    /// </summary>
    /// <param name="syntaxTree">The syntax tree to include in the compilation.</param>
    /// <returns>A CSharpCompilation instance configured with standard references.</returns>
    /// <remarks>
    /// The cache uses the SyntaxTree instance as the key (object identity), ensuring no hash collisions.
    /// Compilations are automatically removed when the SyntaxTree is garbage collected.
    /// This approach is thread-safe and requires no manual cache management.
    /// Cache metrics are logged at Debug level when a logger is available.
    /// </remarks>
    protected CSharpCompilation CreateCompilation(SyntaxTree syntaxTree)
    {
        // Check if compilation exists in cache
        bool cacheHit = _compilationCache.TryGetValue(syntaxTree, out var cachedCompilation);

        if (cacheHit)
        {
            // Cache hit - return existing compilation
            System.Threading.Interlocked.Increment(ref _cacheHits);
            Logger?.LogDebug("Compilation cache hit (total hits: {Hits}, misses: {Misses}, hit rate: {HitRate:P1})",
                _cacheHits, _cacheMisses, (double)_cacheHits / (_cacheHits + _cacheMisses));
            return cachedCompilation!;
        }

        // Cache miss - create new compilation
        System.Threading.Interlocked.Increment(ref _cacheMisses);

        var newCompilation = _compilationCache.GetValue(syntaxTree, tree =>
        {
            Logger?.LogDebug("Creating new compilation (total hits: {Hits}, misses: {Misses}, hit rate: {HitRate:P1})",
                _cacheHits, _cacheMisses, (double)_cacheHits / Math.Max(1, _cacheHits + _cacheMisses));

            // Create new compilation with standard references
            return CSharpCompilation.Create("temp")
                .AddReferences(
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location), // mscorlib/System.Private.CoreLib
                    MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location), // System.Collections
                    MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location) // System.Linq
                )
                .AddSyntaxTrees(tree);
        });

        return newCompilation;
    }

    /// <summary>
    /// Finds a class declaration by name in the syntax root.
    /// </summary>
    /// <param name="root">The compilation unit root to search.</param>
    /// <param name="className">The name of the class to find.</param>
    /// <returns>The class declaration if found; otherwise, null.</returns>
    protected ClassDeclarationSyntax? FindClass(CompilationUnitSyntax root, string className)
    {
        return root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == className);
    }

    /// <summary>
    /// Finds a method declaration by name within a class.
    /// </summary>
    /// <param name="classDeclaration">The class to search.</param>
    /// <param name="methodName">The name of the method to find.</param>
    /// <returns>The method declaration if found; otherwise, null.</returns>
    protected MethodDeclarationSyntax? FindMethod(ClassDeclarationSyntax classDeclaration, string methodName)
    {
        return classDeclaration.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == methodName);
    }

    /// <summary>
    /// Handles exceptions by creating structured error context, logging details, and returning sanitized messages.
    /// </summary>
    /// <param name="ex">The exception to handle.</param>
    /// <param name="operationName">The name of the operation (for error messages).</param>
    /// <param name="sourceLocation">Optional source location where the error occurred.</param>
    /// <returns>A RefactoringResult with a sanitized error message.</returns>
    protected RefactoringResult HandleException(
        Exception ex,
        string operationName = "refactoring",
        Microsoft.CodeAnalysis.Text.LinePosition? sourceLocation = null)
    {
        // Create structured error context
        var errorContext = RefactoringErrorContext.FromException(ex, CurrentPhase, sourceLocation);

        // Add operation name to additional context
        errorContext.AdditionalContext["Operation"] = operationName;

        // Log detailed error information (full exception) for debugging
        Logger?.LogError(ex, errorContext.ToLogMessage());

        // Determine error category string for backwards compatibility with existing tests
        var errorCategory = errorContext.Category switch
        {
            ErrorCategory.InvalidInput => "InvalidInput",
            ErrorCategory.InvalidState => "InvalidState",
            ErrorCategory.ParseError => "ParseError",
            ErrorCategory.SymbolResolution => "SymbolResolution",
            ErrorCategory.ValidationFailure => "ValidationFailure",
            _ => "UnexpectedError"
        };

        // Return sanitized user-friendly message with error category (for backwards compatibility)
        var userMessage = $"An error occurred during {operationName} ({errorCategory}). Please check the code syntax and try again.";
        return RefactoringResult.Failure(userMessage);
    }

    /// <summary>
    /// Performs framework-aware validation wrapping for a refactoring operation.
    /// This is a template method that handles input validation, executes the refactoring, and validates output.
    /// </summary>
    /// <param name="sourceCode">The source code to refactor.</param>
    /// <param name="targetFramework">The target framework moniker (e.g., "net8.0", "net48").</param>
    /// <param name="refactoringOperation">The core refactoring operation to execute.</param>
    /// <returns>A RefactoringResult with validation details.</returns>
    protected async Task<RefactoringResult> ExecuteWithValidationAsync(
        string sourceCode,
        string targetFramework,
        Func<Task<RefactoringResult>> refactoringOperation)
    {
        // Single null-check pattern: cache tracker reference to avoid repeated null checks
        var tracker = MetricsTracker;
        if (tracker != null)
        {
            tracker.RecordInput(sourceCode);
            tracker.Metrics.TargetFramework = targetFramework;
        }

        // Step 1: Validate input code against target framework
        CurrentPhase = "Input Validation";
        var validator = new SyntaxValidator();
        try
        {
            var inputValidation = await validator.ValidateInputAsync(sourceCode, targetFramework);

            if (!inputValidation.IsValid)
            {
                Logger?.LogWarning("Input validation failed for framework {Framework}: {Error}",
                    targetFramework, inputValidation.ErrorMessage);
                tracker?.RecordFailure(ErrorCategory.ValidationFailure, CurrentPhase);
                return RefactoringResult.ValidationFailure(inputValidation);
            }

            // Step 2: Perform refactoring (delegate to provided operation)
            CurrentPhase = "Refactoring Execution";
            var refactoringResult = await refactoringOperation();

            if (!refactoringResult.IsSuccess)
            {
                tracker?.RecordFailure(ErrorCategory.UnexpectedError, CurrentPhase);
                return refactoringResult;
            }

            // Step 3: Validate refactored code is not empty
            CurrentPhase = "Output Validation";
            if (string.IsNullOrWhiteSpace(refactoringResult.RefactoredCode))
            {
                Logger?.LogError("Refactoring succeeded but produced no output code");
                tracker?.RecordFailure(ErrorCategory.InvalidState, CurrentPhase);
                return RefactoringResult.Failure("Refactoring succeeded but produced no output code.");
            }

            // Record output metrics
            tracker?.RecordOutput(refactoringResult.RefactoredCode);

            // Step 4: Validate output code against target framework
            var outputValidation = await validator.ValidateOutputAsync(refactoringResult.RefactoredCode, targetFramework);

            if (!outputValidation.IsValid)
            {
                Logger?.LogWarning("Output validation failed for framework {Framework}: {Error}",
                    targetFramework, outputValidation.ErrorMessage);
                tracker?.RecordFailure(ErrorCategory.ValidationFailure, CurrentPhase);
                return RefactoringResult.ValidationFailure(outputValidation);
            }

            CurrentPhase = "Completed";
            tracker?.RecordSuccess(CurrentPhase);
            Logger?.LogInformation("Refactoring completed successfully for framework {Framework}. {Metrics}",
                targetFramework, tracker?.Metrics.ToSummary() ?? "No metrics");
            return refactoringResult;
        }
        finally
        {
            validator.Dispose();
        }
    }

    /// <summary>
    /// Normalizes whitespace in a syntax node to ensure proper formatting, or preserves original formatting based on options.
    /// </summary>
    /// <param name="node">The syntax node to process.</param>
    /// <param name="options">Optional refactoring options controlling formatting behavior. If null, uses default (normalize whitespace).</param>
    /// <returns>The processed syntax node with whitespace normalized or preserved based on options.</returns>
    protected T NormalizeWhitespace<T>(T node, RefactoringOptions? options = null) where T : SyntaxNode
    {
        options ??= RefactoringOptions.Default;

        // If preserving formatting, return node as-is
        if (options.PreserveFormatting)
        {
            return node;
        }

        // Otherwise, normalize whitespace for consistent formatting
        return node.NormalizeWhitespace();
    }
}
