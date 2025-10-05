using FluentAssertions;
using RefactorCsharpMCP.Core.Analysis;

namespace RefactorCsharpMCP.Tests.Analysis;

public class DependencyAnalyzerTests
{
    [Fact]
    public void AnalyzeMethodDependencies_WithFieldAccess_ShouldDetectDependency()
    {
        // Arrange
        var sourceCode = @"public class Service
{
    private ILogger _logger;
    private IDatabase _database;

    public void Process()
    {
        _logger.Log(""Processing"");
    }
}";
        var analyzer = new DependencyAnalyzer();

        // Act
        var result = analyzer.AnalyzeMethodDependencies(sourceCode, "Service");

        // Assert
        result.Should().ContainKey("Process");
        result["Process"].FieldsAccessed.Should().Contain("_logger");
        result["Process"].FieldsAccessed.Should().NotContain("_database");
    }

    [Fact]
    public void AnalyzeMethodDependencies_WithMethodCalls_ShouldDetectDependencies()
    {
        // Arrange
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
        var analyzer = new DependencyAnalyzer();

        // Act
        var result = analyzer.AnalyzeMethodDependencies(sourceCode, "Calculator");

        // Assert
        result.Should().ContainKey("Calculate");
        result["Calculate"].MethodsCalled.Should().Contain("Add");
    }

    [Fact]
    public void AnalyzeMethodDependencies_WithParameters_ShouldCaptureTypes()
    {
        // Arrange
        var sourceCode = @"public class Service
{
    public void Process(string message, int count)
    {
        // Logic
    }
}";
        var analyzer = new DependencyAnalyzer();

        // Act
        var result = analyzer.AnalyzeMethodDependencies(sourceCode, "Service");

        // Assert
        result.Should().ContainKey("Process");
        result["Process"].ParameterTypes.Should().Contain("string");
        result["Process"].ParameterTypes.Should().Contain("int");
    }

    [Fact]
    public void AnalyzeFieldUsage_WithMultipleMethods_ShouldTrackUsage()
    {
        // Arrange
        var sourceCode = @"public class Service
{
    private ILogger _logger;
    private int _count;

    public void Initialize()
    {
        _logger.Setup();
        _count = 0;
    }

    public void Process()
    {
        _logger.Log(""Processing"");
    }
}";
        var analyzer = new DependencyAnalyzer();

        // Act
        var result = analyzer.AnalyzeFieldUsage(sourceCode, "Service");

        // Assert
        result.Should().ContainKey("_logger");
        result["_logger"].UsedInMethods.Should().Contain("Initialize");
        result["_logger"].UsedInMethods.Should().Contain("Process");
        result["_count"].UsedInMethods.Should().Contain("Initialize");
        result["_count"].UsedInMethods.Should().NotContain("Process");
    }

    [Fact]
    public void AnalyzeFieldUsage_WithReadonlyField_ShouldDetectModifier()
    {
        // Arrange
        var sourceCode = @"public class Service
{
    private readonly ILogger _logger;
    private int _count;
}";
        var analyzer = new DependencyAnalyzer();

        // Act
        var result = analyzer.AnalyzeFieldUsage(sourceCode, "Service");

        // Assert
        result["_logger"].IsReadOnly.Should().BeTrue();
        result["_count"].IsReadOnly.Should().BeFalse();
    }

    [Fact]
    public void AnalyzeFieldUsage_WithInitializer_ShouldDetect()
    {
        // Arrange
        var sourceCode = @"public class Config
{
    private int _maxRetries = 3;
    private string _name;
}";
        var analyzer = new DependencyAnalyzer();

        // Act
        var result = analyzer.AnalyzeFieldUsage(sourceCode, "Config");

        // Assert
        result["_maxRetries"].HasInitializer.Should().BeTrue();
        result["_name"].HasInitializer.Should().BeFalse();
    }

    [Fact]
    public void AnalyzeMethodDependencies_WithNonExistentClass_ShouldReturnEmpty()
    {
        // Arrange
        var sourceCode = "public class Test { }";
        var analyzer = new DependencyAnalyzer();

        // Act
        var result = analyzer.AnalyzeMethodDependencies(sourceCode, "NonExistent");

        // Assert
        result.Should().BeEmpty();
    }
}
