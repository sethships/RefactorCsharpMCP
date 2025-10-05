using FluentAssertions;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Tests.Refactorings;

public class SafeDeleteTests
{
    [Fact]
    public void Execute_WithUnusedMethod_ShouldDeleteMethod()
    {
        // Arrange
        var sourceCode = @"public class Service
{
    private ILogger _logger;

    public Service(ILogger logger)
    {
        _logger = logger;
    }

    public void Process()
    {
        _logger.Log(""Processing"");
    }

    private void UnusedHelper()
    {
        // This method is never called
    }
}";
        var refactoring = new SafeDelete();

        // Act
        var result = refactoring.Execute(sourceCode, "Service", "UnusedHelper");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().NotContain("UnusedHelper");
        result.RefactoredCode.Should().Contain("public void Process()");
        result.Message.Should().Contain("Safely deleted method 'UnusedHelper'");
    }

    [Fact]
    public void Execute_WithReferencedMethod_ShouldReturnFailure()
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
        var refactoring = new SafeDelete();

        // Act
        var result = refactoring.Execute(sourceCode, "Calculator", "Add");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("is referenced");
        result.ErrorMessage.Should().Contain("line");
    }

    [Fact]
    public void Execute_WithThisQualifiedReference_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"public class Service
{
    private void Initialize()
    {
        // Initialization logic
    }

    public void Start()
    {
        this.Initialize();
    }
}";
        var refactoring = new SafeDelete();

        // Act
        var result = refactoring.Execute(sourceCode, "Service", "Initialize");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("is referenced");
    }

    [Fact]
    public void Execute_WithRecursiveMethod_ShouldDeleteIfNoExternalReferences()
    {
        // Arrange
        var sourceCode = @"public class MathHelper
{
    private int Fibonacci(int n)
    {
        if (n <= 1) return n;
        return Fibonacci(n - 1) + Fibonacci(n - 2);
    }

    public int Calculate(int x)
    {
        return x * 2;
    }
}";
        var refactoring = new SafeDelete();

        // Act
        var result = refactoring.Execute(sourceCode, "MathHelper", "Fibonacci");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().NotContain("Fibonacci");
    }

    [Fact]
    public void Execute_WithEmptySourceCode_ShouldReturnFailure()
    {
        // Arrange
        var refactoring = new SafeDelete();

        // Act
        var result = refactoring.Execute("", "TestClass", "method");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Source code cannot be empty");
    }

    [Fact]
    public void Execute_WithEmptyClassName_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = "public class Test { }";
        var refactoring = new SafeDelete();

        // Act
        var result = refactoring.Execute(sourceCode, "", "method");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Class name cannot be empty");
    }

    [Fact]
    public void Execute_WithEmptyMethodName_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = "public class Test { }";
        var refactoring = new SafeDelete();

        // Act
        var result = refactoring.Execute(sourceCode, "Test", "");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Method name cannot be empty");
    }

    [Fact]
    public void Execute_WithNonExistentClass_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"public class RealClass
{
    public void Method() { }
}";
        var refactoring = new SafeDelete();

        // Act
        var result = refactoring.Execute(sourceCode, "FakeClass", "Method");

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
    public void RealMethod() { }
}";
        var refactoring = new SafeDelete();

        // Act
        var result = refactoring.Execute(sourceCode, "TestClass", "FakeMethod");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Method 'FakeMethod' not found");
    }

    [Fact]
    public void Execute_WithMultipleReferences_ShouldListAllLocations()
    {
        // Arrange
        var sourceCode = @"public class Processor
{
    private void Log(string message)
    {
        Console.WriteLine(message);
    }

    public void Process()
    {
        Log(""Start"");
        DoWork();
        Log(""End"");
    }

    private void DoWork()
    {
        Log(""Working"");
    }
}";
        var refactoring = new SafeDelete();

        // Act
        var result = refactoring.Execute(sourceCode, "Processor", "Log");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("is referenced");
        result.ErrorMessage.Should().Contain("line");
    }

    [Fact]
    public void Execute_WithObjectQualifiedReference_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"public class Service
{
    private Service _inner;

    private void Helper()
    {
        // Helper logic
    }

    public void UseInner()
    {
        _inner.Helper();
    }
}";
        var refactoring = new SafeDelete();

        // Act
        var result = refactoring.Execute(sourceCode, "Service", "Helper");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("is referenced");
    }
}
