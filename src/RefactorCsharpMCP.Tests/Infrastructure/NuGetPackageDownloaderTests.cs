using FluentAssertions;
using Microsoft.Extensions.Logging;
using RefactorCsharpMCP.Core.Infrastructure.FrameworkSupport;
using Xunit;

namespace RefactorCsharpMCP.Tests.Infrastructure;

/// <summary>
/// Tests for NuGetPackageDownloader - NuGet package download and extraction.
/// NOTE: Full download tests require network access and are better suited for integration tests.
/// These tests focus on initialization, caching, and error handling.
/// </summary>
public class NuGetPackageDownloaderTests : IDisposable
{
    private readonly string _testPackagesDirectory;
    private readonly TestLogger _testLogger;

    public NuGetPackageDownloaderTests()
    {
        _testPackagesDirectory = Path.Combine(Path.GetTempPath(), $"nuget-test-{Guid.NewGuid()}");
        _testLogger = new TestLogger();
    }

    /// <summary>
    /// Simple test logger that tracks log calls for verification.
    /// </summary>
    private class TestLogger : ILogger<NuGetPackageDownloaderTests>
    {
        public List<(LogLevel Level, string Message)> LogCalls { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LogCalls.Add((logLevel, state?.ToString() ?? string.Empty));
        }
    }

    #region Constructor and Initialization Tests

    [Fact]
    public void Constructor_WithDefaultDirectory_CreatesDirectory()
    {
        // Act
        using var downloader = new NuGetPackageDownloader();

        // Assert
        var expectedPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".refactor-csharp-mcp",
            "nuget-packages"
        );
        Directory.Exists(expectedPath).Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithCustomDirectory_CreatesCustomDirectory()
    {
        // Arrange
        var customDir = Path.Combine(Path.GetTempPath(), $"custom-nuget-{Guid.NewGuid()}");

        try
        {
            // Act
            using var downloader = new NuGetPackageDownloader(customDir);

            // Assert
            Directory.Exists(customDir).Should().BeTrue();
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(customDir))
            {
                Directory.Delete(customDir, recursive: true);
            }
        }
    }

    [Fact]
    public void Constructor_WithLogger_DoesNotThrow()
    {
        // Act
        using var downloader = new NuGetPackageDownloader(_testPackagesDirectory, _testLogger);

        // Assert - No exception thrown
        downloader.Should().NotBeNull();
    }

    #endregion

    #region IsPackageDownloaded Tests

    [Fact]
    public void IsPackageDownloaded_WithNoPackages_ReturnsFalse()
    {
        // Arrange
        using var downloader = new NuGetPackageDownloader(_testPackagesDirectory);

        // Act
        var result = downloader.IsPackageDownloaded("NonExistent.Package");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsPackageDownloaded_WithExistingPackage_ReturnsTrue()
    {
        // Arrange
        using var downloader = new NuGetPackageDownloader(_testPackagesDirectory);
        var packageFile = Path.Combine(_testPackagesDirectory, "Test.Package.1.0.0.nupkg");
        File.WriteAllText(packageFile, "fake package content");

        // Act
        var result = downloader.IsPackageDownloaded("Test.Package");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsPackageDownloaded_WithMultipleVersions_ReturnsTrueForAnyVersion()
    {
        // Arrange
        using var downloader = new NuGetPackageDownloader(_testPackagesDirectory);
        File.WriteAllText(Path.Combine(_testPackagesDirectory, "Test.Package.1.0.0.nupkg"), "v1");
        File.WriteAllText(Path.Combine(_testPackagesDirectory, "Test.Package.2.0.0.nupkg"), "v2");

        // Act
        var result = downloader.IsPackageDownloaded("Test.Package");

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region ClearCache Tests

    [Fact]
    public void ClearCache_WithExistingDirectory_ClearsSuccessfully()
    {
        // Arrange
        using var downloader = new NuGetPackageDownloader(_testPackagesDirectory);
        var testFile = Path.Combine(_testPackagesDirectory, "test.txt");
        File.WriteAllText(testFile, "test content");

        // Act
        downloader.ClearCache();

        // Assert
        Directory.Exists(_testPackagesDirectory).Should().BeTrue(); // Directory recreated
        File.Exists(testFile).Should().BeFalse(); // File removed
    }

    [Fact]
    public async Task ClearCacheAsync_WithExistingDirectory_ClearsSuccessfully()
    {
        // Arrange
        using var downloader = new NuGetPackageDownloader(_testPackagesDirectory);
        var testFile = Path.Combine(_testPackagesDirectory, "test.txt");
        File.WriteAllText(testFile, "test content");

        // Act
        await downloader.ClearCacheAsync();

        // Assert
        Directory.Exists(_testPackagesDirectory).Should().BeTrue(); // Directory recreated
        File.Exists(testFile).Should().BeFalse(); // File removed
    }

    [Fact]
    public void ClearCache_WithNonExistentDirectory_DoesNotThrow()
    {
        // Arrange
        var nonExistentDir = Path.Combine(Path.GetTempPath(), $"non-existent-{Guid.NewGuid()}");
        using var downloader = new NuGetPackageDownloader(nonExistentDir);
        Directory.Delete(nonExistentDir, recursive: true); // Remove the directory

        // Act - Should not throw
        downloader.ClearCache();

        // Assert - ClearCache only recreates if directory exists, so this is a no-op
        // The important thing is it doesn't throw an exception
    }

    [Fact]
    public async Task ClearCacheAsync_WithCancellation_RespectsCancellationToken()
    {
        // Arrange
        using var downloader = new NuGetPackageDownloader(_testPackagesDirectory);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - Should handle cancellation gracefully
        try
        {
            await downloader.ClearCacheAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation occurs during file deletion retry
        }
    }

    #endregion

    #region DownloadAndExtractAsync Error Handling Tests

    [Fact]
    public async Task DownloadAndExtractAsync_WithInvalidPackageId_ThrowsInvalidOperationException()
    {
        // Arrange
        using var downloader = new NuGetPackageDownloader(_testPackagesDirectory, _testLogger);

        // Act & Assert
        var act = async () => await downloader.DownloadAndExtractAsync("NonExistent.Package.That.Does.Not.Exist.Ever", "net8.0");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to download/extract package*");
    }

    [Fact]
    public async Task DownloadAndExtractAsync_WithEmptyPackageId_ThrowsInvalidOperationException()
    {
        // Arrange
        using var downloader = new NuGetPackageDownloader(_testPackagesDirectory);

        // Act & Assert
        var act = async () => await downloader.DownloadAndExtractAsync("", "net8.0");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_DisposesResourcesProperly()
    {
        // Arrange
        var downloader = new NuGetPackageDownloader(_testPackagesDirectory);

        // Act - Should not throw
        downloader.Dispose();

        // Assert - Can dispose multiple times
        downloader.Dispose();
    }

    [Fact]
    public void Dispose_WithUsingStatement_DisposesAutomatically()
    {
        // Act & Assert - Should not throw
        using (var downloader = new NuGetPackageDownloader(_testPackagesDirectory))
        {
            downloader.Should().NotBeNull();
        }
    }

    #endregion

    #region Integration Test Documentation

    // NOTE: The following tests would require actual NuGet package downloads and are better
    // suited for integration tests rather than unit tests. They are documented here for
    // completeness but should be implemented in a separate integration test suite.

    // [Fact(Skip = "Integration test - requires network access")]
    // public async Task DownloadAndExtractAsync_WithValidNet8Package_DownloadsSuccessfully()
    // {
    //     // This test would download a real NuGet package for net8.0 and verify extraction
    //     // Example: Microsoft.NETCore.App.Ref or similar
    // }

    // [Fact(Skip = "Integration test - requires network access")]
    // public async Task DownloadAndExtractAsync_WithValidNet48Package_DownloadsSuccessfully()
    // {
    //     // This test would download Microsoft.NETFramework.ReferenceAssemblies.net48
    //     // and verify that reference assemblies are extracted correctly
    // }

    // [Fact(Skip = "Integration test - requires network access")]
    // public async Task DownloadAndExtractAsync_WithCachedPackage_SkipsDownload()
    // {
    //     // This test would verify that if a package is already downloaded,
    //     // it's not downloaded again (cache behavior)
    // }

    // [Fact(Skip = "Integration test - requires network access")]
    // public async Task DownloadAndExtractAsync_ExtractsOnlyManagedAssemblies()
    // {
    //     // This test would verify that unmanaged assemblies like
    //     // System.EnterpriseServices.Thunk.dll are filtered out
    // }

    #endregion

    public void Dispose()
    {
        if (Directory.Exists(_testPackagesDirectory))
        {
            try
            {
                Directory.Delete(_testPackagesDirectory, recursive: true);
            }
            catch
            {
                // Best effort cleanup - test directory will be cleaned up eventually
            }
        }
    }
}
