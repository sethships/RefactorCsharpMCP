using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using RefactorCsharpMCP.Core.SyntaxConversion;
using RefactorCsharpMCP.Tests.Infrastructure;
using Xunit;

namespace RefactorCsharpMCP.Tests.SyntaxConversion;

/// <summary>
/// Tests for NullableReferenceTypeStripper that removes C# 8.0 nullable annotations
/// for older frameworks.
/// </summary>
[Collection("CacheTests")]
public class NullableReferenceTypeStripperTests : FrameworkTestFixture
{
    private readonly NullableReferenceTypeStripper _converter;

    public NullableReferenceTypeStripperTests()
    {
        _converter = new NullableReferenceTypeStripper();
    }

    [Fact]
    public void ConverterProperties_AreCorrect()
    {
        // Assert
        _converter.Name.Should().Be("NullableReferenceTypeStripper");
        _converter.MinimumSourceLanguageVersion.Should().Be(LanguageVersion.CSharp8);
        _converter.MaximumTargetLanguageVersion.Should().Be(LanguageVersion.CSharp7_3);
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
        canConvert.Should().BeTrue($"framework {targetFramework} should require nullable stripping");
    }

    [Theory]
    [InlineData("net8.0")]  // C# 12
    [InlineData("net9.0")]  // C# 13
    [InlineData("netstandard2.1")]  // C# 8.0
    public void CanConvert_ReturnsFalseForModernFrameworks(string targetFramework)
    {
        // Arrange
        var sourceCode = "class Test { }";
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();

        // Act
        var canConvert = _converter.CanConvert(root, targetFramework);

        // Assert
        canConvert.Should().BeFalse($"framework {targetFramework} supports nullable reference types");
    }

    [Fact]
    public void Convert_SimpleNullableType_StripsAnnotation()
    {
        // Arrange
        var sourceCode = @"
class Test
{
    public string? Name { get; set; }
}";

        // Act
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var converted = _converter.Convert(root, "net48");
        var convertedCode = converted.ToFullString();

        // Assert
        convertedCode.Should().Contain("public string Name");
        convertedCode.Should().NotContain("string?");
    }

    [Fact]
    public void Convert_GenericNullableType_StripsAnnotation()
    {
        // Arrange
        var sourceCode = @"
using System.Collections.Generic;

class Test
{
    public List<string?> Names { get; set; }
}";

        // Act
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var converted = _converter.Convert(root, "net48");
        var convertedCode = converted.ToFullString();

        // Assert
        convertedCode.Should().Contain("List<string>");
        convertedCode.Should().NotContain("string?");
    }

    [Fact]
    public void Convert_NullForgiveOperator_RemovesOperator()
    {
        // Arrange
        var sourceCode = @"
class Test
{
    public void Method(string input)
    {
        var result = input!.ToUpper();
    }
}";

        // Act
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var converted = _converter.Convert(root, "net48");
        var convertedCode = converted.ToFullString();

        // Assert
        convertedCode.Should().Contain("input.ToUpper()");
        convertedCode.Should().NotContain("input!");
    }

    [Fact]
    public void Convert_NullableDirective_RemovesDirective()
    {
        // Arrange
        var sourceCode = @"#nullable enable
class Test
{
    public string? Name { get; set; }
}";

        // Act
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var converted = _converter.Convert(root, "net48");
        var convertedCode = converted.ToFullString();

        // Assert
        convertedCode.Should().NotContain("#nullable");
        convertedCode.Should().Contain("public string Name");
    }

    [Fact]
    public void Convert_MultipleNullableTypes_StripsAll()
    {
        // Arrange
        var sourceCode = @"
class Test
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int? Age { get; set; }

    public void Method(string? input, int? count)
    {
        var result = input!.ToUpper();
    }
}";

        // Act
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var converted = _converter.Convert(root, "net48");
        var convertedCode = converted.ToFullString();

        // Assert
        convertedCode.Should().Contain("public string FirstName");
        convertedCode.Should().Contain("public string LastName");
        // Known limitation: Currently strips value type nullables too (int? → int)
        // This requires semantic analysis to distinguish reference vs value types
        convertedCode.Should().Contain("public int Age");
        convertedCode.Should().Contain("(string input, int count)");
        convertedCode.Should().Contain("input.ToUpper()");
        convertedCode.Should().NotContain("string?");
        convertedCode.Should().NotContain("input!");
    }

    [Fact]
    public void Convert_PreservesLeadingTrivia()
    {
        // Arrange
        var sourceCode = @"
class Test
{
    // This is a comment
    public string? Name { get; set; }
}";

        // Act
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var converted = _converter.Convert(root, "net48");
        var convertedCode = converted.ToFullString();

        // Assert
        convertedCode.Should().Contain("// This is a comment");
        convertedCode.Should().Contain("public string Name");
    }

    [Fact]
    public void Convert_PreservesTrailingTrivia()
    {
        // Arrange
        var sourceCode = @"
class Test
{
    public string? Name { get; set; } // Inline comment
}";

        // Act
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var converted = _converter.Convert(root, "net48");
        var convertedCode = converted.ToFullString();

        // Assert
        convertedCode.Should().Contain("// Inline comment");
        convertedCode.Should().Contain("public string Name");
    }

    [Fact]
    public void Convert_NestedNullableGenerics_StripsAll()
    {
        // Arrange
        var sourceCode = @"
using System.Collections.Generic;

class Test
{
    public Dictionary<string?, List<int?>> Data { get; set; }
}";

        // Act
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var converted = _converter.Convert(root, "net48");
        var convertedCode = converted.ToFullString();

        // Assert
        // Known limitation: Strips all nullable syntax including value types
        convertedCode.Should().Contain("Dictionary<string, List<int>>");
        convertedCode.Should().NotContain("string?");
        convertedCode.Should().NotContain("int?");
    }

    [Theory]
    [FrameworkMatrix(Filter = FrameworkFamily.Framework)]
    public async Task Convert_ValidatesSuccessfully_OnFrameworkTargets(string targetFramework)
    {
        // Arrange - use C# 8.0 nullable syntax
        var sourceCode = @"
using System;
using System.Collections.Generic;

public class Person
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public List<string?> Aliases { get; set; }

    public string GetFullName()
    {
        return FirstName! + ' ' + LastName!;
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
    public void Convert_WithNoNullableAnnotations_ReturnsUnchanged()
    {
        // Arrange
        var sourceCode = @"
class Test
{
    public string Name { get; set; }
    public int Count { get; set; }
}";

        // Act
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var originalCode = root.ToFullString();
        var converted = _converter.Convert(root, "net48");
        var convertedCode = converted.ToFullString();

        // Assert - code should be unchanged
        convertedCode.Should().Be(originalCode);
    }

    [Fact]
    public void Convert_ValueTypeNullable_AlsoStripped()
    {
        // Arrange
        var sourceCode = @"
class Test
{
    public int? Count { get; set; }
    public DateTime? LastModified { get; set; }
}";

        // Act
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var converted = _converter.Convert(root, "net48");
        var convertedCode = converted.ToFullString();

        // Assert - Known limitation: value type nullables are also stripped
        // This requires semantic analysis to distinguish Nullable<T> from reference type nullables
        convertedCode.Should().Contain("int Count");
        convertedCode.Should().Contain("DateTime LastModified");
        convertedCode.Should().NotContain("?");
    }
}
