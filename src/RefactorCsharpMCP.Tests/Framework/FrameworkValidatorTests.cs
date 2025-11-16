using RefactorCsharpMCP.Core.Framework;
using Xunit;

namespace RefactorCsharpMCP.Tests.Framework;

/// <summary>
/// Unit tests for FrameworkValidator covering TFM validation, normalization, and EOL detection.
/// </summary>
public class FrameworkValidatorTests
{
    private readonly FrameworkValidator _validator = new();

    #region Validate Tests

    [Theory]
    [InlineData("net8.0")]
    [InlineData("net9.0")]
    [InlineData("net48")]
    [InlineData("net481")]
    [InlineData("netstandard2.0")]
    public void Validate_WithSupportedFramework_ReturnsSuccess(string tfm)
    {
        // Act
        var result = _validator.Validate(tfm);

        // Assert
        Assert.True(result.IsValid);
        Assert.True(result.IsSupported);
        Assert.False(result.IsEOL);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.ErrorMessage);
        Assert.NotNull(result.FrameworkInfo);
    }

    [Theory]
    [InlineData("net6.0")]
    [InlineData("net7.0")]
    [InlineData("netcoreapp3.1")]
    [InlineData("net461")]
    public void Validate_WithEOLFramework_ReturnsEOLError(string tfm)
    {
        // Act
        var result = _validator.Validate(tfm);

        // Assert
        Assert.True(result.IsValid);
        Assert.False(result.IsSupported);
        Assert.True(result.IsEOL);
        Assert.Equal(ErrorCode.EOL_FRAMEWORK, result.ErrorCode);
        Assert.NotNull(result.ErrorMessage);
        Assert.NotNull(result.SuggestedFramework);
        Assert.NotNull(result.Workaround);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithNullOrEmpty_ReturnsMissingParameterError(string? tfm)
    {
        // Act
        var result = _validator.Validate(tfm);

        // Assert
        Assert.False(result.IsValid);
        Assert.False(result.IsSupported);
        Assert.False(result.IsEOL);
        Assert.Equal(ErrorCode.MISSING_PARAMETER, result.ErrorCode);
        Assert.NotNull(result.ErrorMessage);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("net")]
    [InlineData("framework4.8")]
    [InlineData("dotnet")]
    public void Validate_WithInvalidFormat_ReturnsInvalidFormatError(string tfm)
    {
        // Act
        var result = _validator.Validate(tfm);

        // Assert
        Assert.False(result.IsValid);
        Assert.False(result.IsSupported);
        Assert.False(result.IsEOL);
        Assert.Equal(ErrorCode.INVALID_TFM_FORMAT, result.ErrorCode);
        Assert.NotNull(result.ErrorMessage);
    }

    [Theory]
    [InlineData("net10.0")]
    [InlineData("netstandard3.0")]
    [InlineData("netcoreapp4.0")]
    public void Validate_WithUnknownFramework_ReturnsUnknownFrameworkError(string tfm)
    {
        // Act
        var result = _validator.Validate(tfm);

        // Assert
        Assert.True(result.IsValid); // Format is valid
        Assert.False(result.IsSupported);
        Assert.False(result.IsEOL);
        Assert.Equal(ErrorCode.UNKNOWN_FRAMEWORK, result.ErrorCode);
        Assert.NotNull(result.ErrorMessage);
    }

    [Theory]
    [InlineData("v4.8", "net48")]
    [InlineData(".NETFramework,Version=v4.8.1", "net481")]
    [InlineData("framework48", "net48")]
    [InlineData("dotnet8.0", "net8.0")]
    public void Validate_WithAlternativeFormat_NormalizesAndValidates(string inputTfm, string expectedNormalizedTfm)
    {
        // Act
        var result = _validator.Validate(inputTfm);

        // Assert
        Assert.True(result.IsValid);
        Assert.True(result.IsSupported);
        Assert.NotNull(result.FrameworkInfo);
        Assert.Equal(expectedNormalizedTfm, result.FrameworkInfo.Tfm);
    }

    #endregion

    #region IsSupported Tests

    [Theory]
    [InlineData("net8.0", true)]
    [InlineData("net48", true)]
    [InlineData("net6.0", false)]
    [InlineData("invalid", false)]
    [InlineData(null, false)]
    public void IsSupported_ReturnsCorrectResult(string? tfm, bool expected)
    {
        // Act
        var result = _validator.IsSupported(tfm);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region IsEOL Tests

    [Theory]
    [InlineData("net6.0", true)]
    [InlineData("net7.0", true)]
    [InlineData("net8.0", false)]
    [InlineData("invalid", false)]
    [InlineData(null, false)]
    public void IsEOL_ReturnsCorrectResult(string? tfm, bool expected)
    {
        // Act
        var result = _validator.IsEOL(tfm);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region NormalizeTfm Tests

    [Theory]
    [InlineData("v4.8", "net48")]
    [InlineData(".NETFramework,Version=v4.8.1", "net481")]
    [InlineData("framework48", "net48")]
    [InlineData("dotnet8.0", "net8.0")]
    [InlineData("net8.0", "net8.0")] // Already normalized
    public void NormalizeTfm_WithAlternativeFormats_ReturnsStandardFormat(string input, string expected)
    {
        // Act
        var result = _validator.NormalizeTfm(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeTfm_WithNullOrWhitespace_ReturnsInput(string? input)
    {
        // Act
        var result = _validator.NormalizeTfm(input!);

        // Assert
        Assert.Equal(input, result);
    }

    [Fact]
    public void NormalizeTfm_WithUnknownFormat_ReturnsInputTrimmed()
    {
        // Arrange
        var input = "  unknown_format  ";

        // Act
        var result = _validator.NormalizeTfm(input);

        // Assert
        Assert.Equal("unknown_format", result);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Validate_WithCaseInsensitiveTfm_Succeeds()
    {
        // Act
        var result = _validator.Validate("NET8.0");

        // Assert
        Assert.True(result.IsValid);
        Assert.True(result.IsSupported);
        Assert.NotNull(result.FrameworkInfo);
    }

    [Fact]
    public void Validate_WithTrimmedTfm_Succeeds()
    {
        // Act
        var result = _validator.Validate("  net8.0  ");

        // Assert
        Assert.True(result.IsValid);
        Assert.True(result.IsSupported);
        Assert.NotNull(result.FrameworkInfo);
    }

    [Fact]
    public void Validate_PopulatesErrorContext_ForEOLFramework()
    {
        // Act
        var result = _validator.Validate("net6.0");

        // Assert
        Assert.NotNull(result.ErrorContext);
        Assert.True(result.ErrorContext.ContainsKey("requested"));
        Assert.True(result.ErrorContext.ContainsKey("isEOL"));
        Assert.True(result.ErrorContext.ContainsKey("eolDate"));
    }

    #endregion
}
