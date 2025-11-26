using System.Diagnostics;
using System.Security;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RefactorCsharpMCP.Core.ProjectFiles.Infrastructure;

/// <summary>
/// Validates that projects build successfully using the dotnet CLI.
/// Used to verify that refactoring operations don't break the build.
/// </summary>
public class BuildValidator
{
    private readonly ILogger<BuildValidator> _logger;
    private readonly Lazy<(bool available, string? version)> _dotnetAvailability;

    public BuildValidator(ILogger<BuildValidator>? logger = null)
    {
        _logger = logger ?? NullLogger<BuildValidator>.Instance;
        _dotnetAvailability = new Lazy<(bool, string?)>(CheckDotnetAvailability);
    }

    /// <summary>
    /// Checks if dotnet CLI is available on the system.
    /// </summary>
    private (bool available, string? version) CheckDotnetAvailability()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                _logger.LogWarning("Failed to start dotnet process");
                return (false, null);
            }

            var versionOutput = process.StandardOutput.ReadToEnd();
            var completed = process.WaitForExit(5000);

            if (completed && process.ExitCode == 0)
            {
                var version = versionOutput.Trim();
                _logger.LogDebug("dotnet CLI found, version: {Version}", version);
                return (true, version);
            }

            _logger.LogWarning("dotnet CLI check failed with exit code {ExitCode}", process.ExitCode);
            return (false, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "dotnet CLI not found or not accessible");
            return (false, null);
        }
    }

    /// <summary>
    /// Validates that a project builds successfully.
    /// </summary>
    /// <param name="projectPath">Path to the project file or directory containing the project.</param>
    /// <param name="timeoutSeconds">Timeout for the build operation in seconds.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the build operation.</param>
    /// <returns>Build validation result.</returns>
    /// <exception cref="SecurityException">If the path is invalid or attempts path traversal.</exception>
    public async Task<BuildValidationResult> ValidateBuildAsync(
        string projectPath,
        int timeoutSeconds = 300,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Check if dotnet CLI is available
            var (available, version) = _dotnetAvailability.Value;
            if (!available)
            {
                return BuildValidationResult.Failure(
                    "dotnet CLI not found. Please install .NET SDK from https://dot.net",
                    TimeSpan.Zero);
            }

            _logger.LogDebug("Using dotnet CLI version: {Version}", version);

            // Validate the path to prevent path traversal attacks
            // CRITICAL: Detect relative path traversal attempts (.., ., etc)
            // Allow absolute paths anywhere on the system for legitimate use cases
            if (!Path.IsPathRooted(projectPath) && projectPath.Contains(".."))
            {
                throw new SecurityException(
                    $"Relative path traversal detected: '{projectPath}'. " +
                    "Use absolute paths instead.");
            }

            string validatedPath;
            try
            {
                // Attempt to validate as a file path first (most common case)
                // No basePath - allow absolute paths anywhere, validate extension only
                validatedPath = PathValidator.ValidateAndNormalizePath(projectPath);
            }
            catch (SecurityException ex) when (ex.Message.Contains("Invalid file extension"))
            {
                // Invalid file extension - might be a directory or solution file
                // Check if it's a solution file first (before trying as directory)
                if (projectPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
                {
                    // Solution files are valid for building
                    validatedPath = Path.GetFullPath(projectPath);
                }
                else
                {
                    // Try as directory
                    try
                    {
                        validatedPath = PathValidator.ValidateDirectoryPath(projectPath);
                        // Verify it's actually a directory (not just a file with no extension)
                        if (File.Exists(validatedPath))
                        {
                            // It's a file, not a directory - re-throw original SecurityException
                            throw ex;
                        }
                    }
                    catch (SecurityException)
                    {
                        throw; // Re-throw security exceptions from ValidateDirectoryPath
                    }
                    catch
                    {
                        // ValidateDirectoryPath failed for other reasons - re-throw original exception
                        throw ex;
                    }
                }
            }
            catch (SecurityException)
            {
                // Security validation failed (e.g., path traversal) - re-throw to propagate to caller
                throw;
            }
            catch (ArgumentException)
            {
                // Invalid path format, try as directory
                try
                {
                    validatedPath = PathValidator.ValidateDirectoryPath(projectPath);
                }
                catch (SecurityException)
                {
                    // Security validation failed - re-throw to propagate to caller
                    throw;
                }
            }

            // Now check if the validated path exists
            if (!File.Exists(validatedPath) && !Directory.Exists(validatedPath))
            {
                return BuildValidationResult.Failure(
                    $"Project path not found: {projectPath}. " +
                    "Ensure the path exists and you have read permissions.",
                    TimeSpan.Zero);
            }

            _logger.LogInformation("Starting build validation for: {ProjectPath}", validatedPath);

            var (exitCode, output, errors) = await RunDotnetBuildAsync(validatedPath, timeoutSeconds, cancellationToken);

            stopwatch.Stop();

            if (exitCode == 0)
            {
                _logger.LogInformation(
                    "Build validation succeeded for {ProjectPath} in {Duration}ms",
                    projectPath,
                    stopwatch.ElapsedMilliseconds);

                return BuildValidationResult.Success(output, stopwatch.Elapsed);
            }
            else
            {
                var errorMessage = $"Build failed with exit code {exitCode}";
                _logger.LogWarning(
                    "Build validation failed for {ProjectPath}: {ErrorMessage}",
                    projectPath,
                    errorMessage);

                return BuildValidationResult.Failure(
                    errorMessage,
                    stopwatch.Elapsed,
                    output,
                    errors);
            }
        }
        catch (SecurityException)
        {
            // Security violations should propagate to caller - do not catch
            throw;
        }
        catch (TimeoutException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Build validation timed out for {ProjectPath}", projectPath);

            return BuildValidationResult.Failure(
                $"Build timed out after {timeoutSeconds} seconds",
                stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Build validation failed with exception for {ProjectPath}", projectPath);

            return BuildValidationResult.Failure(
                $"Build validation error: {ex.Message}",
                stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Validates that multiple projects build successfully.
    /// </summary>
    /// <param name="projectPaths">Paths to project files or directories.</param>
    /// <param name="timeoutSeconds">Timeout per project in seconds.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the build operations.</param>
    /// <returns>Dictionary of project path to build validation result.</returns>
    public async Task<Dictionary<string, BuildValidationResult>> ValidateBuildsAsync(
        IEnumerable<string> projectPaths,
        int timeoutSeconds = 300,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, BuildValidationResult>();

        foreach (var projectPath in projectPaths)
        {
            var result = await ValidateBuildAsync(projectPath, timeoutSeconds, cancellationToken);
            results[projectPath] = result;
        }

        return results;
    }

    /// <summary>
    /// Validates that a solution builds successfully.
    /// </summary>
    /// <param name="solutionPath">Path to the solution file (.sln).</param>
    /// <param name="timeoutSeconds">Timeout for the build operation in seconds.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the build operation.</param>
    /// <returns>Build validation result.</returns>
    public async Task<BuildValidationResult> ValidateSolutionBuildAsync(
        string solutionPath,
        int timeoutSeconds = 600,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(solutionPath))
        {
            return BuildValidationResult.Failure(
                $"Solution file not found: {solutionPath}",
                TimeSpan.Zero);
        }

        if (!solutionPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
        {
            return BuildValidationResult.Failure(
                $"Not a solution file: {solutionPath}",
                TimeSpan.Zero);
        }

        _logger.LogInformation("Starting solution build validation for: {SolutionPath}", solutionPath);

        return await ValidateBuildAsync(solutionPath, timeoutSeconds, cancellationToken);
    }

    /// <summary>
    /// Runs dotnet build command and captures output.
    /// </summary>
    private async Task<(int exitCode, string output, string errors)> RunDotnetBuildAsync(
        string targetPath,
        int timeoutSeconds,
        CancellationToken cancellationToken = default)
    {
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        // Use ArgumentList to prevent command injection vulnerabilities
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Add arguments individually - safe from command injection
        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add(targetPath);
        startInfo.ArgumentList.Add("--no-restore");
        startInfo.ArgumentList.Add("--nologo");

        using var process = new Process { StartInfo = startInfo };

        process.OutputDataReceived += (sender, args) =>
        {
            if (args.Data != null)
            {
                outputBuilder.AppendLine(args.Data);
            }
        };

        process.ErrorDataReceived += (sender, args) =>
        {
            if (args.Data != null)
            {
                errorBuilder.AppendLine(args.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var completed = await process.WaitForExitAsync(TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);

        if (!completed)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to kill build process");
            }

            throw new TimeoutException($"Build process timed out after {timeoutSeconds} seconds");
        }

        return (process.ExitCode, outputBuilder.ToString(), errorBuilder.ToString());
    }
}

/// <summary>
/// Extension methods for Process to support WaitForExitAsync with timeout.
/// </summary>
internal static class ProcessExtensions
{
    /// <summary>
    /// Waits for the process to exit with a timeout, respecting external cancellation tokens.
    /// </summary>
    /// <param name="process">The process to wait for.</param>
    /// <param name="timeout">The maximum time to wait.</param>
    /// <param name="cancellationToken">Optional external cancellation token.</param>
    /// <returns>True if the process exited within the timeout, false if timed out.</returns>
    /// <exception cref="OperationCanceledException">If the external cancellation token is triggered.</exception>
    public static async Task<bool> WaitForExitAsync(
        this Process process,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        // Link external cancellation token with timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // External cancellation - propagate
            throw;
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
            // Timeout occurred, not external cancellation
            return false;
        }
    }
}

/// <summary>
/// Represents the result of a build validation operation.
/// </summary>
public class BuildValidationResult
{
    /// <summary>
    /// Whether the build succeeded.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Build output (stdout).
    /// </summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>
    /// Build errors (stderr).
    /// </summary>
    public string Errors { get; init; } = string.Empty;

    /// <summary>
    /// Error message if build failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Duration of the build operation.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Creates a successful build validation result.
    /// </summary>
    public static BuildValidationResult Success(string output, TimeSpan duration)
    {
        return new BuildValidationResult
        {
            IsSuccess = true,
            Output = output,
            Duration = duration
        };
    }

    /// <summary>
    /// Creates a failed build validation result.
    /// </summary>
    public static BuildValidationResult Failure(
        string errorMessage,
        TimeSpan duration,
        string output = "",
        string errors = "")
    {
        return new BuildValidationResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            Output = output,
            Errors = errors,
            Duration = duration
        };
    }

    public override string ToString()
    {
        return IsSuccess
            ? $"Build succeeded ({Duration.TotalSeconds:F1}s)"
            : $"Build failed: {ErrorMessage} ({Duration.TotalSeconds:F1}s)";
    }
}
