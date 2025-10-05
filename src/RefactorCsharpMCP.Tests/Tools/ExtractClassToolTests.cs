using FluentAssertions;
using RefactorCsharpMCP.Server.Tools;
using System.Text.Json;

namespace RefactorCsharpMCP.Tests.Tools;

public class ExtractClassToolTests
{
    [Fact]
    public async Task ExtractClass_WithValidInput_ShouldReturnSuccessResponse()
    {
        // Arrange
        var tool = new ExtractClassTool();
        var sourceCode = @"public class Service
{
    private ILogger _logger;
    private IDatabase _database;

    public void Process()
    {
        // Logic
    }
}";

        // Act
        var result = await tool.ExtractClass(sourceCode, "Service", "LoggingContext", "_logger");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("refactoredCode").GetString().Should().Contain("public class LoggingContext");
    }

    [Fact]
    public async Task ExtractClass_WithInvalidClassName_ShouldReturnError()
    {
        // Arrange
        var tool = new ExtractClassTool();
        var sourceCode = "public class Test { }";

        // Act
        var result = await tool.ExtractClass(sourceCode, "123Invalid", "NewClass", "_field");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("valid C# identifier");
    }

    [Fact]
    public async Task ExtractClass_WithEmptySourceCode_ShouldReturnError()
    {
        // Arrange
        var tool = new ExtractClassTool();

        // Act
        var result = await tool.ExtractClass("", "Test", "NewClass", "_field");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("Source code cannot be empty");
    }

    [Fact]
    public async Task ExtractClass_WithNonExistentField_ShouldReturnError()
    {
        // Arrange
        var tool = new ExtractClassTool();
        var sourceCode = @"public class Service
{
    private ILogger _logger;
}";

        // Act
        var result = await tool.ExtractClass(sourceCode, "Service", "NewClass", "_missing");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("message").GetString().Should().Contain("not found");
    }
}
