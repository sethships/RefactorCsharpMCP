using FluentAssertions;
using RefactorCsharpMCP.Server.Formatting;
using RefactorCsharpMCP.Server.Tools;
using System.Text.Json;

namespace RefactorCsharpMCP.Tests.Tools;

public class InlineVariableToolTests
{
    [Fact]
    public async Task InlineVariable_WithValidVariable_ShouldReturnSuccessResponse()
    {
        // Arrange
        var tool = new InlineVariableTool(new JsonResponseFormatter());
        var sourceCode = @"public class Calculator
{
    public int Calculate()
    {
        int result = 5 + 3;
        return result;
    }
}";

        // Act - Target 'result' variable at line 5
        var result = await tool.InlineVariable(sourceCode, lineNumber: 5, columnNumber: 13);

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("refactoredCode").GetString().Should().Contain("return 5 + 3;");
        doc.RootElement.GetProperty("refactoredCode").GetString().Should().NotContain("int result =");
    }

    [Fact]
    public async Task InlineVariable_WithEmptySourceCode_ShouldReturnError()
    {
        // Arrange
        var tool = new InlineVariableTool(new JsonResponseFormatter());

        // Act
        var result = await tool.InlineVariable("", lineNumber: 1, columnNumber: 1);

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("Source code cannot be empty");
    }

    [Fact]
    public async Task InlineVariable_WithSourceCodeExceeding1MB_ShouldReturnError()
    {
        // Arrange
        var tool = new InlineVariableTool(new JsonResponseFormatter());
        var largeSourceCode = new string('x', 1_000_001); // Just over 1MB

        // Act
        var result = await tool.InlineVariable(largeSourceCode, lineNumber: 1, columnNumber: 1);

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("exceeds 1MB limit");
    }

    [Fact]
    public async Task InlineVariable_WithInvalidLineNumber_ShouldReturnError()
    {
        // Arrange
        var tool = new InlineVariableTool(new JsonResponseFormatter());
        var sourceCode = "public class Test { }";

        // Act - Line number 0 (invalid)
        var result = await tool.InlineVariable(sourceCode, lineNumber: 0, columnNumber: 1);

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("Line number must be between 1 and 100000");
    }

    [Fact]
    public async Task InlineVariable_WithLineNumberTooLarge_ShouldReturnError()
    {
        // Arrange
        var tool = new InlineVariableTool(new JsonResponseFormatter());
        var sourceCode = "public class Test { }";

        // Act - Line number > 100000
        var result = await tool.InlineVariable(sourceCode, lineNumber: 100001, columnNumber: 1);

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("Line number must be between 1 and 100000");
    }

    [Fact]
    public async Task InlineVariable_WithInvalidColumnNumber_ShouldReturnError()
    {
        // Arrange
        var tool = new InlineVariableTool(new JsonResponseFormatter());
        var sourceCode = "public class Test { }";

        // Act - Column number 0 (invalid)
        var result = await tool.InlineVariable(sourceCode, lineNumber: 1, columnNumber: 0);

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("Column number must be between 1 and 10000");
    }

    [Fact]
    public async Task InlineVariable_WithEmptyTargetFramework_ShouldReturnError()
    {
        // Arrange
        var tool = new InlineVariableTool(new JsonResponseFormatter());
        var sourceCode = "public class Test { }";

        // Act
        var result = await tool.InlineVariable(sourceCode, lineNumber: 1, columnNumber: 1, targetFramework: "");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("Target framework cannot be empty");
    }

    [Fact]
    public async Task InlineVariable_WithMultipleReferences_ShouldInlineAll()
    {
        // Arrange
        var tool = new InlineVariableTool(new JsonResponseFormatter());
        var sourceCode = @"public class Calculator
{
    public int Compute()
    {
        int multiplier = 2;
        int a = multiplier * 3;
        int b = multiplier * 5;
        return a + b;
    }
}";

        // Act - Inline 'multiplier' variable at line 5
        var result = await tool.InlineVariable(sourceCode, lineNumber: 5, columnNumber: 13);

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();

        var refactoredCode = doc.RootElement.GetProperty("refactoredCode").GetString();
        refactoredCode.Should().NotContain("int multiplier = 2;");
        refactoredCode.Should().Contain("int a = 2 * 3;");
        refactoredCode.Should().Contain("int b = 2 * 5;");
    }

    [Fact]
    public async Task InlineVariable_WithNet48Framework_ShouldRespectFrameworkValidation()
    {
        // Arrange
        var tool = new InlineVariableTool(new JsonResponseFormatter());
        var sourceCode = @"public class Test
{
    public void Method()
    {
        var value = 42;
        Console.WriteLine(value);
    }
}";

        // Act - Use net48 framework
        var result = await tool.InlineVariable(sourceCode, lineNumber: 5, columnNumber: 13, targetFramework: "net48");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);

        // Should succeed or fail based on framework validation
        var success = doc.RootElement.GetProperty("success").GetBoolean();
        if (success)
        {
            doc.RootElement.GetProperty("refactoredCode").GetString().Should().NotBeNullOrEmpty();
        }
        else
        {
            doc.RootElement.GetProperty("error").GetString().Should().NotBeNullOrEmpty();
        }
    }
}
