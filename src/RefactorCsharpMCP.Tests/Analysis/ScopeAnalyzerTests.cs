using FluentAssertions;
using RefactorCsharpMCP.Core.Analysis;

namespace RefactorCsharpMCP.Tests.Analysis;

public class ScopeAnalyzerTests
{
    [Fact]
    public void AnalyzeScope_WithValidInput_ShouldReturnStructuredResult()
    {
        // Arrange
        var sourceCode = @"public class Service
{
    private ILogger _logger;

    public void Process(string name)
    {
        int count = 0;
        _logger.Log(name);
        count++;
    }
}";
        var analyzer = new ScopeAnalyzer();

        // Act
        var result = analyzer.AnalyzeScope(sourceCode, "Service", "Process", 5, 8);

        // Assert
        result.Should().NotBeNull();
        result.LocalVariables.Should().NotBeNull();
        result.ParameterVariables.Should().NotBeNull();
        result.FieldVariables.Should().NotBeNull();
        result.ExternalMethodCalls.Should().NotBeNull();
    }

    [Fact]
    public void AnalyzeScope_WithSimpleStatement_ShouldAnalyze()
    {
        // Arrange
        var sourceCode = @"public class Service
{
    public void Process()
    {
        Console.WriteLine(""Hello"");
    }
}";
        var analyzer = new ScopeAnalyzer();

        // Act
        var result = analyzer.AnalyzeScope(sourceCode, "Service", "Process", 4, 4);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void AnalyzeExtraction_WithValidInput_ShouldReturnAnalysis()
    {
        // Arrange
        var sourceCode = @"public class Service
{
    public void Process()
    {
        int x = 5;
        Console.WriteLine(x);
    }
}";
        var analyzer = new ScopeAnalyzer();

        // Act
        var result = analyzer.AnalyzeExtraction(sourceCode, "Service", "Process", 5, 5);

        // Assert
        result.Should().NotBeNull();
        result.CanExtract.Should().BeTrue();
        result.Issues.Should().NotBeNull();
        result.VariablesNeeded.Should().NotBeNull();
        result.ReturnType.Should().Be("void");
    }

    [Fact]
    public void AnalyzeScope_WithNonExistentMethod_ShouldReturnEmpty()
    {
        // Arrange
        var sourceCode = "public class Test { }";
        var analyzer = new ScopeAnalyzer();

        // Act
        var result = analyzer.AnalyzeScope(sourceCode, "Test", "NonExistent", 1, 5);

        // Assert
        result.LocalVariables.Should().BeEmpty();
        result.ParameterVariables.Should().BeEmpty();
        result.FieldVariables.Should().BeEmpty();
        result.ExternalMethodCalls.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzeExtraction_WithInvalidInput_ShouldHandleGracefully()
    {
        // Arrange
        var sourceCode = "public class Test { }";
        var analyzer = new ScopeAnalyzer();

        // Act
        var result = analyzer.AnalyzeExtraction(sourceCode, "Test", "NonExistent", 1, 5);

        // Assert
        result.Should().NotBeNull();
        result.CanExtract.Should().BeTrue();
    }
}
