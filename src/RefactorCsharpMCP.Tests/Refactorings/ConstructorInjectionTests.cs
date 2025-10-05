using FluentAssertions;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Tests.Refactorings;

public class ConstructorInjectionTests
{
    [Fact]
    public void Execute_WithValidParameters_ShouldInjectAsFields()
    {
        // Arrange
        var sourceCode = @"public class UserService
{
    public void CreateUser(ILogger logger, IConfig config, string username)
    {
        logger.Log(""Creating user"");
        Console.WriteLine(username);
    }
}";
        var injector = new ConstructorInjection();

        // Act
        var result = injector.Execute(sourceCode, "UserService", "CreateUser", new[] { "logger", "config" }, useProperties: false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().NotBeNullOrEmpty();
        result.RefactoredCode.Should().Contain("private readonly ILogger _logger;");
        result.RefactoredCode.Should().Contain("private readonly IConfig _config;");
        result.RefactoredCode.Should().Contain("public UserService(ILogger logger, IConfig config)");
        result.RefactoredCode.Should().Contain("_logger = logger;");
        result.RefactoredCode.Should().Contain("_config = config;");
        result.RefactoredCode.Should().Contain("CreateUser(string username)");
        result.Message.Should().Contain("Converted 2 parameter(s)");
        result.Message.Should().Contain("fields");
    }

    [Fact]
    public void Execute_WithValidParameters_ShouldInjectAsProperties()
    {
        // Arrange
        var sourceCode = @"public class DataService
{
    public void Process(ILogger logger, string data)
    {
        logger.Log(data);
    }
}";
        var injector = new ConstructorInjection();

        // Act
        var result = injector.Execute(sourceCode, "DataService", "Process", new[] { "logger" }, useProperties: true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("public ILogger Logger { get; }");
        result.RefactoredCode.Should().Contain("public DataService(ILogger logger)");
        result.RefactoredCode.Should().Contain("Logger = logger;");
        result.RefactoredCode.Should().Contain("Process(string data)");
        result.Message.Should().Contain("properties");
    }

    [Fact]
    public void Execute_WithEmptySourceCode_ShouldReturnFailure()
    {
        // Arrange
        var injector = new ConstructorInjection();

        // Act
        var result = injector.Execute("", "TestClass", "TestMethod", new[] { "param" });

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Source code cannot be empty");
    }

    [Fact]
    public void Execute_WithEmptyClassName_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = "public class Test { }";
        var injector = new ConstructorInjection();

        // Act
        var result = injector.Execute(sourceCode, "", "Method", new[] { "param" });

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Class name cannot be empty");
    }

    [Fact]
    public void Execute_WithEmptyMethodName_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = "public class Test { }";
        var injector = new ConstructorInjection();

        // Act
        var result = injector.Execute(sourceCode, "Test", "", new[] { "param" });

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Method name cannot be empty");
    }

    [Fact]
    public void Execute_WithNoParameterNames_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = "public class Test { }";
        var injector = new ConstructorInjection();

        // Act
        var result = injector.Execute(sourceCode, "Test", "Method", Array.Empty<string>());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("At least one parameter name must be specified");
    }

    [Fact]
    public void Execute_WithNonExistentClass_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"public class RealClass
{
    public void Method(int x) { }
}";
        var injector = new ConstructorInjection();

        // Act
        var result = injector.Execute(sourceCode, "FakeClass", "Method", new[] { "x" });

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Class 'FakeClass' not found");
    }

    [Fact]
    public void Execute_WithNonExistentMethod_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"public class TestClass
{
    public void RealMethod(int x) { }
}";
        var injector = new ConstructorInjection();

        // Act
        var result = injector.Execute(sourceCode, "TestClass", "FakeMethod", new[] { "x" });

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Method 'FakeMethod' not found");
    }

    [Fact]
    public void Execute_WithNonExistentParameter_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"public class TestClass
{
    public void Method(int x, string y) { }
}";
        var injector = new ConstructorInjection();

        // Act
        var result = injector.Execute(sourceCode, "TestClass", "Method", new[] { "x", "nonexistent" });

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Not all specified parameters found");
    }

    [Fact]
    public void Execute_WithSingleParameter_ShouldWork()
    {
        // Arrange
        var sourceCode = @"public class Service
{
    public void Execute(ILogger logger)
    {
        logger.Log(""test"");
    }
}";
        var injector = new ConstructorInjection();

        // Act
        var result = injector.Execute(sourceCode, "Service", "Execute", new[] { "logger" });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("private readonly ILogger _logger;");
        result.RefactoredCode.Should().Contain("public Service(ILogger logger)");
        result.RefactoredCode.Should().Contain("Execute()");
    }
}
