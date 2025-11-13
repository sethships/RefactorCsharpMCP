using FluentAssertions;
using RefactorCsharpMCP.Server.Tools;
using System.Text.Json;

namespace RefactorCsharpMCP.Tests.Tools;

public class RenameSymbolToolTests
{
    [Fact]
    public async Task RenameSymbol_WithValidLocalVariable_ShouldReturnSuccessResponse()
    {
        // Arrange
        var tool = new RenameSymbolTool();
        var sourceCode = @"public class Calculator
{
    public void Calculate()
    {
        int value = 5;
        var result = value * 2;
        Console.WriteLine(result);
    }
}";

        // Act - Rename 'value' variable at line 5, column 13
        var result = await tool.RenameSymbol(sourceCode, lineNumber: 5, columnNumber: 13, newName: "number");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();

        var refactoredCode = doc.RootElement.GetProperty("refactoredCode").GetString();
        refactoredCode.Should().Contain("int number = 5;");
        refactoredCode.Should().Contain("var result = number * 2;");
        refactoredCode.Should().NotContain("int value = 5;");
    }

    [Fact]
    public async Task RenameSymbol_WithEmptySourceCode_ShouldReturnError()
    {
        // Arrange
        var tool = new RenameSymbolTool();

        // Act
        var result = await tool.RenameSymbol("", lineNumber: 1, columnNumber: 1, newName: "newName");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("Source code cannot be empty");
    }

    [Fact]
    public async Task RenameSymbol_WithSourceCodeExceeding1MB_ShouldReturnError()
    {
        // Arrange
        var tool = new RenameSymbolTool();
        var largeSourceCode = new string('x', 1_000_001); // Just over 1MB

        // Act
        var result = await tool.RenameSymbol(largeSourceCode, lineNumber: 1, columnNumber: 1, newName: "newName");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("exceeds 1MB limit");
    }

    [Fact]
    public async Task RenameSymbol_WithInvalidLineNumber_ShouldReturnError()
    {
        // Arrange
        var tool = new RenameSymbolTool();
        var sourceCode = "public class Test { }";

        // Act - Line number 0 (invalid)
        var result = await tool.RenameSymbol(sourceCode, lineNumber: 0, columnNumber: 1, newName: "newName");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("Line number must be between 1 and 100000");
    }

    [Fact]
    public async Task RenameSymbol_WithInvalidColumnNumber_ShouldReturnError()
    {
        // Arrange
        var tool = new RenameSymbolTool();
        var sourceCode = "public class Test { }";

        // Act - Column number 0 (invalid)
        var result = await tool.RenameSymbol(sourceCode, lineNumber: 1, columnNumber: 0, newName: "newName");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("Column number must be between 1 and 10000");
    }

    [Fact]
    public async Task RenameSymbol_WithEmptyNewName_ShouldReturnError()
    {
        // Arrange
        var tool = new RenameSymbolTool();
        var sourceCode = "public class Test { private int value; }";

        // Act
        var result = await tool.RenameSymbol(sourceCode, lineNumber: 1, columnNumber: 33, newName: "");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("valid C# identifier");
    }

    [Theory]
    [InlineData("123invalid")]  // Starts with digit
    [InlineData("my-name")]  // Contains hyphen
    [InlineData("my name")]  // Contains space
    public async Task RenameSymbol_WithInvalidIdentifierName_ShouldReturnError(string invalidName)
    {
        // Arrange
        var tool = new RenameSymbolTool();
        var sourceCode = "public class Test { private int value; }";

        // Act
        var result = await tool.RenameSymbol(sourceCode, lineNumber: 1, columnNumber: 33, newName: invalidName);

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("valid C# identifier");
    }

    [Fact]
    public async Task RenameSymbol_WithPrivateField_ShouldRenameAllReferences()
    {
        // Arrange
        var tool = new RenameSymbolTool();
        var sourceCode = @"public class Service
{
    private int _count;

    public Service()
    {
        _count = 0;
    }

    public void Increment()
    {
        _count++;
    }

    public int GetCount()
    {
        return _count;
    }
}";

        // Act - Rename '_count' field at line 3
        var result = await tool.RenameSymbol(sourceCode, lineNumber: 3, columnNumber: 17, newName: "_value");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();

        var refactoredCode = doc.RootElement.GetProperty("refactoredCode").GetString();
        refactoredCode.Should().Contain("private int _value;");
        refactoredCode.Should().Contain("_value = 0;");
        refactoredCode.Should().Contain("_value++;");
        refactoredCode.Should().Contain("return _value;");
        refactoredCode.Should().NotContain("_count");
    }

    [Fact]
    public async Task RenameSymbol_WithMethodParameter_ShouldRenameInMethodScope()
    {
        // Arrange
        var tool = new RenameSymbolTool();
        var sourceCode = @"public class Math
{
    public int Double(int input)
    {
        return input * 2;
    }
}";

        // Act - Rename 'input' parameter at line 3
        var result = await tool.RenameSymbol(sourceCode, lineNumber: 3, columnNumber: 27, newName: "value");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();

        var refactoredCode = doc.RootElement.GetProperty("refactoredCode").GetString();
        refactoredCode.Should().Contain("public int Double(int value)");
        refactoredCode.Should().Contain("return value * 2;");
        refactoredCode.Should().NotContain("input");
    }

    [Fact]
    public async Task RenameSymbol_WithValidUnderscorePrefixedName_ShouldSucceed()
    {
        // Arrange
        var tool = new RenameSymbolTool();
        var sourceCode = "public class Test { private int value; }";

        // Act - Rename to underscore-prefixed name (common for private fields)
        var result = await tool.RenameSymbol(sourceCode, lineNumber: 1, columnNumber: 33, newName: "_value");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("refactoredCode").GetString().Should().Contain("private int _value;");
    }
}
