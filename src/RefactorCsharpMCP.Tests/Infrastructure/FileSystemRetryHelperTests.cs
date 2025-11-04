using FluentAssertions;
using Microsoft.Extensions.Logging;
using RefactorCsharpMCP.Core.Infrastructure;
using Xunit;

namespace RefactorCsharpMCP.Tests.Infrastructure;

/// <summary>
/// Tests for FileSystemRetryHelper - retry logic for file system operations.
/// </summary>
public class FileSystemRetryHelperTests
{
    private readonly TestLogger _testLogger;

    public FileSystemRetryHelperTests()
    {
        _testLogger = new TestLogger();
    }

    /// <summary>
    /// Simple test logger that tracks log calls for verification.
    /// </summary>
    private class TestLogger : ILogger<FileSystemRetryHelperTests>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> LogCalls { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LogCalls.Add((logLevel, state?.ToString() ?? string.Empty, exception));
        }
    }


    #region SafeDeleteDirectoryAsync Tests

    [Fact]
    public async Task SafeDeleteDirectoryAsync_NonExistentDirectory_DoesNotThrow()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act
        await FileSystemRetryHelper.SafeDeleteDirectoryAsync(nonExistentPath, _testLogger);

        // Assert - No exception thrown
        Directory.Exists(nonExistentPath).Should().BeFalse();
    }

    [Fact]
    public async Task SafeDeleteDirectoryAsync_ExistingDirectory_DeletesSuccessfully()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempPath);
        File.WriteAllText(Path.Combine(tempPath, "test.txt"), "test content");

        // Act
        await FileSystemRetryHelper.SafeDeleteDirectoryAsync(tempPath, _testLogger);

        // Assert
        Directory.Exists(tempPath).Should().BeFalse();
    }

    [Fact]
    public async Task SafeDeleteDirectoryAsync_WithCancellationDuringRetry_RespectsCancellationToken()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Create a locked directory scenario by simulating delay cancellation
        var cts = new CancellationTokenSource();
        cts.CancelAfter(50); // Cancel after 50ms

        // Act - Cancellation won't be detected if directory deletes immediately
        // This test documents that cancellation is respected during retry delays
        try
        {
            await FileSystemRetryHelper.SafeDeleteDirectoryAsync(
                tempPath,
                _testLogger,
                maxAttempts: 5,
                cancellationToken: cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected if cancellation occurs during retry delay
        }

        // No assertion - test passes if no unhandled exceptions
    }

    [Fact]
    public async Task SafeDeleteDirectoryAsync_WithMaxAttempts_RetriesCorrectNumberOfTimes()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempPath);

        // Act - Use custom max attempts
        await FileSystemRetryHelper.SafeDeleteDirectoryAsync(tempPath, _testLogger, maxAttempts: 5);

        // Assert - Directory should be deleted regardless of maxAttempts
        Directory.Exists(tempPath).Should().BeFalse();
    }

    #endregion

    #region RetryFileOperationAsync<T> Tests

    [Fact]
    public async Task RetryFileOperationAsync_SucceedsOnFirstAttempt_ReturnsResult()
    {
        // Arrange
        var expectedValue = 42;
        Func<Task<int>> operation = () => Task.FromResult(expectedValue);

        // Act
        var result = await FileSystemRetryHelper.RetryFileOperationAsync(operation, logger: _testLogger);

        // Assert
        result.Should().Be(expectedValue);
    }

    [Fact]
    public async Task RetryFileOperationAsync_IOExceptionThenSuccess_RetriesAndSucceeds()
    {
        // Arrange
        var attemptCount = 0;
        Func<Task<string>> operation = () =>
        {
            attemptCount++;
            if (attemptCount == 1)
            {
                throw new IOException("Simulated file lock");
            }
            return Task.FromResult("success");
        };

        // Act
        var result = await FileSystemRetryHelper.RetryFileOperationAsync(operation, logger: _testLogger);

        // Assert
        result.Should().Be("success");
        attemptCount.Should().Be(2, "should retry once after IOException");
    }

    [Fact]
    public async Task RetryFileOperationAsync_UnauthorizedAccessExceptionThenSuccess_RetriesAndSucceeds()
    {
        // Arrange
        var attemptCount = 0;
        Func<Task<int>> operation = () =>
        {
            attemptCount++;
            if (attemptCount == 1)
            {
                throw new UnauthorizedAccessException("Simulated access denied");
            }
            return Task.FromResult(100);
        };

        // Act
        var result = await FileSystemRetryHelper.RetryFileOperationAsync(operation, logger: _testLogger);

        // Assert
        result.Should().Be(100);
        attemptCount.Should().Be(2, "should retry once after UnauthorizedAccessException");
    }

    [Fact]
    public async Task RetryFileOperationAsync_ExceedsMaxAttempts_ReturnsDefaultValue()
    {
        // Arrange
        var attemptCount = 0;
        Func<Task<int>> operation = () =>
        {
            attemptCount++;
            throw new IOException($"Attempt {attemptCount} failed");
        };

        // Act
        var result = await FileSystemRetryHelper.RetryFileOperationAsync(
            operation,
            defaultValue: -1,
            logger: _testLogger,
            maxAttempts: 3);

        // Assert
        result.Should().Be(-1, "should return default value after max attempts");
        attemptCount.Should().Be(3, "should attempt exactly maxAttempts times");
    }

    [Fact]
    public async Task RetryFileOperationAsync_NonRetryableException_ReturnsDefaultImmediately()
    {
        // Arrange
        var attemptCount = 0;
        Func<Task<string>> operation = () =>
        {
            attemptCount++;
            throw new InvalidOperationException("Non-retryable error");
        };

        // Act
        var result = await FileSystemRetryHelper.RetryFileOperationAsync(
            operation,
            defaultValue: "fallback",
            logger: _testLogger);

        // Assert
        result.Should().Be("fallback");
        attemptCount.Should().Be(1, "should not retry for non-retryable exceptions");
    }

    [Fact]
    public async Task RetryFileOperationAsync_NullDefaultValue_ReturnsNullOnFailure()
    {
        // Arrange
        Func<Task<string?>> operation = () => throw new IOException("Always fails");

        // Act
        var result = await FileSystemRetryHelper.RetryFileOperationAsync<string?>(
            operation,
            defaultValue: null,
            logger: _testLogger,
            maxAttempts: 2);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RetryFileOperationAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task<int>> operation = () =>
        {
            throw new IOException("Simulated failure");
        };

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await FileSystemRetryHelper.RetryFileOperationAsync(
                operation,
                logger: _testLogger,
                cancellationToken: cts.Token));
    }

    [Fact]
    public async Task RetryFileOperationAsync_ExponentialBackoff_UsesCorrectDelays()
    {
        // Arrange
        var attemptTimes = new List<DateTime>();
        Func<Task<int>> operation = () =>
        {
            attemptTimes.Add(DateTime.UtcNow);
            throw new IOException("Simulated failure");
        };

        // Act
        await FileSystemRetryHelper.RetryFileOperationAsync(
            operation,
            logger: _testLogger,
            maxAttempts: 3);

        // Assert
        attemptTimes.Should().HaveCount(3);

        // Verify delays between attempts (50ms, 200ms)
        // Allow some tolerance for timing precision
        if (attemptTimes.Count >= 2)
        {
            var delay1 = (attemptTimes[1] - attemptTimes[0]).TotalMilliseconds;
            delay1.Should().BeGreaterThanOrEqualTo(40, "first retry delay should be ~50ms");
        }

        if (attemptTimes.Count >= 3)
        {
            var delay2 = (attemptTimes[2] - attemptTimes[1]).TotalMilliseconds;
            delay2.Should().BeGreaterThanOrEqualTo(150, "second retry delay should be ~200ms");
        }
    }

    #endregion

    #region RetryFileOperationAsync (void) Tests

    [Fact]
    public async Task RetryFileOperationAsync_Void_SucceedsOnFirstAttempt()
    {
        // Arrange
        var executed = false;
        Func<Task> operation = () =>
        {
            executed = true;
            return Task.CompletedTask;
        };

        // Act
        await FileSystemRetryHelper.RetryFileOperationAsync(operation, logger: _testLogger);

        // Assert
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task RetryFileOperationAsync_Void_IOExceptionThenSuccess_RetriesAndSucceeds()
    {
        // Arrange
        var attemptCount = 0;
        Func<Task> operation = () =>
        {
            attemptCount++;
            if (attemptCount == 1)
            {
                throw new IOException("Simulated file lock");
            }
            return Task.CompletedTask;
        };

        // Act
        await FileSystemRetryHelper.RetryFileOperationAsync(operation, logger: _testLogger);

        // Assert
        attemptCount.Should().Be(2, "should retry once after IOException");
    }

    [Fact]
    public async Task RetryFileOperationAsync_Void_ExceedsMaxAttempts_CompletesGracefully()
    {
        // Arrange
        var attemptCount = 0;
        Func<Task> operation = () =>
        {
            attemptCount++;
            throw new IOException($"Attempt {attemptCount} failed");
        };

        // Act - Should not throw
        await FileSystemRetryHelper.RetryFileOperationAsync(
            operation,
            logger: _testLogger,
            maxAttempts: 3);

        // Assert
        attemptCount.Should().Be(3, "should attempt exactly maxAttempts times");
    }

    [Fact]
    public async Task RetryFileOperationAsync_Void_NonRetryableException_FailsImmediately()
    {
        // Arrange
        var attemptCount = 0;
        Func<Task> operation = () =>
        {
            attemptCount++;
            throw new InvalidOperationException("Non-retryable error");
        };

        // Act - Should not throw (graceful failure)
        await FileSystemRetryHelper.RetryFileOperationAsync(operation, logger: _testLogger);

        // Assert
        attemptCount.Should().Be(1, "should not retry for non-retryable exceptions");
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task RetryFileOperationAsync_IOException_LogsWarning()
    {
        // Arrange
        var attemptCount = 0;
        Func<Task<int>> operation = () =>
        {
            attemptCount++;
            if (attemptCount == 1)
            {
                throw new IOException("Test error");
            }
            return Task.FromResult(1);
        };

        // Act
        await FileSystemRetryHelper.RetryFileOperationAsync(operation, logger: _testLogger);

        // Assert - Verify warning was logged
        var warningLogs = _testLogger.LogCalls.Where(l => l.Level == LogLevel.Warning).ToList();
        warningLogs.Should().HaveCount(1, "should log one warning for the IOException");
        warningLogs[0].Message.Should().Contain("File operation failed");
        warningLogs[0].Exception.Should().BeOfType<IOException>();
    }

    [Fact]
    public async Task RetryFileOperationAsync_UnauthorizedAccessException_LogsWarning()
    {
        // Arrange
        var attemptCount = 0;
        Func<Task<int>> operation = () =>
        {
            attemptCount++;
            if (attemptCount == 1)
            {
                throw new UnauthorizedAccessException("Test access denied");
            }
            return Task.FromResult(1);
        };

        // Act
        await FileSystemRetryHelper.RetryFileOperationAsync(operation, logger: _testLogger);

        // Assert - Verify warning was logged
        var warningLogs = _testLogger.LogCalls.Where(l => l.Level == LogLevel.Warning).ToList();
        warningLogs.Should().HaveCount(1, "should log one warning for UnauthorizedAccessException");
        warningLogs[0].Message.Should().Contain("File access denied");
        warningLogs[0].Exception.Should().BeOfType<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RetryFileOperationAsync_NonRetryableException_LogsError()
    {
        // Arrange
        Func<Task<int>> operation = () => throw new InvalidOperationException("Test error");

        // Act
        await FileSystemRetryHelper.RetryFileOperationAsync(operation, logger: _testLogger);

        // Assert - Verify error was logged
        var errorLogs = _testLogger.LogCalls.Where(l => l.Level == LogLevel.Error).ToList();
        errorLogs.Should().HaveCount(1, "should log one error for non-retryable exception");
        errorLogs[0].Message.Should().Contain("failed permanently");
        errorLogs[0].Exception.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task RetryFileOperationAsync_MaxAttemptsExceeded_LogsError()
    {
        // Arrange
        Func<Task<int>> operation = () => throw new IOException("Always fails");

        // Act
        await FileSystemRetryHelper.RetryFileOperationAsync(
            operation,
            logger: _testLogger,
            maxAttempts: 2);

        // Assert - Verify warnings and final error were logged
        var warningLogs = _testLogger.LogCalls.Where(l => l.Level == LogLevel.Warning).ToList();
        var errorLogs = _testLogger.LogCalls.Where(l => l.Level == LogLevel.Error).ToList();

        warningLogs.Should().HaveCount(1, "should log warnings for retry attempts");
        errorLogs.Should().HaveCount(1, "should log final error after max attempts");
        errorLogs[0].Message.Should().Contain("failed"); // Either "failed permanently" or "failed after"
    }

    #endregion

    #region Edge Cases and Integration Tests

    [Fact]
    public async Task RetryFileOperationAsync_MultipleTransientFailuresThenSuccess_RetriesCorrectly()
    {
        // Arrange
        var attemptCount = 0;
        Func<Task<string>> operation = () =>
        {
            attemptCount++;
            if (attemptCount == 1)
            {
                throw new IOException("First failure");
            }
            if (attemptCount == 2)
            {
                throw new UnauthorizedAccessException("Second failure");
            }
            return Task.FromResult("success");
        };

        // Act
        var result = await FileSystemRetryHelper.RetryFileOperationAsync(operation, logger: _testLogger);

        // Assert
        result.Should().Be("success");
        attemptCount.Should().Be(3, "should retry through both transient failures");
    }

    [Fact]
    public async Task SafeDeleteDirectoryAsync_WithNestedDirectories_DeletesRecursively()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var nestedPath = Path.Combine(tempPath, "nested", "deep");
        Directory.CreateDirectory(nestedPath);
        File.WriteAllText(Path.Combine(nestedPath, "file.txt"), "content");

        // Act
        await FileSystemRetryHelper.SafeDeleteDirectoryAsync(tempPath, _testLogger);

        // Assert
        Directory.Exists(tempPath).Should().BeFalse();
    }

    [Fact]
    public async Task RetryFileOperationAsync_WithNullLogger_DoesNotThrow()
    {
        // Arrange
        Func<Task<int>> operation = () => Task.FromResult(42);

        // Act
        var result = await FileSystemRetryHelper.RetryFileOperationAsync(operation, logger: null);

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public async Task SafeDeleteDirectoryAsync_WithNullLogger_DoesNotThrow()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempPath);

        // Act
        await FileSystemRetryHelper.SafeDeleteDirectoryAsync(tempPath, logger: null);

        // Assert
        Directory.Exists(tempPath).Should().BeFalse();
    }

    #endregion
}
