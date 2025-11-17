using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RefactorCsharpMCP.Core.ProjectFiles.Infrastructure;

/// <summary>
/// Manages backup and rollback operations for project files.
/// Creates backup copies before modifications and supports rollback on failure.
/// </summary>
public class ProjectFileBackup
{
    private readonly ILogger<ProjectFileBackup> _logger;
    private readonly Dictionary<string, string> _backups = new();

    public ProjectFileBackup(ILogger<ProjectFileBackup>? logger = null)
    {
        _logger = logger ?? NullLogger<ProjectFileBackup>.Instance;
    }

    /// <summary>
    /// Creates a backup of the specified file.
    /// Returns the path to the backup file.
    /// </summary>
    /// <param name="filePath">Path to the file to backup.</param>
    /// <returns>Path to the backup file.</returns>
    /// <exception cref="FileNotFoundException">If the source file doesn't exist.</exception>
    /// <exception cref="IOException">If backup creation fails.</exception>
    public string CreateBackup(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Cannot create backup: File not found: {filePath}");
        }

        var backupPath = GenerateBackupPath(filePath);

        try
        {
            File.Copy(filePath, backupPath, overwrite: true);
            _backups[filePath] = backupPath;

            _logger.LogDebug("Created backup: {BackupPath} for {FilePath}", backupPath, filePath);

            return backupPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create backup for {FilePath}", filePath);
            throw new IOException($"Failed to create backup for {filePath}", ex);
        }
    }

    /// <summary>
    /// Creates backups for multiple files.
    /// Returns a dictionary mapping original paths to backup paths.
    /// </summary>
    /// <param name="filePaths">Paths to files to backup.</param>
    /// <returns>Dictionary of original path to backup path.</returns>
    public Dictionary<string, string> CreateBackups(IEnumerable<string> filePaths)
    {
        var backupMap = new Dictionary<string, string>();
        var createdBackups = new List<string>();

        try
        {
            foreach (var filePath in filePaths)
            {
                var backupPath = CreateBackup(filePath);
                backupMap[filePath] = backupPath;
                createdBackups.Add(backupPath);
            }

            return backupMap;
        }
        catch
        {
            // If any backup fails, clean up all created backups
            foreach (var backupPath in createdBackups)
            {
                try
                {
                    if (File.Exists(backupPath))
                    {
                        File.Delete(backupPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up backup file: {BackupPath}", backupPath);
                }
            }

            throw;
        }
    }

    /// <summary>
    /// Restores a file from its backup.
    /// </summary>
    /// <param name="filePath">Path to the original file.</param>
    /// <exception cref="InvalidOperationException">If no backup exists for the file.</exception>
    public void Restore(string filePath)
    {
        if (!_backups.TryGetValue(filePath, out var backupPath))
        {
            throw new InvalidOperationException($"No backup found for {filePath}");
        }

        if (!File.Exists(backupPath))
        {
            throw new FileNotFoundException($"Backup file not found: {backupPath}");
        }

        try
        {
            File.Copy(backupPath, filePath, overwrite: true);
            _logger.LogInformation("Restored {FilePath} from backup {BackupPath}", filePath, backupPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore {FilePath} from backup", filePath);
            throw new IOException($"Failed to restore {filePath} from backup", ex);
        }
    }

    /// <summary>
    /// Restores multiple files from their backups.
    /// </summary>
    /// <param name="filePaths">Paths to files to restore.</param>
    public void RestoreAll(IEnumerable<string> filePaths)
    {
        var errors = new List<Exception>();

        foreach (var filePath in filePaths)
        {
            try
            {
                Restore(filePath);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
                _logger.LogError(ex, "Failed to restore {FilePath}", filePath);
            }
        }

        if (errors.Any())
        {
            throw new AggregateException("Failed to restore one or more files", errors);
        }
    }

    /// <summary>
    /// Deletes a backup file.
    /// </summary>
    /// <param name="filePath">Path to the original file.</param>
    /// <param name="keepBackup">If true, keeps the backup file on disk but removes it from tracking.</param>
    public void DeleteBackup(string filePath, bool keepBackup = false)
    {
        if (!_backups.TryGetValue(filePath, out var backupPath))
        {
            return;
        }

        if (!keepBackup && File.Exists(backupPath))
        {
            try
            {
                File.Delete(backupPath);
                _logger.LogDebug("Deleted backup: {BackupPath}", backupPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete backup file: {BackupPath}", backupPath);
            }
        }

        _backups.Remove(filePath);
    }

    /// <summary>
    /// Deletes all tracked backup files.
    /// </summary>
    /// <param name="keepBackups">If true, keeps the backup files on disk but removes them from tracking.</param>
    public void DeleteAllBackups(bool keepBackups = false)
    {
        var filePaths = _backups.Keys.ToList();

        foreach (var filePath in filePaths)
        {
            DeleteBackup(filePath, keepBackups);
        }
    }

    /// <summary>
    /// Gets the backup path for a file, if it exists.
    /// </summary>
    /// <param name="filePath">Path to the original file.</param>
    /// <returns>Path to the backup file, or null if no backup exists.</returns>
    public string? GetBackupPath(string filePath)
    {
        return _backups.TryGetValue(filePath, out var backupPath) ? backupPath : null;
    }

    /// <summary>
    /// Gets all tracked backups.
    /// </summary>
    /// <returns>Dictionary mapping original file paths to backup paths.</returns>
    public IReadOnlyDictionary<string, string> GetAllBackups()
    {
        return _backups;
    }

    /// <summary>
    /// Generates a backup file path with timestamp.
    /// Format: {originalPath}.backup.{timestamp}
    /// </summary>
    private static string GenerateBackupPath(string filePath)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        return $"{filePath}.backup.{timestamp}";
    }
}
