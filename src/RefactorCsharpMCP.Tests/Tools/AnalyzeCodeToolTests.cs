using FluentAssertions;
using RefactorCsharpMCP.Server.Formatting;
using RefactorCsharpMCP.Server.Tools;
using System.Text.Json;

namespace RefactorCsharpMCP.Tests.Tools;

public class AnalyzeCodeToolTests
{
    [Fact]
    public async Task AnalyzeCode_WithValidInput_ShouldReturnSuccessResponse()
    {
        // Arrange
        var tool = new AnalyzeCodeTool(new JsonResponseFormatter());
        var sourceCode = @"
using System;

public class Test
{
    public void Method()
    {
        Console.WriteLine(""Hello"");
    }
}";

        // Act
        var result = await tool.AnalyzeCode(sourceCode, "net8.0");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.TryGetProperty("diagnostics", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("summary", out _).Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeCode_WithEmptySourceCode_ShouldReturnError()
    {
        // Arrange
        var tool = new AnalyzeCodeTool(new JsonResponseFormatter());

        // Act
        var result = await tool.AnalyzeCode("", "net8.0");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("Source code cannot be empty");
    }

    [Fact]
    public async Task AnalyzeCode_WithEmptyFramework_ShouldReturnError()
    {
        // Arrange
        var tool = new AnalyzeCodeTool(new JsonResponseFormatter());
        var sourceCode = "public class Test { }";

        // Act
        var result = await tool.AnalyzeCode(sourceCode, "");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("Target framework cannot be empty");
    }

    [Fact]
    public async Task AnalyzeCode_WithSyntaxErrors_ShouldReturnErrorDiagnostics()
    {
        // Arrange
        var tool = new AnalyzeCodeTool(new JsonResponseFormatter());
        var sourceCode = @"
public class Test
{
    public void Method(
    {
        // Missing closing parenthesis
    }
}";

        // Act
        var result = await tool.AnalyzeCode(sourceCode, "net8.0");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();

        var diagnostics = doc.RootElement.GetProperty("diagnostics");
        diagnostics.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AnalyzeCode_WithSeverityFilter_ShouldFilterByLevel()
    {
        // Arrange
        var tool = new AnalyzeCodeTool(new JsonResponseFormatter());
        var sourceCode = @"
using System;

public class Test
{
    public void Method()
    {
        Console.WriteLine(""Test"");
    }
}";

        // Act - Filter for errors only
        var result = await tool.AnalyzeCode(sourceCode, "net8.0", "Error");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();

        // Should not find errors in clean code
        var diagnostics = doc.RootElement.GetProperty("diagnostics");
        diagnostics.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task AnalyzeCode_DiagnosticStructure_ShouldContainAllFields()
    {
        // Arrange
        var tool = new AnalyzeCodeTool(new JsonResponseFormatter());
        var sourceCode = @"
public class Test
{
    public void Method(
    {
    }
}";

        // Act
        var result = await tool.AnalyzeCode(sourceCode, "net8.0");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();

        var diagnostics = doc.RootElement.GetProperty("diagnostics");
        if (diagnostics.GetArrayLength() > 0)
        {
            var firstDiagnostic = diagnostics[0];
            firstDiagnostic.TryGetProperty("id", out _).Should().BeTrue();
            firstDiagnostic.TryGetProperty("severity", out _).Should().BeTrue();
            firstDiagnostic.TryGetProperty("message", out _).Should().BeTrue();
            firstDiagnostic.TryGetProperty("location", out _).Should().BeTrue();
            firstDiagnostic.TryGetProperty("category", out _).Should().BeTrue();
            firstDiagnostic.TryGetProperty("applicableRefactorings", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task AnalyzeCode_SummaryStructure_ShouldContainAllCounts()
    {
        // Arrange
        var tool = new AnalyzeCodeTool(new JsonResponseFormatter());
        var sourceCode = @"
using System;

public class Test
{
    public void Method()
    {
        Console.WriteLine(""Hello"");
    }
}";

        // Act
        var result = await tool.AnalyzeCode(sourceCode, "net8.0");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);

        var summary = doc.RootElement.GetProperty("summary");
        summary.TryGetProperty("totalDiagnostics", out _).Should().BeTrue();
        summary.TryGetProperty("errorCount", out _).Should().BeTrue();
        summary.TryGetProperty("warningCount", out _).Should().BeTrue();
        summary.TryGetProperty("infoCount", out _).Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeCode_WithLargeSourceCode_ShouldReturnError()
    {
        // Arrange
        var tool = new AnalyzeCodeTool(new JsonResponseFormatter());
        var largeSource = new string('x', 2 * 1024 * 1024); // > 1MB

        // Act
        var result = await tool.AnalyzeCode(largeSource, "net8.0");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("exceeds 1MB limit");
    }

    [Theory]
    [InlineData("net8.0")]
    [InlineData("net9.0")]
    [InlineData("net48")]
    [InlineData("netstandard2.0")]
    public async Task AnalyzeCode_WithSupportedFrameworks_ShouldSucceed(string framework)
    {
        // Arrange
        var tool = new AnalyzeCodeTool(new JsonResponseFormatter());
        var sourceCode = @"
using System;

public class Test
{
    public void Method()
    {
        Console.WriteLine(""Test"");
    }
}";

        // Act
        var result = await tool.AnalyzeCode(sourceCode, framework);

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
    }
}
