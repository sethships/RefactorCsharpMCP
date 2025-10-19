using FluentAssertions;
using RefactorCsharpMCP.Tests.TestData;

namespace RefactorCsharpMCP.Tests.Infrastructure;

/// <summary>
/// Example tests demonstrating the use of FrameworkTestFixture and FrameworkMatrixAttribute.
/// These tests showcase how to write framework-aware tests that run across all supported frameworks.
/// </summary>
[Collection("CacheTests")]
public class FrameworkTestFixtureExampleTests : FrameworkTestFixture
{
    [Theory]
    [FrameworkMatrix]
    public async Task SimpleClass_Compiles_ForAllFrameworks(string targetFramework)
    {
        // Arrange
        var sourceCode = FrameworkSourceBuilder.CreateSimpleClass(targetFramework);

        // Act
        var compilation = await CreateTestCompilationAsync(targetFramework, sourceCode);

        // Assert
        CompilationValidator.AssertNoErrors(compilation, $"Simple class for {targetFramework}");
    }

    [Theory]
    [FrameworkMatrix(Filter = FrameworkFamily.Modern)]
    public async Task ModernFrameworks_Support_CollectionExpressions(string targetFramework)
    {
        // Arrange - Modern frameworks support C# 12 features
        var sourceCode = @"using System.Collections.Generic;

public class ModernFeatures
{
    public List<int> GetNumbers()
    {
        int[] arr = [1, 2, 3];
        return [.. arr];
    }
}";

        // Act
        var isValid = await ValidatesSuccessfullyAsync(targetFramework, sourceCode);

        // Assert
        isValid.Should().BeTrue($"{targetFramework} should support collection expressions");
    }

    [Theory]
    [FrameworkMatrix(Filter = FrameworkFamily.Framework)]
    public async Task DotNetFramework_Compiles_TraditionalSyntax(string targetFramework)
    {
        // Arrange - .NET Framework uses traditional C# syntax
        var sourceCode = FrameworkSourceBuilder.CreateClassWithFields(targetFramework);

        // Act
        var compilation = await CreateTestCompilationAsync(targetFramework, sourceCode);

        // Assert
        CompilationValidator.AssertNoErrors(compilation, $".NET Framework {targetFramework}");
    }

    [Theory]
    [FrameworkMatrix]
    public void LanguageVersion_MapsCorrectly_ForAllFrameworks(string targetFramework)
    {
        // Act
        var langVersion = GetLanguageVersion(targetFramework);
        var friendlyName = GetFriendlyFrameworkName(targetFramework);

        // Assert
        langVersion.Should().BeDefined();
        friendlyName.Should().NotBeNullOrEmpty();

        // Verify expected mappings
        var normalized = targetFramework.ToLowerInvariant();
        if (normalized == "net8.0")
        {
            langVersion.Should().Be(Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp12);
        }
        else if (normalized == "net48")
        {
            langVersion.Should().Be(Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp7_3);
        }
        else if (normalized == "net35")
        {
            langVersion.Should().Be(Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp3);
        }
    }

    [Theory]
    [FrameworkMatrix]
    public void FeatureDetection_WorksCorrectly(string targetFramework)
    {
        // Act & Assert - Check feature support
        var hasNullable = SupportsFeature(targetFramework, FrameworkFeature.NullableTypes);
        var hasTuples = SupportsFeature(targetFramework, FrameworkFeature.Tuples);
        var hasCollectionExpressions = SupportsFeature(targetFramework, FrameworkFeature.CollectionExpressions);

        // Verify known frameworks
        var normalized = targetFramework.ToLowerInvariant();
        if (normalized == "net8.0" || normalized == "net9.0")
        {
            hasNullable.Should().BeTrue("modern .NET supports nullable types");
            hasTuples.Should().BeTrue("modern .NET supports tuples");
            hasCollectionExpressions.Should().BeTrue("modern .NET supports collection expressions");
        }
        else if (normalized == "net35")
        {
            hasNullable.Should().BeFalse(".NET Framework 3.5 does not support nullable types");
            hasTuples.Should().BeFalse(".NET Framework 3.5 does not support tuples");
            hasCollectionExpressions.Should().BeFalse(".NET Framework 3.5 does not support collection expressions");
        }
    }
}
