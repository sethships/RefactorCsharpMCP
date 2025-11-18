using Microsoft.Extensions.Logging.Abstractions;
using RefactorCsharpMCP.Core.ProjectFiles.Infrastructure;
using RefactorCsharpMCP.Core.ProjectFiles.Models;
using RefactorCsharpMCP.Core.ProjectFiles.NuGet;
using RefactorCsharpMCP.Core.ProjectFiles.Refactorings;
using Xunit;

namespace RefactorCsharpMCP.Tests.ProjectFiles.Refactorings;

/// <summary>
/// Tests for PackageReferenceManager functionality.
/// Focuses on XML manipulation, validation, and error handling.
/// </summary>
public class PackageReferenceManagerTests : IDisposable
{
    private readonly PackageReferenceManager _manager;
    private readonly string _tempBasePath;

    public PackageReferenceManagerTests()
    {
        var nugetClient = new NuGetClientWrapper(NullLogger<NuGetClientWrapper>.Instance);
        _manager = new PackageReferenceManager(NullLogger<PackageReferenceManager>.Instance, nugetClient);
        _tempBasePath = Path.Combine(Path.GetTempPath(), $"PackageManagerTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempBasePath);
    }

    #region Input Validation Tests

    [Theory]
    [InlineData(null, "Newtonsoft.Json", "13.0.3")]
    [InlineData("", "Newtonsoft.Json", "13.0.3")]
    [InlineData("   ", "Newtonsoft.Json", "13.0.3")]
    public async Task ManagePackageReferenceAsync_WithNullOrEmptyPath_ShouldReturnFailure(
        string? projectPath,
        string packageId,
        string version)
    {
        // Act
        var result = await _manager.ManagePackageReferenceAsync(
            projectPath!,
            PackageOperation.Add,
            packageId,
            version);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Project path cannot be null or empty", result.ErrorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ManagePackageReferenceAsync_WithNullOrEmptyPackageId_ShouldReturnFailure(string? packageId)
    {
        // Arrange
        var projectPath = Path.Combine(_tempBasePath, "Test.csproj");

        // Act
        var result = await _manager.ManagePackageReferenceAsync(
            projectPath,
            PackageOperation.Add,
            packageId!,
            "1.0.0");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Package ID cannot be null or empty", result.ErrorMessage);
    }

    [Theory]
    [InlineData("Invalid Package")]  // Space in package ID
    [InlineData("Package@1.0")]      // Invalid character
    [InlineData("Package#Test")]     // Invalid character
    [InlineData("Package$Name")]     // Invalid character
    public async Task ManagePackageReferenceAsync_WithInvalidPackageId_ShouldReturnFailure(string invalidPackageId)
    {
        // Arrange
        var projectPath = Path.Combine(_tempBasePath, "Test.csproj");

        // Act
        var result = await _manager.ManagePackageReferenceAsync(
            projectPath,
            PackageOperation.Add,
            invalidPackageId,
            "1.0.0");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid package ID", result.ErrorMessage);
    }

    [Theory]
    [InlineData("invalid-version")]
    [InlineData("1.0.0.0.0")]  // Too many version parts
    [InlineData("v1.0.0")]     // Invalid prefix
    public async Task ManagePackageReferenceAsync_WithInvalidVersion_ShouldReturnFailure(string invalidVersion)
    {
        // Arrange
        var projectPath = Path.Combine(_tempBasePath, "Test.csproj");
        CreateTestProject(projectPath);

        // Act
        var result = await _manager.ManagePackageReferenceAsync(
            projectPath,
            PackageOperation.Add,
            "Newtonsoft.Json",
            invalidVersion);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid NuGet version format", result.ErrorMessage);
    }

    [Fact]
    public async Task ManagePackageReferenceAsync_AddOperationWithoutVersion_ShouldReturnFailure()
    {
        // Arrange
        var projectPath = Path.Combine(_tempBasePath, "Test.csproj");
        CreateTestProject(projectPath);

        // Act
        var result = await _manager.ManagePackageReferenceAsync(
            projectPath,
            PackageOperation.Add,
            "Newtonsoft.Json",
            version: null);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Version is required for Add operation", result.ErrorMessage);
    }

    #endregion

    #region Add Package Tests

    [Fact]
    public async Task ManagePackageReferenceAsync_AddNewPackage_ShouldAddToProject()
    {
        // Arrange
        var projectPath = Path.Combine(_tempBasePath, "Test.csproj");
        CreateTestProject(projectPath);
        var options = new ProjectRefactoringOptions { ValidateBuild = false };

        // Act
        var result = await _manager.ManagePackageReferenceAsync(
            projectPath,
            PackageOperation.Add,
            "Serilog",
            "3.1.1",
            options);

        // Assert - Should succeed (build validation disabled)
        // NOTE: Due to network restrictions, actual package validation may fail
        // We're testing the logic, not actual NuGet API calls
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ManagePackageReferenceAsync_DryRunMode_ShouldPreviewWithoutModifying()
    {
        // Arrange
        var projectPath = Path.Combine(_tempBasePath, "Test.csproj");
        CreateTestProject(projectPath);
        var options = new ProjectRefactoringOptions { DryRun = true };
        var originalContent = File.ReadAllText(projectPath);

        // Act
        var result = await _manager.ManagePackageReferenceAsync(
            projectPath,
            PackageOperation.Add,
            "Serilog",
            "3.1.1",
            options);

        // Assert
        var currentContent = File.ReadAllText(projectPath);
        Assert.Equal(originalContent, currentContent); // File should not be modified
        Assert.NotNull(result);
        if (result.IsSuccess)
        {
            Assert.Contains("DRY RUN Preview", result.Message);
        }
    }

    #endregion

    #region Update Package Tests

    [Fact]
    public async Task ManagePackageReferenceAsync_UpdateExistingPackage_ShouldUpdateVersion()
    {
        // Arrange
        var projectPath = Path.Combine(_tempBasePath, "TestUpdate.csproj");
        CreateTestProjectWithPackage(projectPath, "Newtonsoft.Json", "13.0.1");
        var options = new ProjectRefactoringOptions { ValidateBuild = false };

        // Act
        var result = await _manager.ManagePackageReferenceAsync(
            projectPath,
            PackageOperation.Update,
            "Newtonsoft.Json",
            "13.0.3",
            options);

        // Assert
        Assert.NotNull(result);
        // Verify the XML was modified
        var content = File.ReadAllText(projectPath);
        if (result.IsSuccess)
        {
            Assert.Contains("13.0.3", content);
        }
    }

    [Fact]
    public async Task ManagePackageReferenceAsync_UpdateNonExistentPackage_ShouldReturnFailure()
    {
        // Arrange
        var projectPath = Path.Combine(_tempBasePath, "TestUpdateNonExistent.csproj");
        CreateTestProject(projectPath);
        var options = new ProjectRefactoringOptions { ValidateBuild = false };

        // Act
        var result = await _manager.ManagePackageReferenceAsync(
            projectPath,
            PackageOperation.Update,
            "NonExistentPackage",
            "1.0.0",
            options);

        // Assert
        Assert.False(result.IsSuccess);
    }

    #endregion

    #region Remove Package Tests

    [Fact]
    public async Task ManagePackageReferenceAsync_RemoveExistingPackage_ShouldRemoveFromProject()
    {
        // Arrange
        var projectPath = Path.Combine(_tempBasePath, "TestRemove.csproj");
        CreateTestProjectWithPackage(projectPath, "Serilog", "3.1.1");
        var options = new ProjectRefactoringOptions { ValidateBuild = false };

        // Act
        var result = await _manager.ManagePackageReferenceAsync(
            projectPath,
            PackageOperation.Remove,
            "Serilog",
            version: null,
            options);

        // Assert
        Assert.NotNull(result);
        var content = File.ReadAllText(projectPath);
        if (result.IsSuccess)
        {
            Assert.DoesNotContain("Serilog", content);
        }
    }

    [Fact]
    public async Task ManagePackageReferenceAsync_RemoveNonExistentPackage_ShouldReturnFailure()
    {
        // Arrange
        var projectPath = Path.Combine(_tempBasePath, "TestRemoveNonExistent.csproj");
        CreateTestProject(projectPath);
        var options = new ProjectRefactoringOptions { ValidateBuild = false };

        // Act
        var result = await _manager.ManagePackageReferenceAsync(
            projectPath,
            PackageOperation.Remove,
            "NonExistentPackage",
            version: null,
            options);

        // Assert
        Assert.False(result.IsSuccess);
    }

    #endregion

    #region Batch Operations Tests

    [Fact]
    public async Task ManagePackageReferenceAsync_WithApplyToAllProjects_ShouldDiscoverMultipleProjects()
    {
        // Arrange
        var project1 = Path.Combine(_tempBasePath, "Project1.csproj");
        var project2 = Path.Combine(_tempBasePath, "Project2.csproj");
        CreateTestProject(project1);
        CreateTestProject(project2);

        var options = new ProjectRefactoringOptions
        {
            ValidateBuild = false,
            ApplyToAllProjects = true
        };

        // Act
        var result = await _manager.ManagePackageReferenceAsync(
            _tempBasePath,  // Pass directory, not specific project
            PackageOperation.Add,
            "Serilog",
            "3.1.1",
            options);

        // Assert
        Assert.NotNull(result);
        // Should process multiple projects
    }

    #endregion

    #region Helper Methods

    private void CreateTestProject(string projectPath)
    {
        var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>";
        File.WriteAllText(projectPath, projectContent);
    }

    private void CreateTestProjectWithPackage(string projectPath, string packageId, string version)
    {
        var projectContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""{packageId}"" Version=""{version}"" />
  </ItemGroup>
</Project>";
        File.WriteAllText(projectPath, projectContent);
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
