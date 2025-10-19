using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using RefactorCsharpMCP.Core.SyntaxConversion;
using RefactorCsharpMCP.Tests.Infrastructure;
using Xunit;

namespace RefactorCsharpMCP.Tests.SyntaxConversion;

/// <summary>
/// Tests for TupleReturnConverter that converts C# 7.0 tuple returns to out parameters.
/// </summary>
[Collection("CacheTests")]
public class TupleReturnConverterTests : FrameworkTestFixture
{
    private readonly TupleReturnConverter _converter;

    public TupleReturnConverterTests()
    {
        _converter = new TupleReturnConverter();
    }

    [Fact]
    public void ConverterProperties_AreCorrect()
    {
        // Assert
        _converter.Name.Should().Be("TupleReturnConverter");
        _converter.MinimumSourceLanguageVersion.Should().Be(LanguageVersion.CSharp7);
        _converter.MaximumTargetLanguageVersion.Should().Be(LanguageVersion.CSharp6);
    }

    [Theory]
    [InlineData("net35")]  // C# 3.0
    public void CanConvert_ReturnsTrueForOlderFrameworks(string targetFramework)
    {
        // Arrange
        var sourceCode = "class Test { }";
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();

        // Act
        var canConvert = _converter.CanConvert(root, targetFramework);

        // Assert
        canConvert.Should().BeTrue($"framework {targetFramework} should require tuple conversion");
    }

    [Theory]
    [InlineData("net8.0")]  // C# 12
    [InlineData("net48")]  // C# 7.3
    [InlineData("netstandard2.0")]  // C# 7.3
    public void CanConvert_ReturnsFalseForModernFrameworks(string targetFramework)
    {
        // Arrange
        var sourceCode = "class Test { }";
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();

        // Act
        var canConvert = _converter.CanConvert(root, targetFramework);

        // Assert
        canConvert.Should().BeFalse($"framework {targetFramework} supports tuples");
    }

    [Fact]
    public void Convert_SimpleTupleReturn_ConvertsToOutParameters()
    {
        // Arrange
        var sourceCode = @"
class Test
{
    public (int, string) GetData()
    {
        return (42, ""test"");
    }
}";

        // Act
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var converted = _converter.Convert(root, "net35");
        var convertedCode = converted.ToFullString();

        // Assert
        convertedCode.Should().Contain("public void GetData(out int item1, out string item2)");
        convertedCode.Should().Contain("item1 = 42");
        convertedCode.Should().Contain("item2 = \"test\"");
        convertedCode.Should().Contain("return;");
        convertedCode.Should().NotContain("(int, string)");
    }

    [Fact]
    public void Convert_NamedTupleReturn_PreservesNames()
    {
        // Arrange
        var sourceCode = @"
class Test
{
    public (int count, string message) GetData()
    {
        return (42, ""test"");
    }
}";

        // Act
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var converted = _converter.Convert(root, "net35");
        var convertedCode = converted.ToFullString();

        // Assert
        convertedCode.Should().Contain("public void GetData(out int count, out string message)");
        convertedCode.Should().Contain("count = 42");
        convertedCode.Should().Contain("message = \"test\"");
    }

    [Fact]
    public void Convert_TupleWithExistingParameters_AppendsOutParameters()
    {
        // Arrange
        var sourceCode = @"
class Test
{
    public (int, string) GetData(int id)
    {
        return (id, ""test"");
    }
}";

        // Act
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var converted = _converter.Convert(root, "net35");
        var convertedCode = converted.ToFullString();

        // Assert
        convertedCode.Should().Contain("public void GetData(int id, out int item1, out string item2)");
        convertedCode.Should().Contain("item1 = id");
        convertedCode.Should().Contain("item2 = \"test\"");
    }

    [Fact]
    public void Convert_ExpressionBodyTupleReturn_ConvertsToBlock()
    {
        // Arrange
        var sourceCode = @"
class Test
{
    public (int x, string y) GetData() => (42, ""test"");
}";

        // Act
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var converted = _converter.Convert(root, "net35");
        var convertedCode = converted.ToFullString();

        // Assert
        convertedCode.Should().Contain("public void GetData(out int x, out string y)");
        convertedCode.Should().Contain("x = 42");
        convertedCode.Should().Contain("y = \"test\"");
        convertedCode.Should().Contain("return;");
        convertedCode.Should().NotContain("=>");
    }

    [Fact]
    public void Convert_MethodWithoutTupleReturn_Unchanged()
    {
        // Arrange
        var sourceCode = @"
class Test
{
    public int GetValue()
    {
        return 42;
    }
}";

        // Act
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var originalCode = root.ToFullString();
        var converted = _converter.Convert(root, "net35");
        var convertedCode = converted.ToFullString();

        // Assert - code should be unchanged
        convertedCode.Should().Be(originalCode);
    }

    [Fact]
    public void Convert_PreservesLeadingTrivia()
    {
        // Arrange
        var sourceCode = @"
class Test
{
    // This is a comment
    public (int, string) GetData()
    {
        return (42, ""test"");
    }
}";

        // Act
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var converted = _converter.Convert(root, "net35");
        var convertedCode = converted.ToFullString();

        // Assert
        convertedCode.Should().Contain("// This is a comment");
        convertedCode.Should().Contain("public void GetData(out int item1, out string item2)");
    }

    [Fact]
    public void Convert_PreservesTrailingTrivia()
    {
        // Arrange
        var sourceCode = @"
class Test
{
    public (int, string) GetData() // Inline comment
    {
        return (42, ""test"");
    }
}";

        // Act
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var converted = _converter.Convert(root, "net35");
        var convertedCode = converted.ToFullString();

        // Assert
        convertedCode.Should().Contain("// Inline comment");
        convertedCode.Should().Contain("public void GetData(out int item1, out string item2)");
    }

    [Fact]
    public void Convert_ThreeElementTuple_ConvertsAll()
    {
        // Arrange
        var sourceCode = @"
class Test
{
    public (int x, string y, bool z) GetData()
    {
        return (42, ""test"", true);
    }
}";

        // Act
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var converted = _converter.Convert(root, "net35");
        var convertedCode = converted.ToFullString();

        // Assert
        convertedCode.Should().Contain("public void GetData(out int x, out string y, out bool z)");
        convertedCode.Should().Contain("x = 42");
        convertedCode.Should().Contain("y = \"test\"");
        convertedCode.Should().Contain("z = true");
    }

    [Fact]
    public void Convert_TupleWithVariableExpression_ConvertsCorrectly()
    {
        // Arrange
        var sourceCode = @"
class Test
{
    public (int, string) GetData()
    {
        int value = 42;
        string text = ""test"";
        return (value, text);
    }
}";

        // Act
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var converted = _converter.Convert(root, "net35");
        var convertedCode = converted.ToFullString();

        // Assert
        convertedCode.Should().Contain("public void GetData(out int item1, out string item2)");
        convertedCode.Should().Contain("item1 = value");
        convertedCode.Should().Contain("item2 = text");
    }

    [Fact]
    public void Convert_MultipleReturnsInMethod_ConvertsAll()
    {
        // Arrange
        var sourceCode = @"
class Test
{
    public (int, string) GetData(bool flag)
    {
        if (flag)
        {
            return (42, ""yes"");
        }
        return (0, ""no"");
    }
}";

        // Act
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var converted = _converter.Convert(root, "net35");
        var convertedCode = converted.ToFullString();

        // Assert
        convertedCode.Should().Contain("public void GetData(bool flag, out int item1, out string item2)");
        convertedCode.Should().Contain("item1 = 42");
        convertedCode.Should().Contain("item2 = \"yes\"");
        convertedCode.Should().Contain("item1 = 0");
        convertedCode.Should().Contain("item2 = \"no\"");
    }

    [Theory]
    [InlineData("net35")]
    public async Task Convert_ValidatesSuccessfully_OnNet35Only(string targetFramework)
    {
        // Arrange - use C# 7.0 tuple syntax
        var sourceCode = @"
using System;

public class Calculator
{
    public (int sum, int product) Calculate(int a, int b)
    {
        return (a + b, a * b);
    }
}";

        // Act
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var converted = _converter.Convert(root, targetFramework);
        var convertedCode = converted.ToFullString();

        // Assert - converted code should compile on target framework
        var isValid = await ValidatesSuccessfullyAsync(targetFramework, convertedCode);
        isValid.Should().BeTrue($"converted code should compile on {targetFramework}");
    }

    [Fact]
    public void Convert_NestedTupleTypes_ConvertsOuterTuple()
    {
        // Arrange - nested tuples are complex, we only convert method-level returns
        var sourceCode = @"
class Test
{
    public (int, (string, bool)) GetData()
    {
        return (42, (""test"", true));
    }
}";

        // Act
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var converted = _converter.Convert(root, "net35");
        var convertedCode = converted.ToFullString();

        // Assert - converts outer tuple to out params, inner tuple needs ValueTuple support
        convertedCode.Should().Contain("public void GetData(out int item1, out (string, bool) item2)");
    }
}
