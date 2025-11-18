using System.Security;
using RefactorCsharpMCP.Core.ProjectFiles.Infrastructure;
using Xunit;

namespace RefactorCsharpMCP.Tests.ProjectFiles.Infrastructure;

/// <summary>
/// Tests for PathValidator security and path normalization functionality.
/// CRITICAL: These tests validate path traversal attack prevention.
/// </summary>
public class PathValidatorTests : IDisposable
{
    private readonly string _tempBasePath;

    public PathValidatorTests()
    {
        // Create a temporary directory for testing
        _tempBasePath = Path.Combine(Path.GetTempPath(), $"PathValidatorTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempBasePath);
    }

    #region ValidateAndNormalizePath Tests

    [Fact]
    public void ValidateAndNormalizePath_WithValidCsprojPath_ShouldSucceed()
    {
        // Arrange
        var projectPath = Path.Combine(_tempBasePath, "Test.csproj");
        File.WriteAllText(projectPath, "<Project />");

        // Act
        var result = PathValidator.ValidateAndNormalizePath(projectPath, _tempBasePath);

        // Assert
        Assert.Equal(Path.GetFullPath(projectPath), result);
        Assert.StartsWith(_tempBasePath, result);

        // Cleanup
        File.Delete(projectPath);
    }

    [Theory]
    [InlineData("../../etc/passwd.csproj")] // Path traversal with .csproj
    [InlineData("../../../sensitive.csproj")] // Multiple levels up
    [InlineData("./../malicious.csproj")] // Hidden traversal
    public void ValidateAndNormalizePath_WithPathTraversalAttempt_ShouldThrowSecurityException(string maliciousPath)
    {
        // Arrange
        var combined = Path.Combine(_tempBasePath, maliciousPath);

        // Act & Assert
        var exception = Assert.Throws<SecurityException>(() =>
            PathValidator.ValidateAndNormalizePath(combined, _tempBasePath));

        Assert.Contains("outside the allowed directory", exception.Message);
    }

    [Theory]
    [InlineData("Test.txt")] // Text file
    [InlineData("Test.exe")] // Executable
    [InlineData("Test.dll")] // Library
    [InlineData("malicious.bat")] // Batch file
    [InlineData("config.json")] // JSON config (not in whitelist)
    public void ValidateAndNormalizePath_WithInvalidExtension_ShouldThrowSecurityException(string fileName)
    {
        // Arrange
        var filePath = Path.Combine(_tempBasePath, fileName);

        // Act & Assert
        var exception = Assert.Throws<SecurityException>(() =>
            PathValidator.ValidateAndNormalizePath(filePath, _tempBasePath));

        Assert.Contains("Invalid file extension", exception.Message);
    }

    [Theory]
    [InlineData("app.config")] // Allowed config file
    [InlineData("web.config")] // Allowed config file
    [InlineData("packages.config")] // Allowed config file
    public void ValidateAndNormalizePath_WithAllowedConfigFile_ShouldSucceed(string fileName)
    {
        // Arrange
        var filePath = Path.Combine(_tempBasePath, fileName);

        // Act
        var result = PathValidator.ValidateAndNormalizePath(filePath, _tempBasePath);

        // Assert
        Assert.Equal(Path.GetFullPath(filePath), result);
    }

    [Theory]
    [InlineData("malicious.config")] // Not in allowed config list
    [InlineData("connectionstrings.config")] // Not in allowed config list
    [InlineData("appsettings.config")] // Not in allowed config list
    public void ValidateAndNormalizePath_WithDisallowedConfigFile_ShouldThrowSecurityException(string fileName)
    {
        // Arrange
        var filePath = Path.Combine(_tempBasePath, fileName);

        // Act & Assert
        var exception = Assert.Throws<SecurityException>(() =>
            PathValidator.ValidateAndNormalizePath(filePath, _tempBasePath));

        Assert.Contains("is not allowed", exception.Message);
        Assert.Contains("Allowed config files", exception.Message);
    }

    [Theory]
    [InlineData("Test.csproj")] // C# project
    [InlineData("Test.vbproj")] // VB project
    [InlineData("Test.fsproj")] // F# project
    [InlineData("Directory.Build.props")] // MSBuild props
    [InlineData("Directory.Packages.props")] // MSBuild props
    [InlineData("Common.targets")] // MSBuild targets
    public void ValidateAndNormalizePath_WithAllowedExtensions_ShouldSucceed(string fileName)
    {
        // Arrange
        var filePath = Path.Combine(_tempBasePath, fileName);

        // Act
        var result = PathValidator.ValidateAndNormalizePath(filePath, _tempBasePath);

        // Assert
        Assert.Equal(Path.GetFullPath(filePath), result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateAndNormalizePath_WithNullOrEmptyPath_ShouldThrowArgumentException(string? invalidPath)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            PathValidator.ValidateAndNormalizePath(invalidPath!));

        Assert.Contains("Path cannot be null or whitespace", exception.Message);
    }

    [Fact]
    public void ValidateAndNormalizePath_WithAbsolutePathOutsideBase_ShouldThrowSecurityException()
    {
        // Arrange
        var outsidePath = Path.Combine(Path.GetTempPath(), "outside.csproj");

        // Act & Assert
        var exception = Assert.Throws<SecurityException>(() =>
            PathValidator.ValidateAndNormalizePath(outsidePath, _tempBasePath));

        Assert.Contains("outside the allowed directory", exception.Message);
    }

    #endregion

    #region ValidateDirectoryPath Tests

    [Fact]
    public void ValidateDirectoryPath_WithValidDirectory_ShouldSucceed()
    {
        // Arrange
        var subDir = Path.Combine(_tempBasePath, "SubDirectory");
        Directory.CreateDirectory(subDir);

        // Act
        var result = PathValidator.ValidateDirectoryPath(subDir, _tempBasePath);

        // Assert
        Assert.Equal(Path.GetFullPath(subDir), result);
        Assert.StartsWith(_tempBasePath, result);

        // Cleanup
        Directory.Delete(subDir);
    }

    [Theory]
    [InlineData("../../etc")]
    [InlineData("../../../sensitive")]
    [InlineData("./../malicious")]
    public void ValidateDirectoryPath_WithPathTraversalAttempt_ShouldThrowSecurityException(string maliciousPath)
    {
        // Arrange
        var combined = Path.Combine(_tempBasePath, maliciousPath);

        // Act & Assert
        var exception = Assert.Throws<SecurityException>(() =>
            PathValidator.ValidateDirectoryPath(combined, _tempBasePath));

        Assert.Contains("outside the allowed base path", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateDirectoryPath_WithNullOrEmptyPath_ShouldThrowArgumentException(string? invalidPath)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            PathValidator.ValidateDirectoryPath(invalidPath!));

        Assert.Contains("Path cannot be null or whitespace", exception.Message);
    }

    [Fact]
    public void ValidateDirectoryPath_WithAbsolutePathOutsideBase_ShouldThrowSecurityException()
    {
        // Arrange
        var outsidePath = Path.Combine(Path.GetTempPath(), "outside_directory");

        // Act & Assert
        var exception = Assert.Throws<SecurityException>(() =>
            PathValidator.ValidateDirectoryPath(outsidePath, _tempBasePath));

        Assert.Contains("outside the allowed base path", exception.Message);
    }

    #endregion

    #region SafeCombine Tests

    [Fact]
    public void SafeCombine_WithValidRelativePath_ShouldSucceed()
    {
        // Arrange
        var relativePath = "Subdirectory/Test.csproj";

        // Act
        var result = PathValidator.SafeCombine(_tempBasePath, relativePath);

        // Assert
        var expected = Path.GetFullPath(Path.Combine(_tempBasePath, relativePath));
        Assert.Equal(expected, result);
        Assert.StartsWith(_tempBasePath, result);
    }

    [Theory]
    [InlineData("../../etc/passwd.csproj")]
    [InlineData("../../../malicious.csproj")]
    [InlineData("./../escape.csproj")]
    public void SafeCombine_WithPathTraversalAttempt_ShouldThrowSecurityException(string maliciousRelative)
    {
        // Act & Assert
        var exception = Assert.Throws<SecurityException>(() =>
            PathValidator.SafeCombine(_tempBasePath, maliciousRelative));

        Assert.Contains("outside the allowed directory", exception.Message);
    }

    [Theory]
    [InlineData(null, "relative.csproj")]
    [InlineData("", "relative.csproj")]
    [InlineData("   ", "relative.csproj")]
    public void SafeCombine_WithNullOrEmptyBasePath_ShouldThrowArgumentException(string? invalidBase, string relative)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            PathValidator.SafeCombine(invalidBase!, relative));

        Assert.Contains("Base path cannot be null or whitespace", exception.Message);
    }

    [Theory]
    [InlineData("/valid/base", null)]
    [InlineData("/valid/base", "")]
    [InlineData("/valid/base", "   ")]
    public void SafeCombine_WithNullOrEmptyRelativePath_ShouldThrowArgumentException(string basePath, string? invalidRelative)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            PathValidator.SafeCombine(basePath, invalidRelative!));

        Assert.Contains("Relative path cannot be null or whitespace", exception.Message);
    }

    #endregion

    #region Edge Cases and Platform-Specific Tests

    [Fact]
    public void ValidateAndNormalizePath_WithMixedSlashes_ShouldNormalizeCorrectly()
    {
        // Arrange
        var mixedPath = _tempBasePath.Replace(Path.DirectorySeparatorChar, '/') + "/Test.csproj";

        // Act
        var result = PathValidator.ValidateAndNormalizePath(mixedPath, _tempBasePath);

        // Assert
        Assert.Equal(Path.GetFullPath(mixedPath), result);
        Assert.StartsWith(_tempBasePath, result);
    }

    [Fact]
    public void ValidateAndNormalizePath_WithDotSegments_ShouldResolveCorrectly()
    {
        // Arrange
        var subDir = Path.Combine(_tempBasePath, "SubDir");
        Directory.CreateDirectory(subDir);
        var pathWithDots = Path.Combine(subDir, "./Test.csproj");

        // Act
        var result = PathValidator.ValidateAndNormalizePath(pathWithDots, _tempBasePath);

        // Assert
        Assert.Equal(Path.GetFullPath(Path.Combine(subDir, "Test.csproj")), result);
        Assert.DoesNotContain(".", Path.GetFileName(result));

        // Cleanup
        Directory.Delete(subDir);
    }

    [Fact]
    public void ValidateAndNormalizePath_WithCaseVariationsOnWindows_ShouldHandleCorrectly()
    {
        // This test is more relevant on Windows, but should work cross-platform
        // Arrange
        var lowerPath = Path.Combine(_tempBasePath.ToLowerInvariant(), "test.csproj");
        var upperBase = _tempBasePath.ToUpperInvariant();

        // Act - should not throw on Windows due to case-insensitive comparison
        if (OperatingSystem.IsWindows())
        {
            var result = PathValidator.ValidateAndNormalizePath(lowerPath, upperBase);
            Assert.NotNull(result);
        }
        else
        {
            // On Linux/macOS, this might throw due to case-sensitive paths
            // Just verify it doesn't crash
            try
            {
                PathValidator.ValidateAndNormalizePath(lowerPath, upperBase);
            }
            catch (SecurityException)
            {
                // Expected on case-sensitive file systems
            }
        }
    }

    #endregion

    public void Dispose()
    {
        // Cleanup temporary directory
        if (Directory.Exists(_tempBasePath))
        {
            try
            {
                Directory.Delete(_tempBasePath, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
}
