using Microsoft.CodeAnalysis.CSharp;
using RefactorCsharpMCP.Core.Framework;
using Xunit;

namespace RefactorCsharpMCP.Tests.Framework;

/// <summary>
/// Unit tests for LanguageVersionMapper covering TFM to language version mapping.
/// </summary>
public class LanguageVersionMapperTests
{
    private readonly LanguageVersionMapper _mapper = new();

    #region GetLanguageVersion Tests

    [Theory]
    [InlineData("net9.0", LanguageVersion.CSharp13)]
    [InlineData("net8.0", LanguageVersion.CSharp12)]
    [InlineData("net481", LanguageVersion.CSharp7_3)]
    [InlineData("net48", LanguageVersion.CSharp7_3)]
    [InlineData("net462", LanguageVersion.CSharp7_3)]
    [InlineData("netstandard2.1", LanguageVersion.CSharp8)]
    [InlineData("netstandard2.0", LanguageVersion.CSharp7_3)]
    public void GetLanguageVersion_WithSupportedFramework_ReturnsCorrectVersion(string tfm, LanguageVersion expected)
    {
        // Act
        var result = _mapper.GetLanguageVersion(tfm);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData("net6.0")] // EOL framework
    [InlineData("net10.0")] // Unknown framework
    [InlineData("invalid")] // Invalid format
    public void GetLanguageVersion_WithUnsupportedFramework_ReturnsNull(string tfm)
    {
        // Act
        var result = _mapper.GetLanguageVersion(tfm);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetLanguageVersion_WithNullOrWhitespace_ReturnsNull(string? tfm)
    {
        // Act
        var result = _mapper.GetLanguageVersion(tfm);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetLanguageVersion_WithFrameworkInfo_ReturnsLanguageVersion()
    {
        // Arrange
        var frameworkInfo = FrameworkInfo.Builder()
            .WithTfm("net8.0")
            .WithDisplayName(".NET 8")
            .WithLanguageVersion(LanguageVersion.CSharp12)
            .WithFamily(FrameworkFamily.Modern)
            .WithSupportStatus("Supported")
            .Build();

        // Act
        var result = _mapper.GetLanguageVersion(frameworkInfo);

        // Assert
        Assert.Equal(LanguageVersion.CSharp12, result);
    }

    [Fact]
    public void GetLanguageVersion_WithNullFrameworkInfo_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _mapper.GetLanguageVersion((FrameworkInfo)null!));
    }

    #endregion

    #region TryGetLanguageVersion Tests

    [Fact]
    public void TryGetLanguageVersion_WithSupportedFramework_ReturnsTrueAndVersion()
    {
        // Act
        var result = _mapper.TryGetLanguageVersion("net8.0", out var languageVersion);

        // Assert
        Assert.True(result);
        Assert.Equal(LanguageVersion.CSharp12, languageVersion);
    }

    [Fact]
    public void TryGetLanguageVersion_WithUnsupportedFramework_ReturnsFalseAndDefault()
    {
        // Act
        var result = _mapper.TryGetLanguageVersion("net6.0", out var languageVersion);

        // Assert
        Assert.False(result);
        Assert.Equal(LanguageVersion.Default, languageVersion);
    }

    #endregion

    #region GetLanguageVersionOrDefault Tests

    [Fact]
    public void GetLanguageVersionOrDefault_WithSupportedFramework_ReturnsVersion()
    {
        // Act
        var result = _mapper.GetLanguageVersionOrDefault("net8.0");

        // Assert
        Assert.Equal(LanguageVersion.CSharp12, result);
    }

    [Fact]
    public void GetLanguageVersionOrDefault_WithUnsupportedFramework_ReturnsDefaultCSharp12()
    {
        // Act
        var result = _mapper.GetLanguageVersionOrDefault("net6.0");

        // Assert
        Assert.Equal(LanguageVersion.CSharp12, result);
    }

    [Fact]
    public void GetLanguageVersionOrDefault_WithCustomFallback_ReturnsFallback()
    {
        // Act
        var result = _mapper.GetLanguageVersionOrDefault("invalid", LanguageVersion.CSharp10);

        // Assert
        Assert.Equal(LanguageVersion.CSharp10, result);
    }

    #endregion
}
