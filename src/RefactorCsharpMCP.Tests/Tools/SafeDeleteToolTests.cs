using FluentAssertions;
using RefactorCsharpMCP.Server.Formatting;
using RefactorCsharpMCP.Server.Tools;
using System.Text.Json;

namespace RefactorCsharpMCP.Tests.Tools;

public class SafeDeleteToolTests
{
    [Fact]
    public async Task SafeDeleteMethod_WithValidInput_ShouldReturnSuccessResponse()
    {
        // Arrange
        var tool = new SafeDeleteTool(new JsonResponseFormatter());
        var sourceCode = @"public class Service
{
    private void UnusedHelper()
    {
        // Not used
    }

    public void Process()
    {
        // Main logic
    }
}";

        // Act
        var result = await tool.SafeDeleteMethod(sourceCode, "Service", "UnusedHelper");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("refactoredCode").GetString().Should().NotContain("UnusedHelper");
    }

    [Fact]
    public async Task SafeDeleteMethod_WithInvalidClassName_ShouldReturnError()
    {
        // Arrange
        var tool = new SafeDeleteTool(new JsonResponseFormatter());
        var sourceCode = "public class Test { }";

        // Act
        var result = await tool.SafeDeleteMethod(sourceCode, "123Invalid", "method");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("valid C# identifier");
    }

    [Fact]
    public async Task SafeDeleteMethod_WithEmptySourceCode_ShouldReturnError()
    {
        // Arrange
        var tool = new SafeDeleteTool(new JsonResponseFormatter());

        // Act
        var result = await tool.SafeDeleteMethod("", "Test", "method");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("Source code cannot be empty");
    }

    [Fact]
    public async Task SafeDeleteMethod_WithReferencedMethod_ShouldReturnError()
    {
        // Arrange
        var tool = new SafeDeleteTool(new JsonResponseFormatter());
        var sourceCode = @"public class Calculator
{
    public int Add(int a, int b)
    {
        return a + b;
    }

    public int Calculate(int x, int y)
    {
        return Add(x, y);
    }
}";

        // Act
        var result = await tool.SafeDeleteMethod(sourceCode, "Calculator", "Add");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("message").GetString().Should().Contain("is referenced");
    }
}
