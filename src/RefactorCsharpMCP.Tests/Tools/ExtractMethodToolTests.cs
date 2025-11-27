using FluentAssertions;
using RefactorCsharpMCP.Server.Formatting;
using RefactorCsharpMCP.Server.Tools;
using System.Text.Json;

namespace RefactorCsharpMCP.Tests.Tools;

public class ExtractMethodToolTests
{
    [Fact]
    public async Task ExtractMethod_WithValidInput_ShouldReturnSuccessResponse()
    {
        // Arrange
        var tool = new ExtractMethodTool(new JsonResponseFormatter());
        var sourceCode = @"public class TestClass
{
    public void Method()
    {
        var x = 1;
        var y = 2;
        var z = x + y;
    }
}";

        // Act
        var result = await tool.ExtractMethod(sourceCode, 5, 6, "Calculate");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

        dict!["success"].GetBoolean().Should().BeTrue();
        dict["refactoredCode"].GetString().Should().Contain("Calculate();");
        dict["message"].GetString().Should().Contain("Extracted method 'Calculate'");
    }

    [Fact]
    public async Task ExtractMethod_WithInvalidInput_ShouldReturnErrorResponse()
    {
        // Arrange
        var tool = new ExtractMethodTool(new JsonResponseFormatter());

        // Act
        var result = await tool.ExtractMethod("", 1, 2, "TestMethod");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

        dict!["success"].GetBoolean().Should().BeFalse();
        dict["error"].GetString().Should().Contain("Source code cannot be empty");
    }

    [Fact]
    public async Task ExtractMethod_WithInvalidLineRange_ShouldReturnErrorResponse()
    {
        // Arrange
        var tool = new ExtractMethodTool(new JsonResponseFormatter());
        var sourceCode = "public class Test { }";

        // Act
        var result = await tool.ExtractMethod(sourceCode, 10, 5, "TestMethod");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

        dict!["success"].GetBoolean().Should().BeFalse();
        dict["error"].GetString().Should().Contain("Start line must be less than or equal to end line");
    }
}
