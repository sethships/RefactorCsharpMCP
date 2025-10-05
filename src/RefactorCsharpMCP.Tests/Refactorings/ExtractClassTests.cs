using FluentAssertions;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Tests.Refactorings;

public class ExtractClassTests
{
    [Fact]
    public void Execute_WithSingleField_ShouldExtractToNewClass()
    {
        // Arrange
        var sourceCode = @"namespace MyApp
{
    public class UserService
    {
        private ILogger _logger;
        private IDatabase _database;

        public void Process()
        {
            // Processing logic
        }
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "UserService", "LoggingContext", "_logger");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("public class LoggingContext");
        result.RefactoredCode.Should().Contain("private ILogger _logger;");
        result.RefactoredCode.Should().Contain("private readonly LoggingContext _loggingContext = new LoggingContext();");
        result.RefactoredCode.Should().Contain("private IDatabase _database;");
    }

    [Fact]
    public void Execute_WithMultipleFields_ShouldExtractAllToNewClass()
    {
        // Arrange
        var sourceCode = @"public class Service
{
    private ILogger _logger;
    private IMetrics _metrics;
    private IDatabase _database;

    public void DoWork()
    {
        // Work
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "Service", "Telemetry", "_logger,_metrics");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("public class Telemetry");
        result.RefactoredCode.Should().Contain("private ILogger _logger;");
        result.RefactoredCode.Should().Contain("private IMetrics _metrics;");
        result.RefactoredCode.Should().Contain("private IDatabase _database;");
        result.RefactoredCode.Should().Contain("private readonly Telemetry _telemetry");
    }

    [Fact]
    public void Execute_WithSemicolonSeparator_ShouldParseFieldNames()
    {
        // Arrange
        var sourceCode = @"public class Service
{
    private int _count;
    private string _name;
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "Service", "Data", "_count;_name");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("public class Data");
        result.RefactoredCode.Should().Contain("private int _count;");
        result.RefactoredCode.Should().Contain("private string _name;");
    }

    [Fact]
    public void Execute_WithFieldsAndMethods_ShouldExtractBoth()
    {
        // Arrange
        var sourceCode = @"public class Service
{
    private ILogger _logger;
    private string _message;

    private void Log(string msg)
    {
        _logger.Log(msg);
    }

    public void Process()
    {
        // Main logic
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "Service", "Logger", "_logger", "Log");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("public class Logger");
        result.RefactoredCode.Should().Contain("private ILogger _logger;");
        result.RefactoredCode.Should().Contain("private void Log(string msg)");
        result.RefactoredCode.Should().Contain("public void Process()");
        result.RefactoredCode.Should().Contain("private string _message;");
    }

    [Fact]
    public void Execute_WithEmptySourceCode_ShouldReturnFailure()
    {
        // Arrange
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute("", "Test", "NewClass", "_field");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Source code cannot be empty");
    }

    [Fact]
    public void Execute_WithEmptyClassName_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = "public class Test { }";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "", "NewClass", "_field");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Class name cannot be empty");
    }

    [Fact]
    public void Execute_WithEmptyNewClassName_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = "public class Test { }";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "Test", "", "_field");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("New class name cannot be empty");
    }

    [Fact]
    public void Execute_WithEmptyFieldNames_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = "public class Test { }";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "Test", "NewClass", "");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Field names cannot be empty");
    }

    [Fact]
    public void Execute_WithNonExistentClass_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"public class RealClass
{
    private int _field;
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "FakeClass", "NewClass", "_field");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Class 'FakeClass' not found");
    }

    [Fact]
    public void Execute_WithNonExistentField_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"public class TestClass
{
    private int _realField;
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "TestClass", "NewClass", "_fakeField");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Field '_fakeField' not found");
    }

    [Fact]
    public void Execute_WithNonExistentMethod_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"public class TestClass
{
    private int _field;

    public void RealMethod() { }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "TestClass", "NewClass", "_field", "FakeMethod");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Method 'FakeMethod' not found");
    }

    [Fact]
    public void Execute_WithFileScopedNamespace_ShouldExtractToNewClass()
    {
        // Arrange
        var sourceCode = @"namespace MyApp;

public class Service
{
    private ILogger _logger;
    private IDatabase _database;
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "Service", "LoggerContext", "_logger");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("public class LoggerContext");
        result.RefactoredCode.Should().Contain("private ILogger _logger;");
    }

    [Fact]
    public void Execute_WithNoNamespace_ShouldExtractToNewClass()
    {
        // Arrange
        var sourceCode = @"public class Service
{
    private ILogger _logger;
    private IDatabase _database;
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "Service", "LoggerContext", "_logger");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("public class LoggerContext");
        result.RefactoredCode.Should().Contain("private ILogger _logger;");
        result.RefactoredCode.Should().Contain("private readonly LoggerContext _loggerContext");
    }

    [Fact]
    public void Execute_ShouldCreateReadonlyFieldWithInstantiation()
    {
        // Arrange
        var sourceCode = @"public class Service
{
    private ILogger _logger;
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "Service", "LoggingService", "_logger");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("private readonly LoggingService _loggingService = new LoggingService();");
    }
}
