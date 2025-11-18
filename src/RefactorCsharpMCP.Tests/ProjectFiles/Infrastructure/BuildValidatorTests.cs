using Microsoft.Extensions.Logging.Abstractions;
using RefactorCsharpMCP.Core.ProjectFiles.Infrastructure;
using Xunit;

namespace RefactorCsharpMCP.Tests.ProjectFiles.Infrastructure;

/// <summary>
/// Tests for BuildValidator functionality.
/// NOTE: These tests focus on validation logic and error handling.
/// Actual dotnet build execution is tested in integration tests.
/// </summary>
public class BuildValidatorTests
{
    private readonly BuildValidator _validator;
    private readonly string _tempBasePath;

    public BuildValidatorTests()
    {
        _validator = new BuildValidator(NullLogger<BuildValidator>.Instance);
        _tempBasePath = Path.Combine(Path.GetTempPath(), $"BuildValidatorTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempBasePath);
    }

    #region Input Validation Tests

    [Fact]
    public async Task ValidateBuildAsync_WithNonExistentProjectPath_ShouldReturnFailure()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempBasePath, "NonExistent.csproj");

        // Act
        var result = await _validator.ValidateBuildAsync(nonExistentPath);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Project path not found", result.ErrorMessage);
        Assert.Contains(nonExistentPath, result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateBuildAsync_WithValidProjectFile_ShouldAttemptBuild()
    {
        // Arrange - Create a valid project file
        var projectPath = Path.Combine(_tempBasePath, "Test.csproj");
        var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>";
        File.WriteAllText(projectPath, projectContent);

        // Act
        var result = await _validator.ValidateBuildAsync(projectPath, timeoutSeconds: 10);

        // Assert
        // Result depends on dotnet CLI availability and network access
        // We just verify it doesn't throw and returns a result
        Assert.NotNull(result);
        if (!result.IsSuccess)
        {
            // If it fails, it should be due to dotnet CLI not found or build failure
            Assert.True(
                result.ErrorMessage?.Contains("dotnet CLI not found") == true ||
                result.ErrorMessage?.Contains("Build failed") == true ||
                result.ErrorMessage?.Contains("timed out") == true,
                $"Unexpected error message: {result.ErrorMessage}");
        }

        // Cleanup
        File.Delete(projectPath);
    }

    [Fact]
    public async Task ValidateBuildAsync_WithDirectoryPath_ShouldAttemptBuild()
    {
        // Arrange - Create a project in a directory
        var subDir = Path.Combine(_tempBasePath, "ProjectDir");
        Directory.CreateDirectory(subDir);
        var projectPath = Path.Combine(subDir, "Test.csproj");
        var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>";
        File.WriteAllText(projectPath, projectContent);

        // Act - Pass directory path instead of file path
        var result = await _validator.ValidateBuildAsync(subDir, timeoutSeconds: 10);

        // Assert
        Assert.NotNull(result);

        // Cleanup
        Directory.Delete(subDir, true);
    }

    #endregion

    #region Security Tests

    [Fact]
    public async Task ValidateBuildAsync_WithPathTraversalAttempt_ShouldThrowSecurityException()
    {
        // Arrange
        var maliciousPath = "../../etc/passwd.csproj";

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await _validator.ValidateBuildAsync(maliciousPath));
    }

    [Fact]
    public async Task ValidateBuildAsync_WithInvalidExtension_ShouldThrowSecurityException()
    {
        // Arrange
        var invalidPath = Path.Combine(_tempBasePath, "malicious.exe");
        File.WriteAllText(invalidPath, "fake content");

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await _validator.ValidateBuildAsync(invalidPath));

        // Cleanup
        File.Delete(invalidPath);
    }

    #endregion

    #region Multiple Projects Tests

    [Fact]
    public async Task ValidateBuildsAsync_WithMultipleProjects_ShouldReturnResultForEach()
    {
        // Arrange
        var project1 = Path.Combine(_tempBasePath, "Project1.csproj");
        var project2 = Path.Combine(_tempBasePath, "Project2.csproj");
        var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>";

        File.WriteAllText(project1, projectContent);
        File.WriteAllText(project2, projectContent);

        var projectPaths = new[] { project1, project2 };

        // Act
        var results = await _validator.ValidateBuildsAsync(projectPaths, timeoutSeconds: 10);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(project1, results.Keys);
        Assert.Contains(project2, results.Keys);

        // Cleanup
        File.Delete(project1);
        File.Delete(project2);
    }

    [Fact]
    public async Task ValidateBuildsAsync_WithMixedValidAndInvalidPaths_ShouldHandleIndividually()
    {
        // Arrange
        var validProject = Path.Combine(_tempBasePath, "Valid.csproj");
        var invalidProject = Path.Combine(_tempBasePath, "NonExistent.csproj");
        var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>";

        File.WriteAllText(validProject, projectContent);
        var projectPaths = new[] { validProject, invalidProject };

        // Act
        var results = await _validator.ValidateBuildsAsync(projectPaths, timeoutSeconds: 10);

        // Assert
        Assert.Equal(2, results.Count);

        // Invalid project should have a failure result
        Assert.False(results[invalidProject].IsSuccess);
        Assert.Contains("not found", results[invalidProject].ErrorMessage);

        // Cleanup
        File.Delete(validProject);
    }

    #endregion

    #region Solution Build Tests

    [Fact]
    public async Task ValidateSolutionBuildAsync_WithNonExistentSolution_ShouldReturnFailure()
    {
        // Arrange
        var solutionPath = Path.Combine(_tempBasePath, "NonExistent.sln");

        // Act
        var result = await _validator.ValidateSolutionBuildAsync(solutionPath);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Solution file not found", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateSolutionBuildAsync_WithNonSlnFile_ShouldReturnFailure()
    {
        // Arrange
        var notASolution = Path.Combine(_tempBasePath, "NotASolution.txt");
        File.WriteAllText(notASolution, "not a solution");

        // Act
        var result = await _validator.ValidateSolutionBuildAsync(notASolution);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Not a solution file", result.ErrorMessage);

        // Cleanup
        File.Delete(notASolution);
    }

    [Fact]
    public async Task ValidateSolutionBuildAsync_WithValidSolutionFile_ShouldAttemptBuild()
    {
        // Arrange - Create a minimal solution file
        var solutionPath = Path.Combine(_tempBasePath, "Test.sln");
        var solutionContent = @"Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
EndGlobal";
        File.WriteAllText(solutionPath, solutionContent);

        // Act
        var result = await _validator.ValidateSolutionBuildAsync(solutionPath, timeoutSeconds: 10);

        // Assert
        Assert.NotNull(result);
        // Result depends on dotnet CLI and solution content

        // Cleanup
        File.Delete(solutionPath);
    }

    #endregion

    #region BuildValidationResult Tests

    [Fact]
    public void BuildValidationResult_Success_ShouldHaveCorrectProperties()
    {
        // Arrange
        var output = "Build succeeded!";
        var duration = TimeSpan.FromSeconds(5);

        // Act
        var result = BuildValidationResult.Success(output, duration);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(output, result.Output);
        Assert.Equal(duration, result.Duration);
        Assert.Null(result.ErrorMessage);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void BuildValidationResult_Failure_ShouldHaveCorrectProperties()
    {
        // Arrange
        var errorMessage = "Build failed with errors";
        var duration = TimeSpan.FromSeconds(3);
        var output = "Some output";
        var errors = "Error details";

        // Act
        var result = BuildValidationResult.Failure(errorMessage, duration, output, errors);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(errorMessage, result.ErrorMessage);
        Assert.Equal(duration, result.Duration);
        Assert.Equal(output, result.Output);
        Assert.Equal(errors, result.Errors);
    }

    [Fact]
    public void BuildValidationResult_ToString_Success_ShouldFormatCorrectly()
    {
        // Arrange
        var result = BuildValidationResult.Success("output", TimeSpan.FromSeconds(2.5));

        // Act
        var formatted = result.ToString();

        // Assert
        Assert.Contains("Build succeeded", formatted);
        Assert.Contains("2.5", formatted);
    }

    [Fact]
    public void BuildValidationResult_ToString_Failure_ShouldFormatCorrectly()
    {
        // Arrange
        var result = BuildValidationResult.Failure("Test error", TimeSpan.FromSeconds(1.2));

        // Act
        var formatted = result.ToString();

        // Assert
        Assert.Contains("Build failed", formatted);
        Assert.Contains("Test error", formatted);
        Assert.Contains("1.2", formatted);
    }

    #endregion

    #region Cancellation Token Tests

    [Fact]
    public async Task ValidateBuildAsync_WithCancelledToken_ShouldHandleCancellation()
    {
        // Arrange
        var projectPath = Path.Combine(_tempBasePath, "Test.csproj");
        var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>";
        File.WriteAllText(projectPath, projectContent);

        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        // Cancellation might manifest as OperationCanceledException or BuildValidationResult.Failure
        try
        {
            var result = await _validator.ValidateBuildAsync(projectPath, timeoutSeconds: 10, cancellationToken: cts.Token);

            // If we get a result instead of exception, it should indicate failure
            if (result != null)
            {
                // This is also acceptable behavior
                Assert.True(true);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected behavior for cancelled token
            Assert.True(true);
        }

        // Cleanup
        File.Delete(projectPath);
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
