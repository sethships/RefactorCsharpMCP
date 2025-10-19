using System.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Captures performance and operational metrics for refactoring operations.
/// Enables monitoring, telemetry, and performance analysis.
/// </summary>
public class RefactoringMetrics
{
    /// <summary>
    /// Gets or sets the name of the refactoring operation.
    /// </summary>
    public string OperationName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the refactoring started.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the refactoring completed.
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Gets the elapsed time for the refactoring operation.
    /// Populated by RefactoringMetricsTracker when Stop() is called.
    /// </summary>
    public TimeSpan? ElapsedTime { get; set; }

    /// <summary>
    /// Gets or sets whether the refactoring succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error category if the refactoring failed.
    /// </summary>
    public ErrorCategory? ErrorCategory { get; set; }

    /// <summary>
    /// Gets or sets the number of lines in the input source code.
    /// </summary>
    public int InputLineCount { get; set; }

    /// <summary>
    /// Gets or sets the number of lines in the output source code.
    /// </summary>
    public int OutputLineCount { get; set; }

    /// <summary>
    /// Gets the number of lines changed (added or removed).
    /// </summary>
    public int LinesChanged => Math.Abs(OutputLineCount - InputLineCount);

    /// <summary>
    /// Gets or sets the number of syntax nodes affected by the refactoring.
    /// </summary>
    public int NodesAffected { get; set; }

    /// <summary>
    /// Gets or sets the target framework for the refactoring.
    /// </summary>
    public string TargetFramework { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether compilation caching was used.
    /// </summary>
    public bool UsedCompilationCache { get; set; }

    /// <summary>
    /// Gets or sets the phase where the operation completed or failed.
    /// </summary>
    public string CompletionPhase { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional custom metrics.
    /// </summary>
    public Dictionary<string, object> CustomMetrics { get; set; } = new();

    /// <summary>
    /// Creates a formatted summary of the metrics.
    /// </summary>
    /// <returns>A human-readable metrics summary.</returns>
    public string ToSummary()
    {
        var status = Success ? "Success" : $"Failed ({ErrorCategory})";
        var duration = ElapsedTime.HasValue ? $"{ElapsedTime.Value.TotalMilliseconds:F2}ms" : "N/A";

        return $"[{OperationName}] {status} | Duration: {duration} | " +
               $"Lines: {InputLineCount}→{OutputLineCount} | " +
               $"Nodes: {NodesAffected} | Framework: {TargetFramework} | " +
               $"Phase: {CompletionPhase}";
    }

    /// <summary>
    /// Creates metrics for a successful refactoring operation.
    /// </summary>
    /// <param name="operationName">The name of the refactoring operation.</param>
    /// <returns>A RefactoringMetrics instance with success status.</returns>
    public static RefactoringMetrics ForSuccess(string operationName)
    {
        return new RefactoringMetrics
        {
            OperationName = operationName,
            StartTime = DateTime.UtcNow,
            Success = true
        };
    }

    /// <summary>
    /// Creates metrics for a failed refactoring operation.
    /// </summary>
    /// <param name="operationName">The name of the refactoring operation.</param>
    /// <param name="errorCategory">The error category.</param>
    /// <returns>A RefactoringMetrics instance with failure status.</returns>
    public static RefactoringMetrics ForFailure(string operationName, ErrorCategory errorCategory)
    {
        return new RefactoringMetrics
        {
            OperationName = operationName,
            StartTime = DateTime.UtcNow,
            Success = false,
            ErrorCategory = errorCategory
        };
    }
}

/// <summary>
/// Tracks refactoring metrics using a stopwatch for accurate timing.
/// Provides a convenient way to measure refactoring performance.
/// </summary>
public class RefactoringMetricsTracker : IDisposable
{
    private readonly Stopwatch _stopwatch;
    private readonly RefactoringMetrics _metrics;

    /// <summary>
    /// Gets the metrics being tracked.
    /// </summary>
    public RefactoringMetrics Metrics => _metrics;

    /// <summary>
    /// Initializes a new metrics tracker for a refactoring operation.
    /// </summary>
    /// <param name="operationName">The name of the refactoring operation.</param>
    public RefactoringMetricsTracker(string operationName)
    {
        _metrics = new RefactoringMetrics
        {
            OperationName = operationName,
            StartTime = DateTime.UtcNow
        };
        _stopwatch = Stopwatch.StartNew();
    }

    /// <summary>
    /// Records the input source code size.
    /// </summary>
    /// <param name="sourceCode">The input source code.</param>
    public void RecordInput(string sourceCode)
    {
        _metrics.InputLineCount = SourceText.From(sourceCode).Lines.Count;
    }

    /// <summary>
    /// Records the output source code size.
    /// </summary>
    /// <param name="sourceCode">The output source code.</param>
    public void RecordOutput(string sourceCode)
    {
        _metrics.OutputLineCount = SourceText.From(sourceCode).Lines.Count;
    }

    /// <summary>
    /// Records that the refactoring succeeded.
    /// </summary>
    /// <param name="phase">The completion phase.</param>
    public void RecordSuccess(string phase = "Completed")
    {
        _metrics.Success = true;
        _metrics.CompletionPhase = phase;
        Stop();
    }

    /// <summary>
    /// Records that the refactoring failed.
    /// </summary>
    /// <param name="errorCategory">The error category.</param>
    /// <param name="phase">The phase where the failure occurred.</param>
    public void RecordFailure(ErrorCategory errorCategory, string phase)
    {
        _metrics.Success = false;
        _metrics.ErrorCategory = errorCategory;
        _metrics.CompletionPhase = phase;
        Stop();
    }

    /// <summary>
    /// Stops the metrics tracker and records the end time.
    /// </summary>
    public void Stop()
    {
        if (_metrics.EndTime == null)
        {
            _stopwatch.Stop();
            _metrics.EndTime = DateTime.UtcNow;
            _metrics.ElapsedTime = _stopwatch.Elapsed;
        }
    }

    /// <summary>
    /// Disposes the tracker and ensures timing is stopped.
    /// </summary>
    public void Dispose()
    {
        Stop();
    }
}
