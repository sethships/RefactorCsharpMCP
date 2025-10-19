using Microsoft.Extensions.Logging;

namespace RefactorCsharpMCP.Core.Infrastructure;

/// <summary>
/// Provides retry logic for file system operations to handle transient failures like file locking,
/// access denied errors, and race conditions. Uses async patterns with Task.Delay to avoid blocking threads.
/// </summary>
public static class FileSystemRetryHelper
{
    /// <summary>
    /// Safely deletes a directory with exponential backoff retry logic to handle locked files.
    /// Common in test scenarios where DLLs may be loaded or files are still being accessed.
    /// </summary>
    /// <param name="path">The directory path to delete.</param>
    /// <param name="logger">Optional logger for diagnostic messages.</param>
    /// <param name="maxAttempts">Maximum number of retry attempts (default: 3).</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Uses exponential backoff: 200ms, 400ms, 600ms delays between attempts.
    /// Triggers garbage collection before retries to help release file handles.
    /// Logs warnings on failures but continues gracefully - does not throw on final failure.
    /// </remarks>
    public static async Task SafeDeleteDirectoryAsync(
        string path,
        ILogger? logger = null,
        int maxAttempts = 3,
        CancellationToken cancellationToken = default)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
                return; // Success
            }
            catch (IOException ex) when (attempt < maxAttempts - 1)
            {
                // File locked (likely DLL loaded by another test) - wait and retry
                logger?.LogWarning(ex, "Failed to delete directory {Path} (attempt {Attempt}/{Max}), retrying after GC",
                    path, attempt + 1, maxAttempts);

                // Help release file handles by triggering GC
                GC.Collect();
                GC.WaitForPendingFinalizers();

                // Exponential backoff: 200ms, 400ms, 600ms
                await Task.Delay(200 * (attempt + 1), cancellationToken);
            }
            catch (UnauthorizedAccessException ex) when (attempt < maxAttempts - 1)
            {
                logger?.LogWarning(ex, "Access denied deleting {Path} (attempt {Attempt}/{Max}), retrying",
                    path, attempt + 1, maxAttempts);

                await Task.Delay(200 * (attempt + 1), cancellationToken);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Could not delete directory {Path} after {Attempt} attempts - ignoring",
                    path, attempt + 1);
                return; // Don't throw - graceful degradation
            }
        }

        // Final attempt failed but didn't throw - log and continue
        logger?.LogWarning("Could not delete directory {Path} after {Max} attempts - cache may be incomplete",
            path, maxAttempts);
    }

    /// <summary>
    /// Retries a file operation with exponential backoff to handle transient file system errors.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The async operation to retry.</param>
    /// <param name="defaultValue">The default value to return on permanent failure.</param>
    /// <param name="logger">Optional logger for diagnostic messages.</param>
    /// <param name="maxAttempts">Maximum number of retry attempts (default: 3).</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
    /// <returns>The result of the operation or the default value on failure.</returns>
    /// <remarks>
    /// Uses exponential backoff: 50ms, 200ms, 500ms delays between attempts.
    /// Catches IOException and UnauthorizedAccessException for retry, all other exceptions fail immediately.
    /// </remarks>
    public static async Task<T?> RetryFileOperationAsync<T>(
        Func<Task<T>> operation,
        T? defaultValue = default,
        ILogger? logger = null,
        int maxAttempts = 3,
        CancellationToken cancellationToken = default)
    {
        var delays = new[] { 50, 200, 500 }; // Exponential backoff in milliseconds

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (IOException ex) when (attempt < maxAttempts - 1)
            {
                logger?.LogWarning(ex, "File operation failed (attempt {Attempt}/{Max}), retrying after {Delay}ms",
                    attempt + 1, maxAttempts, delays[attempt]);
                await Task.Delay(delays[attempt], cancellationToken);
            }
            catch (UnauthorizedAccessException ex) when (attempt < maxAttempts - 1)
            {
                logger?.LogWarning(ex, "File access denied (attempt {Attempt}/{Max}), retrying after {Delay}ms",
                    attempt + 1, maxAttempts, delays[attempt]);
                await Task.Delay(delays[attempt], cancellationToken);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "File operation failed permanently after {Attempt} attempts", attempt + 1);
                return defaultValue;
            }
        }

        logger?.LogError("File operation failed after {MaxAttempts} attempts, returning default value", maxAttempts);
        return defaultValue;
    }

    /// <summary>
    /// Retries a void file operation with exponential backoff.
    /// </summary>
    /// <param name="operation">The async operation to retry.</param>
    /// <param name="logger">Optional logger for diagnostic messages.</param>
    /// <param name="maxAttempts">Maximum number of retry attempts (default: 3).</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task RetryFileOperationAsync(
        Func<Task> operation,
        ILogger? logger = null,
        int maxAttempts = 3,
        CancellationToken cancellationToken = default)
    {
        await RetryFileOperationAsync(async () =>
        {
            await operation();
            return (object?)null;
        }, logger: logger, maxAttempts: maxAttempts, cancellationToken: cancellationToken);
    }
}
