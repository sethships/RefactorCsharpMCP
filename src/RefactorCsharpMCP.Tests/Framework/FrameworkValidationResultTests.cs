using Microsoft.CodeAnalysis.CSharp;
using RefactorCsharpMCP.Core.Framework;
using Xunit;

namespace RefactorCsharpMCP.Tests.Framework;

/// <summary>
/// Unit tests for FrameworkValidationResult covering result creation and error states.
/// </summary>
public class FrameworkValidationResultTests
{
    [Fact]
    public void Success_CreatesValidResult()
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
        var result = FrameworkValidationResult.Success(frameworkInfo);

        // Assert
        Assert.True(result.IsValid);
        Assert.True(result.IsSupported);
        Assert.False(result.IsEOL);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.ErrorMessage);
        Assert.NotNull(result.FrameworkInfo);
        Assert.Equal(frameworkInfo, result.FrameworkInfo);
    }

    [Fact]
    public void EOLError_CreatesEOLResult()
    {
        // Arrange
        var tfm = "net6.0";
        var suggestedFramework = "net8.0";
        var displayName = ".NET 6";
        var eolDate = new DateTime(2024, 11, 12);

        // Act
        var result = FrameworkValidationResult.EOLError(tfm, suggestedFramework, displayName, eolDate);

        // Assert
        Assert.True(result.IsValid);
        Assert.False(result.IsSupported);
        Assert.True(result.IsEOL);
        Assert.Equal(ErrorCode.EOL_FRAMEWORK, result.ErrorCode);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains(displayName, result.ErrorMessage);
        Assert.Equal(suggestedFramework, result.SuggestedFramework);
        Assert.NotNull(result.Workaround);
        Assert.NotNull(result.ErrorContext);
    }

    [Fact]
    public void InvalidFormatError_CreatesInvalidFormatResult()
    {
        // Arrange
        var tfm = "invalid";

        // Act
        var result = FrameworkValidationResult.InvalidFormatError(tfm);

        // Assert
        Assert.False(result.IsValid);
        Assert.False(result.IsSupported);
        Assert.False(result.IsEOL);
        Assert.Equal(ErrorCode.INVALID_TFM_FORMAT, result.ErrorCode);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains(tfm, result.ErrorMessage);
        Assert.NotNull(result.ErrorContext);
    }

    [Fact]
    public void MissingParameterError_CreatesMissingParameterResult()
    {
        // Act
        var result = FrameworkValidationResult.MissingParameterError();

        // Assert
        Assert.False(result.IsValid);
        Assert.False(result.IsSupported);
        Assert.False(result.IsEOL);
        Assert.Equal(ErrorCode.MISSING_PARAMETER, result.ErrorCode);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("targetFramework", result.ErrorMessage);
        Assert.NotNull(result.ErrorContext);
    }

    [Fact]
    public void UnknownFrameworkError_CreatesUnknownFrameworkResult()
    {
        // Arrange
        var tfm = "net10.0";
        var nearestMatch = "net9.0";

        // Act
        var result = FrameworkValidationResult.UnknownFrameworkError(tfm, nearestMatch);

        // Assert
        Assert.True(result.IsValid); // Format is valid
        Assert.False(result.IsSupported);
        Assert.False(result.IsEOL);
        Assert.Equal(ErrorCode.UNKNOWN_FRAMEWORK, result.ErrorCode);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains(tfm, result.ErrorMessage);
        Assert.Equal(nearestMatch, result.SuggestedFramework);
        Assert.NotNull(result.ErrorContext);
    }
}
