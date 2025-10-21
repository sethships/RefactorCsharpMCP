using RefactorCsharpMCP.Core.Infrastructure.FrameworkSupport;
using Microsoft.CodeAnalysis.CSharp;

namespace RefactorCsharpMCP.Tests.Infrastructure;

public class FrameworkMonikerTests
{
    [Fact]
    public void SupportedFrameworks_Contains_Exactly11Frameworks()
    {
        // Arrange & Act
        var frameworks = FrameworkMoniker.SupportedFrameworks;

        // Assert - 11 total: 2 modern .NET + 7 .NET Framework + 2 .NET Standard
        Assert.Equal(11, frameworks.Count);
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
    public void IsSupported_Returns_TrueForSupportedFrameworks(string framework)
    {
        // Act
        var result = FrameworkMoniker.IsSupported(framework);

        // Assert
        Assert.True(result, $"Framework {framework} should be supported");
    }

    [Theory]
    [InlineData("net7.0")]
    [InlineData("net6.0")]
    [InlineData("net5.0")]
    [InlineData("netcoreapp3.1")]
    [InlineData("net461")]
    [InlineData("net45")]
    public void IsEndOfLife_Returns_TrueForEOLFrameworks(string framework)
    {
        // Act
        var result = FrameworkMoniker.IsEndOfLife(framework);

        // Assert
        Assert.True(result, $"Framework {framework} should be marked as EOL");
    }

    [Theory]
    [InlineData("net9.0", LanguageVersion.CSharp13)]
    [InlineData("net8.0", LanguageVersion.CSharp12)]
    [InlineData("net481", LanguageVersion.CSharp7_3)]
    [InlineData("net48", LanguageVersion.CSharp7_3)]
    [InlineData("net472", LanguageVersion.CSharp7_3)]
    [InlineData("net471", LanguageVersion.CSharp7_3)]
    [InlineData("net47", LanguageVersion.CSharp7_3)]
    [InlineData("net462", LanguageVersion.CSharp7_3)]
    [InlineData("net35", LanguageVersion.CSharp3)]
    [InlineData("netstandard2.1", LanguageVersion.CSharp8)]
    [InlineData("netstandard2.0", LanguageVersion.CSharp7_3)]
    public void GetLanguageVersion_Returns_CorrectVersionForFramework(string framework, LanguageVersion expected)
    {
        // Act
        var result = FrameworkMoniker.GetLanguageVersion(framework);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetLanguageVersion_ThrowsArgumentException_ForUnsupportedFramework()
    {
        // Arrange
        var unsupportedFramework = "net7.0";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            FrameworkMoniker.GetLanguageVersion(unsupportedFramework));

        Assert.Contains("Unsupported framework", exception.Message);
    }

    [Theory]
    [InlineData("net481", "Microsoft.NETFramework.ReferenceAssemblies.net481")]
    [InlineData("net48", "Microsoft.NETFramework.ReferenceAssemblies.net48")]
    [InlineData("net472", "Microsoft.NETFramework.ReferenceAssemblies.net472")]
    [InlineData("net471", "Microsoft.NETFramework.ReferenceAssemblies.net471")]
    [InlineData("net47", "Microsoft.NETFramework.ReferenceAssemblies.net47")]
    [InlineData("net462", "Microsoft.NETFramework.ReferenceAssemblies.net462")]
    [InlineData("net35", "Microsoft.NETFramework.ReferenceAssemblies.net35")]
    public void GetNuGetPackageName_Returns_CorrectPackageForFramework(string framework, string expectedPackage)
    {
        // Act
        var result = FrameworkMoniker.GetNuGetPackageName(framework);

        // Assert
        Assert.Equal(expectedPackage, result);
    }

    [Theory]
    [InlineData("net9.0")]
    [InlineData("net8.0")]
    [InlineData("netstandard2.1")]
    [InlineData("netstandard2.0")]
    public void GetNuGetPackageName_Returns_NullForModernFrameworks(string framework)
    {
        // Act
        var result = FrameworkMoniker.GetNuGetPackageName(framework);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("net481", true)]
    [InlineData("net48", true)]
    [InlineData("net35", true)]
    [InlineData("net9.0", false)]
    [InlineData("net8.0", false)]
    [InlineData("netstandard2.1", false)]
    public void RequiresNuGetPackage_Returns_CorrectValueForFramework(string framework, bool expected)
    {
        // Act
        var result = FrameworkMoniker.RequiresNuGetPackage(framework);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("net9.0", ".NET 9")]
    [InlineData("net8.0", ".NET 8")]
    [InlineData("net481", ".NET Framework 4.8.1")]
    [InlineData("net48", ".NET Framework 4.8")]
    [InlineData("net35", ".NET Framework 3.5 SP1")]
    [InlineData("netstandard2.1", ".NET Standard 2.1")]
    [InlineData("netstandard2.0", ".NET Standard 2.0")]
    public void GetFriendlyName_Returns_CorrectNameForFramework(string framework, string expectedName)
    {
        // Act
        var result = FrameworkMoniker.GetFriendlyName(framework);

        // Assert
        Assert.Equal(expectedName, result);
    }

    [Theory]
    [InlineData("net7.0", "net8.0")]
    [InlineData("net6.0", "net8.0")]
    [InlineData("net5.0", "net8.0")]
    [InlineData("netcoreapp3.1", "net8.0")]
    [InlineData("net461", "net462")]
    [InlineData("net45", "net462")]
    public void SuggestAlternative_Returns_CorrectAlternativeForEOLFramework(string eolFramework, string expectedAlternative)
    {
        // Act
        var result = FrameworkMoniker.SuggestAlternative(eolFramework);

        // Assert
        Assert.Equal(expectedAlternative, result);
    }

    [Theory]
    [InlineData("net4.8.1", "net481")]
    [InlineData("net4.8", "net48")]
    [InlineData("net4.7.2", "net472")]
    [InlineData("net4.7.1", "net471")]
    [InlineData("net4.7", "net47")]
    [InlineData("net4.6.2", "net462")]
    [InlineData("net3.5", "net35")]
    [InlineData("NET9.0", "net9.0")]
    [InlineData("  net8.0  ", "net8.0")]
    public void Normalize_Returns_CorrectNormalizedFramework(string input, string expected)
    {
        // Act
        var result = FrameworkMoniker.Normalize(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SupportedFrameworks_IsCaseInsensitive()
    {
        // Arrange
        var frameworks = new[] { "NET9.0", "Net8.0", "NETSTANDARD2.1" };

        // Act & Assert
        foreach (var framework in frameworks)
        {
            Assert.True(FrameworkMoniker.IsSupported(framework),
                $"Framework {framework} should be recognized (case-insensitive)");
        }
    }

    [Fact]
    public void EolFrameworks_ContainsExpectedFrameworks()
    {
        // Arrange
        var expectedEolFrameworks = new[]
        {
            "net7.0", "net6.0", "net5.0",
            "netcoreapp3.1", "netcoreapp3.0", "netcoreapp2.2", "netcoreapp2.1", "netcoreapp2.0",
            "net461", "net46", "net452", "net451", "net45"
        };

        // Act & Assert
        foreach (var framework in expectedEolFrameworks)
        {
            Assert.True(FrameworkMoniker.IsEndOfLife(framework),
                $"Framework {framework} should be marked as EOL");
        }
    }

    [Fact]
    public void Normalize_ThrowsArgumentException_ForNullInput()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => FrameworkMoniker.Normalize(null!));
    }

    [Fact]
    public void Normalize_ThrowsArgumentException_ForEmptyInput()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => FrameworkMoniker.Normalize(string.Empty));
    }

    [Fact]
    public void Normalize_ThrowsArgumentException_ForWhitespaceInput()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => FrameworkMoniker.Normalize("   "));
    }

    [Fact]
    public void Normalize_ThrowsArgumentException_ForInputWithSpaces()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => FrameworkMoniker.Normalize("net 8.0"));
        Assert.Contains("cannot contain spaces", exception.Message);
    }

    [Fact]
    public void Normalize_DoesNotNormalize_InvalidDottedVersions()
    {
        // Arrange - "net4.81" is not a valid framework (should be "net4.8.1" or "net481")
        var input = "net4.81";

        // Act
        var result = FrameworkMoniker.Normalize(input);

        // Assert - Should not be normalized to "net481" since it's not a valid pattern
        Assert.Equal("net4.81", result);
    }
}
