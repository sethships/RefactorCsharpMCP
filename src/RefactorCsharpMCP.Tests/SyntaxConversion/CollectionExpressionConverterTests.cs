using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using RefactorCsharpMCP.Core.SyntaxConversion;
using Xunit;

namespace RefactorCsharpMCP.Tests.SyntaxConversion;

/// <summary>
/// Tests for CollectionExpressionConverter - architectural demonstration.
///
/// CollectionExpressionSyntax API is available in Roslyn 4.14.0, but full implementation
/// is intentionally deferred pending real-world migration scenarios requiring collection
/// expression downgrading to C# 11 or lower syntax.
///
/// Current tests verify:
/// - Converter properties (name, language version requirements)
/// - Framework compatibility detection (CanConvert)
/// - Placeholder behavior (returns code unchanged)
///
/// See docs/FUTURE-ROADMAP.md for implementation timeline and rationale.
/// </summary>
public class CollectionExpressionConverterTests
{
    private readonly CollectionExpressionConverter _converter;

    public CollectionExpressionConverterTests()
    {
        _converter = new CollectionExpressionConverter();
    }

    [Fact]
    public void ConverterProperties_AreCorrect()
    {
        // Assert
        _converter.Name.Should().Be("CollectionExpressionConverter");
        _converter.MinimumSourceLanguageVersion.Should().Be(LanguageVersion.CSharp12);
        _converter.MaximumTargetLanguageVersion.Should().Be(LanguageVersion.CSharp11);
    }

    [Theory]
    [InlineData("net48")]  // C# 7.3
    [InlineData("net35")]  // C# 3.0
    [InlineData("netstandard2.0")]  // C# 7.3
    public void CanConvert_ReturnsTrueForOlderFrameworks(string targetFramework)
    {
        // Arrange
        var sourceCode = "class Test { }";
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();

        // Act
        var canConvert = _converter.CanConvert(root, targetFramework);

        // Assert
        canConvert.Should().BeTrue($"framework {targetFramework} should require conversion");
    }

    [Theory]
    [InlineData("net8.0")]  // C# 12
    [InlineData("net9.0")]  // C# 13
    public void CanConvert_ReturnsFalseForModernFrameworks(string targetFramework)
    {
        // Arrange
        var sourceCode = "class Test { }";
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();

        // Act
        var canConvert = _converter.CanConvert(root, targetFramework);

        // Assert
        canConvert.Should().BeFalse($"framework {targetFramework} supports collection expressions natively");
    }

    [Fact]
    public void Convert_PlaceholderImplementation_ReturnsUnchanged()
    {
        // Arrange
        var sourceCode = @"
class Test
{
    public void Method()
    {
        var numbers = new[] { 1, 2, 3 };
    }
}";

        // Act
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var converted = _converter.Convert(root, "net48");
        var convertedCode = converted.ToFullString();

        // Assert - placeholder implementation doesn't modify code
        convertedCode.Should().Contain("new[] { 1, 2, 3 }");
        converted.Should().Be(root); // Should return original until full implementation
    }
}
