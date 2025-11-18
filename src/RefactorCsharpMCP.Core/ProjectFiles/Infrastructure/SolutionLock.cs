using System.Security;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RefactorCsharpMCP.Core.ProjectFiles.Infrastructure;

/// <summary>
/// Provides solution-level file-based locking to prevent race conditions
/// when multiple operations attempt to modify solution-wide files (e.g., Directory.*.props).
/// Uses a lock file to ensure exclusive access to solution-level resources.
/// </summary>
public class SolutionLock : IDisposable
{
    private const string LockFileExtension = ".refactor.lock";
    private const int DefaultLockTimeoutSeconds = 30;
    private const int LockRetryDelayMilliseconds = 100;

    private readonly ILogger<SolutionLock> _logger;
    private readonly string _lockFilePath;
    private FileStream? _lockFileStream;
    private bool _disposed;

    /// <summary>
    /// Creates a solution lock for the specified solution directory.
    /// </summary>
    /// <param name="solutionDirectory">The solution directory to lock.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <exception cref="SecurityException">If the path is invalid or attempts path traversal.</exception>
    public SolutionLock(string solutionDirectory, ILogger<SolutionLock>? logger = null)
    {
        _logger = logger ?? NullLogger<SolutionLock>.Instance;

        // Validate and normalize the directory path
        var validatedPath = PathValidator.ValidateDirectoryPath(solutionDirectory);

        _lockFilePath = Path.Combine(validatedPath, LockFileExtension);

        _logger.LogDebug("Created SolutionLock for directory: {Directory}", validatedPath);
    }

    /// <summary>
    /// Acquires the solution lock, waiting up to the specified timeout.
    /// </summary>
    /// <param name="timeoutSeconds">Maximum time to wait for the lock in seconds.</param>
    /// <returns>True if lock was acquired, false if timeout occurred.</returns>
    /// <exception cref="IOException">If lock file cannot be created or accessed.</exception>
    public async Task<bool> AcquireAsync(int timeoutSeconds = DefaultLockTimeoutSeconds)
    {
        if (_lockFileStream != null)
        {
            throw new InvalidOperationException("Lock already acquired");
        }

        // Check for stale lock file before attempting acquisition
        if (File.Exists(_lockFilePath))
        {
            try
            {
                // Read lock file content to extract PID
                var lockContent = await File.ReadAllTextAsync(_lockFilePath);

                // Parse PID from format: "Locked at {timestamp} by PID {pid}"
                var match = Regex.Match(lockContent, @"by PID (\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var pid))
                {
                    // Check if process is still running
                    if (!IsProcessRunning(pid))
                    {
                        _logger.LogWarning(
                            "Detected stale lock from terminated process PID {Pid}, removing lock file: {LockFile}",
                            pid,
                            _lockFilePath);

                        File.Delete(_lockFilePath);

                        _logger.LogInformation("Successfully removed stale lock file");
                    }
                    else
                    {
                        _logger.LogDebug(
                            "Lock file exists and process PID {Pid} is still running",
                            pid);
                    }
                }
                else
                {
                    _logger.LogDebug("Could not parse PID from lock file, will retry acquisition normally");
                }
            }
            catch (Exception ex)
            {
                // Non-critical error - log and proceed with normal retry logic
                _logger.LogDebug(ex, "Failed to check/remove stale lock file, proceeding with normal acquisition");
            }
        }

        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        var attempt = 0;

        while (DateTime.UtcNow < deadline)
        {
            attempt++;

            try
            {
                // Attempt to create and exclusively lock the file
                _lockFileStream = new FileStream(
                    _lockFilePath,
                    FileMode.Create,
                    FileAccess.ReadWrite,
                    FileShare.None, // Exclusive access
                    bufferSize: 1,
                    FileOptions.DeleteOnClose); // Auto-cleanup on process exit

                // Write lock metadata
                var lockInfo = $"Locked at {DateTime.UtcNow:O} by PID {Environment.ProcessId}";
                var bytes = System.Text.Encoding.UTF8.GetBytes(lockInfo);
                await _lockFileStream.WriteAsync(bytes, 0, bytes.Length);
                await _lockFileStream.FlushAsync();

                _logger.LogInformation(
                    "Acquired solution lock: {LockFile} (attempt {Attempt})",
                    _lockFilePath,
                    attempt);

                return true;
            }
            catch (IOException)
            {
                // Lock file is held by another process, wait and retry
                _logger.LogDebug(
                    "Solution lock busy, retrying... (attempt {Attempt}, timeout in {Remaining}s)",
                    attempt,
                    (deadline - DateTime.UtcNow).TotalSeconds);

                await Task.Delay(LockRetryDelayMilliseconds);
            }
        }

        _logger.LogWarning(
            "Failed to acquire solution lock after {Attempts} attempts ({Timeout}s timeout)",
            attempt,
            timeoutSeconds);

        return false;
    }

    /// <summary>
    /// Releases the solution lock.
    /// </summary>
    public void Release()
    {
        if (_lockFileStream != null)
        {
            try
            {
                _lockFileStream.Dispose();
                _lockFileStream = null;

                _logger.LogDebug("Released solution lock: {LockFile}", _lockFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to release solution lock: {LockFile}", _lockFilePath);
            }
        }
    }

    /// <summary>
    /// Disposes the lock, releasing it if still held.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            Release();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Checks if a process with the given PID is currently running.
    /// </summary>
    /// <param name="pid">The process ID to check.</param>
    /// <returns>True if the process is running, false otherwise.</returns>
    private static bool IsProcessRunning(int pid)
    {
        try
        {
            var process = System.Diagnostics.Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            // Process with given PID does not exist
            return false;
        }
        catch (InvalidOperationException)
        {
            // Process has exited
            return false;
        }
    }
}
