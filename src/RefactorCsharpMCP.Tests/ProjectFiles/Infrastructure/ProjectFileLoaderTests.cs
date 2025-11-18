using Microsoft.Extensions.Logging.Abstractions;
using RefactorCsharpMCP.Core.ProjectFiles.Infrastructure;
using RefactorCsharpMCP.Core.ProjectFiles.Models;
using System.Xml.Linq;
using Xunit;

namespace RefactorCsharpMCP.Tests.ProjectFiles.Infrastructure;

/// <summary>
/// Tests for ProjectFileLoader functionality.
/// Tests XML loading, project type detection, and framework parsing.
/// </summary>
public class ProjectFileLoaderTests : IDisposable
{
    private readonly ProjectFileLoader _loader;
    private readonly string _tempBasePath;
    private readonly string _fixturesPath;

    public ProjectFileLoaderTests()
    {
        _loader = new ProjectFileLoader(NullLogger<ProjectFileLoader>.Instance);
        _tempBasePath = Path.Combine(Path.GetTempPath(), $"ProjectLoaderTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempBasePath);

        // Path to test fixtures
        _fixturesPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "ProjectFiles",
            "TestFixtures",
            "SampleProjects");
    }

    #region LoadProject Tests

    [Fact]
    public void LoadProject_WithSdkStyleProject_ShouldLoadSuccessfully()
    {
        // Arrange
        var projectPath = Path.Combine(_tempBasePath, "SdkStyle.csproj");
        var sdkContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>";
        File.WriteAllText(projectPath, sdkContent);

        // Act
        var document = _loader.LoadProject(projectPath);

        // Assert
        Assert.NotNull(document);
        Assert.NotNull(document.Root);
        Assert.Equal("Project", document.Root.Name.LocalName);
        Assert.Equal("Microsoft.NET.Sdk", document.Root.Attribute("Sdk")?.Value);
    }

    [Fact]
    public void LoadProject_WithLegacyProject_ShouldLoadSuccessfully()
    {
        // Arrange
        var projectPath = Path.Combine(_tempBasePath, "Legacy.csproj");
        var legacyContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project ToolsVersion=""15.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <PropertyGroup>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
  </PropertyGroup>
</Project>";
        File.WriteAllText(projectPath, legacyContent);

        // Act
        var document = _loader.LoadProject(projectPath);

        // Assert
        Assert.NotNull(document);
        Assert.NotNull(document.Root);
        Assert.Equal("Project", document.Root.Name.LocalName);
    }

    [Fact]
    public void LoadProject_WithNonExistentFile_ShouldThrowException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempBasePath, "NonExistent.csproj");

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => _loader.LoadProject(nonExistentPath));
    }

    #endregion

    #region SaveProject Tests

    [Fact]
    public void SaveProject_ShouldWriteDocumentToFile()
    {
        // Arrange
        var projectPath = Path.Combine(_tempBasePath, "SaveTest.csproj");
        var document = XDocument.Parse(@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>");

        // Act
        _loader.SaveProject(document, projectPath);

        // Assert
        Assert.True(File.Exists(projectPath));
        var content = File.ReadAllText(projectPath);
        Assert.Contains("Microsoft.NET.Sdk", content);
        Assert.Contains("net8.0", content);
    }

    [Fact]
    public void SaveProject_WithPreserveFormatting_ShouldMaintainWhitespace()
    {
        // Arrange
        var projectPath = Path.Combine(_tempBasePath, "FormatTest.csproj");
        var document = XDocument.Parse(@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>");

        // Act
        _loader.SaveProject(document, projectPath, preserveFormatting: true);

        // Assert
        var content = File.ReadAllText(projectPath);
        Assert.Contains("  <PropertyGroup>", content); // Check indentation preserved
    }

    #endregion

    #region DetectProjectType Tests

    [Fact]
    public void DetectProjectType_WithSdkStyleProject_ShouldReturnSdkStyle()
    {
        // Arrange
        var document = XDocument.Parse(@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>");

        // Act
        var projectType = _loader.DetectProjectType(document);

        // Assert
        Assert.Equal(ProjectType.SdkStyle, projectType);
    }

    [Fact]
    public void DetectProjectType_WithLegacyProject_ShouldReturnLegacy()
    {
        // Arrange
        var document = XDocument.Parse(@"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <PropertyGroup>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
  </PropertyGroup>
</Project>");

        // Act
        var projectType = _loader.DetectProjectType(document);

        // Assert
        Assert.Equal(ProjectType.Legacy, projectType);
    }

    [Fact]
    public void DetectProjectType_WithAspNetWebApp_ShouldReturnAspNetWebApp()
    {
        // Arrange
        var document = XDocument.Parse(@"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <PropertyGroup>
    <ProjectTypeGuids>{349c5851-65df-11da-9384-00065b846f21};{fae04ec0-301f-11d3-bf4b-00c04f79efbc}</ProjectTypeGuids>
  </PropertyGroup>
</Project>");

        // Act
        var projectType = _loader.DetectProjectType(document);

        // Assert
        Assert.Equal(ProjectType.AspNetWebApp, projectType);
    }

    #endregion

    #region GetTargetFrameworks Tests

    [Fact]
    public void GetTargetFrameworks_WithSingleFramework_ShouldReturnOne()
    {
        // Arrange
        var document = XDocument.Parse(@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>");

        // Act
        var frameworks = _loader.GetTargetFrameworks(document);

        // Assert
        Assert.Single(frameworks);
        Assert.Contains("net8.0", frameworks);
    }

    [Fact]
    public void GetTargetFrameworks_WithMultipleFrameworks_ShouldReturnAll()
    {
        // Arrange
        var document = XDocument.Parse(@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFrameworks>net8.0;net48</TargetFrameworks>
  </PropertyGroup>
</Project>");

        // Act
        var frameworks = _loader.GetTargetFrameworks(document);

        // Assert
        Assert.Equal(2, frameworks.Count);
        Assert.Contains("net8.0", frameworks);
        Assert.Contains("net48", frameworks);
    }

    [Fact]
    public void GetTargetFrameworks_WithLegacyFrameworkVersion_ShouldConvertToMoniker()
    {
        // Arrange
        var document = XDocument.Parse(@"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <PropertyGroup>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
  </PropertyGroup>
</Project>");

        // Act
        var frameworks = _loader.GetTargetFrameworks(document);

        // Assert
        Assert.Single(frameworks);
        Assert.Contains("net48", frameworks);
    }

    #endregion

    #region GetPackageReferences Tests

    [Fact]
    public void GetPackageReferences_WithPackages_ShouldReturnAll()
    {
        // Arrange
        var document = XDocument.Parse(@"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Newtonsoft.Json"" Version=""13.0.3"" />
    <PackageReference Include=""Serilog"" Version=""3.1.1"" />
  </ItemGroup>
</Project>");

        // Act
        var packages = _loader.GetPackageReferences(document);

        // Assert
        Assert.Equal(2, packages.Count);
        Assert.Contains(packages, p => p.PackageId == "Newtonsoft.Json" && p.Version == "13.0.3");
        Assert.Contains(packages, p => p.PackageId == "Serilog" && p.Version == "3.1.1");
    }

    [Fact]
    public void GetPackageReferences_WithNoPackages_ShouldReturnEmpty()
    {
        // Arrange
        var document = XDocument.Parse(@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>");

        // Act
        var packages = _loader.GetPackageReferences(document);

        // Assert
        Assert.Empty(packages);
    }

    #endregion

    #region LoadProjectContext Tests

    [Fact]
    public void LoadProjectContext_ShouldPopulateAllProperties()
    {
        // Arrange
        var projectPath = Path.Combine(_tempBasePath, "ContextTest.csproj");
        var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <AssemblyName>TestAssembly</AssemblyName>
    <RootNamespace>TestNamespace</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Newtonsoft.Json"" Version=""13.0.3"" />
  </ItemGroup>
</Project>";
        File.WriteAllText(projectPath, projectContent);

        // Act
        var context = _loader.LoadProjectContext(projectPath);

        // Assert
        Assert.Equal(projectPath, context.FilePath);
        Assert.Equal(ProjectType.SdkStyle, context.ProjectType);
        Assert.Contains("net8.0", context.TargetFrameworks);
        Assert.Equal("TestAssembly", context.AssemblyName);
        Assert.Equal("TestNamespace", context.RootNamespace);
        Assert.Single(context.PackageReferences);
        Assert.False(context.IsMultiTargeting);
    }

    [Fact]
    public void LoadProjectContext_WithMultiTargeting_ShouldSetIsMultiTargetingTrue()
    {
        // Arrange
        var projectPath = Path.Combine(_tempBasePath, "MultiTarget.csproj");
        var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFrameworks>net8.0;net48</TargetFrameworks>
  </PropertyGroup>
</Project>";
        File.WriteAllText(projectPath, projectContent);

        // Act
        var context = _loader.LoadProjectContext(projectPath);

        // Assert
        Assert.True(context.IsMultiTargeting);
        Assert.Equal(2, context.TargetFrameworks.Count);
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
