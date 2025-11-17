using System.Diagnostics;
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

    public BuildValidator(ILogger<BuildValidator>? logger = null)
    {
        _logger = logger ?? NullLogger<BuildValidator>.Instance;
    }

    /// <summary>
    /// Validates that a project builds successfully.
    /// </summary>
    /// <param name="projectPath">Path to the project file or directory containing the project.</param>
    /// <param name="timeoutSeconds">Timeout for the build operation in seconds.</param>
    /// <returns>Build validation result.</returns>
    public async Task<BuildValidationResult> ValidateBuildAsync(
        string projectPath,
        int timeoutSeconds = 300)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Determine if path is a file or directory
            var targetPath = File.Exists(projectPath) ? projectPath : projectPath;
            if (!File.Exists(projectPath) && !Directory.Exists(projectPath))
            {
                return BuildValidationResult.Failure(
                    $"Project path not found: {projectPath}",
                    TimeSpan.Zero);
            }

            _logger.LogInformation("Starting build validation for: {ProjectPath}", projectPath);

            var (exitCode, output, errors) = await RunDotnetBuildAsync(targetPath, timeoutSeconds);

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
    /// <returns>Dictionary of project path to build validation result.</returns>
    public async Task<Dictionary<string, BuildValidationResult>> ValidateBuildsAsync(
        IEnumerable<string> projectPaths,
        int timeoutSeconds = 300)
    {
        var results = new Dictionary<string, BuildValidationResult>();

        foreach (var projectPath in projectPaths)
        {
            var result = await ValidateBuildAsync(projectPath, timeoutSeconds);
            results[projectPath] = result;
        }

        return results;
    }

    /// <summary>
    /// Validates that a solution builds successfully.
    /// </summary>
    /// <param name="solutionPath">Path to the solution file (.sln).</param>
    /// <param name="timeoutSeconds">Timeout for the build operation in seconds.</param>
    /// <returns>Build validation result.</returns>
    public async Task<BuildValidationResult> ValidateSolutionBuildAsync(
        string solutionPath,
        int timeoutSeconds = 600)
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

        return await ValidateBuildAsync(solutionPath, timeoutSeconds);
    }

    /// <summary>
    /// Runs dotnet build command and captures output.
    /// </summary>
    private async Task<(int exitCode, string output, string errors)> RunDotnetBuildAsync(
        string targetPath,
        int timeoutSeconds)
    {
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{targetPath}\" --no-restore --nologo",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

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

        var completed = await process.WaitForExitAsync(TimeSpan.FromSeconds(timeoutSeconds));

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
    public static async Task<bool> WaitForExitAsync(this Process process, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);

        try
        {
            await process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
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
