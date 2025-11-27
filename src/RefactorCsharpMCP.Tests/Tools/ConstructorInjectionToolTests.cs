using FluentAssertions;
using RefactorCsharpMCP.Server.Formatting;
using RefactorCsharpMCP.Server.Tools;
using System.Text.Json;

namespace RefactorCsharpMCP.Tests.Tools;

public class ConstructorInjectionToolTests
{
    [Fact]
    public async Task ConstructorInjection_WithValidInput_ShouldReturnSuccessResponse()
    {
        // Arrange
        var tool = new ConstructorInjectionTool(new JsonResponseFormatter());
        var sourceCode = @"public class UserService
{
    public void CreateUser(ILogger logger, string username)
    {
        logger.Log(""Creating user"");
    }
}";

        // Act
        var result = await tool.ConstructorInjection(sourceCode, "UserService", "CreateUser", "logger", false);

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

        dict!["success"].GetBoolean().Should().BeTrue();
        dict["refactoredCode"].GetString().Should().Contain("private readonly ILogger _logger;");
        dict["message"].GetString().Should().Contain("Converted 1 parameter(s)");
    }

    [Fact]
    public async Task ConstructorInjection_WithMultipleParameters_ShouldParseCommaSeparated()
    {
        // Arrange
        var tool = new ConstructorInjectionTool(new JsonResponseFormatter());
        var sourceCode = @"public class DataService
{
    public void Process(ILogger logger, IConfig config, string data)
    {
        logger.Log(data);
    }
}";

        // Act
        var result = await tool.ConstructorInjection(sourceCode, "DataService", "Process", "logger,config", false);

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

        dict!["success"].GetBoolean().Should().BeTrue();
        var injectedParams = dict["injectedParameters"].EnumerateArray()
            .Select(e => e.GetString()!)
            .ToArray();
        injectedParams.Should().HaveCount(2);
        injectedParams.Should().Contain("logger");
        injectedParams.Should().Contain("config");
    }

    [Fact]
    public async Task ConstructorInjection_WithProperties_ShouldIndicatePropertyInjection()
    {
        // Arrange
        var tool = new ConstructorInjectionTool(new JsonResponseFormatter());
        var sourceCode = @"public class Service
{
    public void Execute(ILogger logger)
    {
        logger.Log(""test"");
    }
}";

        // Act
        var result = await tool.ConstructorInjection(sourceCode, "Service", "Execute", "logger", true);

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

        dict!["success"].GetBoolean().Should().BeTrue();
        dict["injectionType"].GetString().Should().Be("properties");
        dict["refactoredCode"].GetString().Should().Contain("public ILogger Logger { get; }");
    }

    [Fact]
    public async Task ConstructorInjection_WithInvalidInput_ShouldReturnErrorResponse()
    {
        // Arrange
        var tool = new ConstructorInjectionTool(new JsonResponseFormatter());

        // Act
        var result = await tool.ConstructorInjection("", "TestClass", "TestMethod", "param", false);

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

        dict!["success"].GetBoolean().Should().BeFalse();
        dict["error"].GetString().Should().Contain("Source code cannot be empty");
    }

    [Fact]
    public async Task ConstructorInjection_WithSemicolonSeparator_ShouldParseParameters()
    {
        // Arrange
        var tool = new ConstructorInjectionTool(new JsonResponseFormatter());
        var sourceCode = @"public class TestService
{
    public void Method(ILogger logger, IConfig config)
    {
        logger.Log(""test"");
    }
}";

        // Act
        var result = await tool.ConstructorInjection(sourceCode, "TestService", "Method", "logger;config", false);

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

        dict!["success"].GetBoolean().Should().BeTrue();
        var injectedParams = dict["injectedParameters"].EnumerateArray()
            .Select(e => e.GetString()!)
            .ToArray();
        injectedParams.Should().HaveCount(2);
    }
}
