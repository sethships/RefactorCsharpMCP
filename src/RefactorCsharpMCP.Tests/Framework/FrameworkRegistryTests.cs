using Microsoft.CodeAnalysis.CSharp;
using RefactorCsharpMCP.Core.Framework;
using Xunit;

namespace RefactorCsharpMCP.Tests.Framework;

/// <summary>
/// Unit tests for FrameworkRegistry covering static dictionaries and lookups.
/// </summary>
public class FrameworkRegistryTests
{
    [Fact]
    public void SupportedFrameworks_Contains11Frameworks()
    {
        // Assert
        Assert.Equal(11, FrameworkRegistry.SupportedFrameworks.Count);
    }

    [Theory]
    [InlineData("net9.0")]
    [InlineData("net8.0")]
    [InlineData("net481")]
    [InlineData("net48")]
    [InlineData("net472")]
    [InlineData("net471")]
    [InlineData("net47")]
    [InlineData("net462")]
    [InlineData("net35")]
    [InlineData("netstandard2.1")]
    [InlineData("netstandard2.0")]
    public void SupportedFrameworks_ContainsExpectedFrameworks(string tfm)
    {
        // Assert
        Assert.True(FrameworkRegistry.SupportedFrameworks.ContainsKey(tfm));
    }

    [Fact]
    public void EOLFrameworks_ContainsExpectedCount()
    {
        // Assert - Should have at least 13 EOL frameworks
        Assert.True(FrameworkRegistry.EOLFrameworks.Count >= 13);
    }

    [Theory]
    [InlineData("net6.0", "net8.0")]
    [InlineData("net7.0", "net8.0")]
    [InlineData("netcoreapp3.1", "net8.0")]
    [InlineData("net461", "net462")]
    public void EOLFrameworks_ContainsExpectedMappings(string eolTfm, string suggestedTfm)
    {
        // Assert
        Assert.True(FrameworkRegistry.EOLFrameworks.ContainsKey(eolTfm));
        Assert.Equal(suggestedTfm, FrameworkRegistry.EOLFrameworks[eolTfm].SuggestedTfm);
    }

    [Theory]
    [InlineData("v4.8", "net48")]
    [InlineData(".NETFramework,Version=v4.8.1", "net481")]
    [InlineData("framework48", "net48")]
    [InlineData("dotnet8.0", "net8.0")]
    public void TfmNormalizations_ContainsExpectedMappings(string input, string expected)
    {
        // Assert
        Assert.True(FrameworkRegistry.TfmNormalizations.ContainsKey(input));
        Assert.Equal(expected, FrameworkRegistry.TfmNormalizations[input]);
    }
}
