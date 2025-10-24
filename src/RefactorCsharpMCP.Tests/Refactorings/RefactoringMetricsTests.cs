using RefactorCsharpMCP.Core.Refactorings;
using Xunit;

namespace RefactorCsharpMCP.Tests.Refactorings;

/// <summary>
/// Unit tests for RefactoringMetrics and RefactoringMetricsTracker classes.
/// Tests timing, line counting, metrics tracking, and summary generation.
/// </summary>
public class RefactoringMetricsTests
{
    [Fact]
    public void RefactoringMetrics_ForSuccess_CreatesSuccessMetrics()
    {
        // Act
        var metrics = RefactoringMetrics.ForSuccess("ExtractMethod");

        // Assert
        Assert.Equal("ExtractMethod", metrics.OperationName);
        Assert.True(metrics.Success);
        Assert.Null(metrics.ErrorCategory);
        Assert.InRange(metrics.StartTime, DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow);
    }

    [Fact]
    public void RefactoringMetrics_ForFailure_CreatesFailureMetrics()
    {
        // Act
        var metrics = RefactoringMetrics.ForFailure("MakeReadonly", ErrorCategory.ValidationFailure);

        // Assert
        Assert.Equal("MakeReadonly", metrics.OperationName);
        Assert.False(metrics.Success);
        Assert.Equal(ErrorCategory.ValidationFailure, metrics.ErrorCategory);
        Assert.InRange(metrics.StartTime, DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow);
    }

    [Fact]
    public void RefactoringMetrics_LinesChanged_CalculatesCorrectly()
    {
        // Arrange
        var metrics = new RefactoringMetrics
        {
            InputLineCount = 50,
            OutputLineCount = 75
        };

        // Act
        var linesChanged = metrics.LinesChanged;

        // Assert
        Assert.Equal(25, linesChanged);
    }

    [Fact]
    public void RefactoringMetrics_LinesChanged_HandlesNegativeDifference()
    {
        // Arrange
        var metrics = new RefactoringMetrics
        {
            InputLineCount = 100,
            OutputLineCount = 60
        };

        // Act
        var linesChanged = metrics.LinesChanged;

        // Assert
        Assert.Equal(40, linesChanged); // Absolute value
    }

    [Fact]
    public void RefactoringMetrics_ToSummary_FormatsSuccessCorrectly()
    {
        // Arrange
        var metrics = new RefactoringMetrics
        {
            OperationName = "ExtractMethod",
            Success = true,
            InputLineCount = 50,
            OutputLineCount = 75,
            NodesAffected = 12,
            TargetFramework = "net8.0",
            CompletionPhase = "Completed",
            ElapsedTime = TimeSpan.FromMilliseconds(123.45)
        };

        // Act
        var summary = metrics.ToSummary();

        // Assert
        Assert.Contains("[ExtractMethod]", summary);
        Assert.Contains("Success", summary);
        Assert.Contains("123.45ms", summary);
        Assert.Contains("Lines: 50→75", summary);
        Assert.Contains("Nodes: 12", summary);
        Assert.Contains("Framework: net8.0", summary);
        Assert.Contains("Phase: Completed", summary);
    }

    [Fact]
    public void RefactoringMetrics_ToSummary_FormatsFailureCorrectly()
    {
        // Arrange
        var metrics = new RefactoringMetrics
        {
            OperationName = "SafeDelete",
            Success = false,
            ErrorCategory = ErrorCategory.SymbolResolution,
            InputLineCount = 30,
            OutputLineCount = 0,
            NodesAffected = 0,
            TargetFramework = "net48",
            CompletionPhase = "Semantic Analysis",
            ElapsedTime = TimeSpan.FromMilliseconds(45.67)
        };

        // Act
        var summary = metrics.ToSummary();

        // Assert
        Assert.Contains("[SafeDelete]", summary);
        Assert.Contains("Failed (SymbolResolution)", summary);
        Assert.Contains("45.67ms", summary);
        Assert.Contains("Lines: 30→0", summary);
        Assert.Contains("Nodes: 0", summary);
        Assert.Contains("Framework: net48", summary);
        Assert.Contains("Phase: Semantic Analysis", summary);
    }

    [Fact]
    public void RefactoringMetrics_ToSummary_HandlesNullElapsedTime()
    {
        // Arrange
        var metrics = new RefactoringMetrics
        {
            OperationName = "Test",
            Success = true,
            ElapsedTime = null
        };

        // Act
        var summary = metrics.ToSummary();

        // Assert
        Assert.Contains("Duration: N/A", summary);
    }

    [Fact]
    public void RefactoringMetricsTracker_Initializes_WithCorrectDefaults()
    {
        // Act
        using var tracker = new RefactoringMetricsTracker("TestOperation");

        // Assert
        Assert.Equal("TestOperation", tracker.Metrics.OperationName);
        Assert.InRange(tracker.Metrics.StartTime, DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow);
        Assert.Null(tracker.Metrics.EndTime);
        Assert.Null(tracker.Metrics.ElapsedTime);
    }

    [Fact]
    public void RefactoringMetricsTracker_RecordInput_CountsLinesCorrectly()
    {
        // Arrange
        using var tracker = new RefactoringMetricsTracker("Test");
        var sourceCode = "using System;\n\nclass Foo {\n    void Bar() { }\n}";

        // Act
        tracker.RecordInput(sourceCode);

        // Assert
        // SourceText.From() correctly counts 5 lines (including empty line)
        Assert.Equal(5, tracker.Metrics.InputLineCount);
    }

    [Fact]
    public void RefactoringMetricsTracker_RecordInput_HandlesEmptyString()
    {
        // Arrange
        using var tracker = new RefactoringMetricsTracker("Test");

        // Act
        tracker.RecordInput("");

        // Assert
        // SourceText.From("") returns 1 line (the empty line)
        Assert.Equal(1, tracker.Metrics.InputLineCount);
    }

    [Fact]
    public void RefactoringMetricsTracker_RecordInput_HandlesSingleLine()
    {
        // Arrange
        using var tracker = new RefactoringMetricsTracker("Test");

        // Act
        tracker.RecordInput("class Foo { }");

        // Assert
        Assert.Equal(1, tracker.Metrics.InputLineCount);
    }

    [Fact]
    public void RefactoringMetricsTracker_RecordOutput_CountsLinesCorrectly()
    {
        // Arrange
        using var tracker = new RefactoringMetricsTracker("Test");
        var sourceCode = "class Foo\n{\n    void Bar()\n    {\n        Console.WriteLine();\n    }\n}\n";

        // Act
        tracker.RecordOutput(sourceCode);

        // Assert
        // SourceText counts the lines correctly (8 lines: 7 text lines + 1 empty line after trailing newline)
        Assert.Equal(8, tracker.Metrics.OutputLineCount);
    }

    [Fact]
    public void RefactoringMetricsTracker_RecordSuccess_SetsPropertiesAndStops()
    {
        // Arrange
        using var tracker = new RefactoringMetricsTracker("Test");
        Thread.Sleep(10); // Ensure elapsed time > 0

        // Act
        tracker.RecordSuccess("Completed");

        // Assert
        Assert.True(tracker.Metrics.Success);
        Assert.Null(tracker.Metrics.ErrorCategory);
        Assert.Equal("Completed", tracker.Metrics.CompletionPhase);
        Assert.NotNull(tracker.Metrics.EndTime);
        Assert.NotNull(tracker.Metrics.ElapsedTime);
        Assert.True(tracker.Metrics.ElapsedTime.Value.TotalMilliseconds > 0);
    }

    [Fact]
    public void RefactoringMetricsTracker_RecordFailure_SetsPropertiesAndStops()
    {
        // Arrange
        using var tracker = new RefactoringMetricsTracker("Test");
        Thread.Sleep(10); // Ensure elapsed time > 0

        // Act
        tracker.RecordFailure(ErrorCategory.ParseError, "Syntax Parsing");

        // Assert
        Assert.False(tracker.Metrics.Success);
        Assert.Equal(ErrorCategory.ParseError, tracker.Metrics.ErrorCategory);
        Assert.Equal("Syntax Parsing", tracker.Metrics.CompletionPhase);
        Assert.NotNull(tracker.Metrics.EndTime);
        Assert.NotNull(tracker.Metrics.ElapsedTime);
        Assert.True(tracker.Metrics.ElapsedTime.Value.TotalMilliseconds > 0);
    }

    [Fact]
    public void RefactoringMetricsTracker_Stop_CanBeCalledMultipleTimes()
    {
        // Arrange
        using var tracker = new RefactoringMetricsTracker("Test");
        Thread.Sleep(10);

        // Act
        tracker.Stop();
        var firstEndTime = tracker.Metrics.EndTime;
        var firstElapsed = tracker.Metrics.ElapsedTime;

        Thread.Sleep(10);
        tracker.Stop(); // Second call

        // Assert
        Assert.Equal(firstEndTime, tracker.Metrics.EndTime); // Unchanged
        Assert.Equal(firstElapsed, tracker.Metrics.ElapsedTime); // Unchanged
    }

    [Fact]
    public void RefactoringMetricsTracker_Dispose_CallsStop()
    {
        // Arrange
        var tracker = new RefactoringMetricsTracker("Test");
        Thread.Sleep(10);

        // Act
        tracker.Dispose();

        // Assert
        Assert.NotNull(tracker.Metrics.EndTime);
        Assert.NotNull(tracker.Metrics.ElapsedTime);
    }

    [Fact]
    public void RefactoringMetricsTracker_UsesStopwatch_ForAccurateTiming()
    {
        // Arrange
        using var tracker = new RefactoringMetricsTracker("Test");

        // Act
        Thread.Sleep(50); // Sleep for 50ms
        tracker.Stop();

        // Assert
        Assert.NotNull(tracker.Metrics.ElapsedTime);
        // Elapsed time should be close to 50ms (allow some variance for thread scheduling)
        Assert.InRange(tracker.Metrics.ElapsedTime.Value.TotalMilliseconds, 40, 100);
    }

    [Fact]
    public void RefactoringMetrics_CustomMetrics_CanBeAdded()
    {
        // Arrange
        var metrics = new RefactoringMetrics();

        // Act
        metrics.CustomMetrics["SymbolsResolved"] = 42;
        metrics.CustomMetrics["CacheHitRate"] = 0.85;
        metrics.CustomMetrics["MemoryUsedMB"] = 12.5;

        // Assert
        Assert.Equal(3, metrics.CustomMetrics.Count);
        Assert.Equal(42, metrics.CustomMetrics["SymbolsResolved"]);
        Assert.Equal(0.85, metrics.CustomMetrics["CacheHitRate"]);
        Assert.Equal(12.5, metrics.CustomMetrics["MemoryUsedMB"]);
    }

    [Fact]
    public void RefactoringMetrics_UsedCompilationCache_TracksOptimization()
    {
        // Arrange
        var metrics = new RefactoringMetrics
        {
            UsedCompilationCache = true
        };

        // Assert
        Assert.True(metrics.UsedCompilationCache);
    }
}
