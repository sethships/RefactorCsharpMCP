using System.Security;

namespace RefactorCsharpMCP.Core.ProjectFiles.Infrastructure;

/// <summary>
/// Validates and normalizes file paths to prevent path traversal attacks.
/// </summary>
public static class PathValidator
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csproj",
        ".vbproj",
        ".fsproj",
        ".props",
        ".targets"
    };

    private static readonly HashSet<string> AllowedConfigFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "app.config",
        "web.config",
        "packages.config"
    };

    /// <summary>
    /// Validates and normalizes a project file path.
    /// </summary>
    /// <param name="path">The path to validate.</param>
    /// <param name="basePath">Optional base path to restrict access within. Defaults to current directory.</param>
    /// <returns>The normalized absolute path.</returns>
    /// <exception cref="ArgumentException">If the path is null, empty, or invalid.</exception>
    /// <exception cref="SecurityException">If the path attempts to escape the base directory or has an invalid extension.</exception>
    public static string ValidateAndNormalizePath(string path, string? basePath = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));
        }

        // Get the full path (resolves .., ., symlinks)
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            throw new ArgumentException($"Invalid path format: {path}", nameof(path), ex);
        }

        // Validate file extension
        var extension = Path.GetExtension(fullPath);
        var fileName = Path.GetFileName(fullPath);

        // Special handling for .config files - only allow specific safe config files
        if (extension.Equals(".config", StringComparison.OrdinalIgnoreCase))
        {
            if (!AllowedConfigFiles.Contains(fileName))
            {
                throw new SecurityException(
                    $"Config file '{fileName}' is not allowed. " +
                    $"Allowed config files: {string.Join(", ", AllowedConfigFiles)}");
            }
        }
        else if (!AllowedExtensions.Contains(extension))
        {
            throw new SecurityException(
                $"Invalid file extension '{extension}'. " +
                $"Allowed extensions: {string.Join(", ", AllowedExtensions)} and specific .config files");
        }

        // If base path is provided, ensure the full path is within it
        if (!string.IsNullOrWhiteSpace(basePath))
        {
            var normalizedBasePath = Path.GetFullPath(basePath);

            // Check if the full path starts with the base path
            // Use case-insensitive comparison on Windows, case-sensitive on Unix
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (!fullPath.StartsWith(normalizedBasePath, comparison))
            {
                throw new SecurityException(
                    $"Path '{path}' attempts to access files outside the allowed directory. " +
                    $"Resolved path: '{fullPath}', Base path: '{normalizedBasePath}'");
            }
        }

        return fullPath;
    }

    /// <summary>
    /// Validates a solution or directory path.
    /// </summary>
    /// <param name="path">The directory path to validate.</param>
    /// <param name="basePath">Optional base path to restrict access within.</param>
    /// <returns>The normalized absolute directory path.</returns>
    /// <exception cref="ArgumentException">If the path is null, empty, or invalid.</exception>
    /// <exception cref="SecurityException">If the path attempts to escape the base directory.</exception>
    public static string ValidateDirectoryPath(string path, string? basePath = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));
        }

        // Get the full path
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            throw new ArgumentException($"Invalid path format: {path}", nameof(path), ex);
        }

        // If base path is provided, ensure the full path is within it
        if (!string.IsNullOrWhiteSpace(basePath))
        {
            var normalizedBasePath = Path.GetFullPath(basePath);

            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (!fullPath.StartsWith(normalizedBasePath, comparison))
            {
                throw new SecurityException(
                    $"Path '{path}' attempts to access directories outside the allowed base path. " +
                    $"Resolved path: '{fullPath}', Base path: '{normalizedBasePath}'");
            }
        }

        return fullPath;
    }

    /// <summary>
    /// Safely combines a base path with a relative path, ensuring the result stays within the base path.
    /// </summary>
    /// <param name="basePath">The base directory path.</param>
    /// <param name="relativePath">The relative path to combine.</param>
    /// <returns>The combined and validated path.</returns>
    /// <exception cref="SecurityException">If the combined path escapes the base directory.</exception>
    public static string SafeCombine(string basePath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(basePath))
        {
            throw new ArgumentException("Base path cannot be null or whitespace.", nameof(basePath));
        }

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("Relative path cannot be null or whitespace.", nameof(relativePath));
        }

        var combined = Path.Combine(basePath, relativePath);
        return ValidateAndNormalizePath(combined, basePath);
    }
}
