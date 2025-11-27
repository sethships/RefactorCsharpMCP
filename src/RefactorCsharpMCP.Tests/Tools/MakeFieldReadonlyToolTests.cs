using FluentAssertions;
using RefactorCsharpMCP.Server.Formatting;
using RefactorCsharpMCP.Server.Tools;
using System.Text.Json;

namespace RefactorCsharpMCP.Tests.Tools;

public class MakeFieldReadonlyToolTests
{
    [Fact]
    public async Task MakeFieldReadonly_WithValidInput_ShouldReturnSuccessResponse()
    {
        // Arrange
        var tool = new MakeFieldReadonlyTool(new JsonResponseFormatter());
        var sourceCode = @"public class Service
{
    private ILogger _logger;

    public Service(ILogger logger)
    {
        _logger = logger;
    }
}";

        // Act
        var result = await tool.MakeFieldReadonly(sourceCode, "Service", "_logger");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("refactoredCode").GetString().Should().Contain("private readonly ILogger _logger;");
    }

    [Fact]
    public async Task MakeFieldReadonly_WithInvalidClassName_ShouldReturnError()
    {
        // Arrange
        var tool = new MakeFieldReadonlyTool(new JsonResponseFormatter());
        var sourceCode = "public class Test { }";

        // Act
        var result = await tool.MakeFieldReadonly(sourceCode, "123Invalid", "_field");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("valid C# identifier");
    }

    [Fact]
    public async Task MakeFieldReadonly_WithEmptySourceCode_ShouldReturnError()
    {
        // Arrange
        var tool = new MakeFieldReadonlyTool(new JsonResponseFormatter());

        // Act
        var result = await tool.MakeFieldReadonly("", "Test", "_field");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("Source code cannot be empty");
    }

    [Fact]
    public async Task MakeFieldReadonly_WithFieldAssignedInMethod_ShouldReturnError()
    {
        // Arrange
        var tool = new MakeFieldReadonlyTool(new JsonResponseFormatter());
        var sourceCode = @"public class Counter
{
    private int _value;

    public void Reset()
    {
        _value = 0;
    }
}";

        // Act
        var result = await tool.MakeFieldReadonly(sourceCode, "Counter", "_value");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("message").GetString().Should().Contain("assigned outside of constructors");
    }
}
