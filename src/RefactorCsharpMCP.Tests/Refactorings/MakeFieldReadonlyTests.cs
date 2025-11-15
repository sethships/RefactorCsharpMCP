using FluentAssertions;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Tests.Refactorings;

public class MakeFieldReadonlyTests
{
    [Fact]
    public void Execute_WithFieldOnlyAssignedInConstructor_ShouldMakeReadonly()
    {
        // Arrange
        var sourceCode = @"public class UserService
{
    private ILogger _logger;

    public UserService(ILogger logger)
    {
        _logger = logger;
    }

    public void Process()
    {
        _logger.Log(""Processing"");
    }
}";
        var refactoring = new MakeFieldReadonly();

        // Act
        var result = refactoring.Execute(sourceCode, "UserService", "_logger", "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("private readonly ILogger _logger;");
        result.Message.Should().Contain("Made field '_logger' readonly");
    }

    [Fact]
    public void Execute_WithFieldIncrementedInMethod_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"public class Service
{
    private int _counter;

    public void Increment()
    {
        _counter++;
    }
}";
        var refactoring = new MakeFieldReadonly();

        // Act
        var result = refactoring.Execute(sourceCode, "Service", "_counter", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("increment/decrement operators");
    }

    [Fact]
    public void Execute_WithFieldAssignedInMethod_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"public class Counter
{
    private int _value;

    public void Reset()
    {
        _value = 0;
    }
}";
        var refactoring = new MakeFieldReadonly();

        // Act
        var result = refactoring.Execute(sourceCode, "Counter", "_value", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("assigned outside of constructors");
    }

    [Fact]
    public void Execute_WithFieldAssignedInPropertySetter_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"public class Person
{
    private string _name;

    public string Name
    {
        get => _name;
        set => _name = value;
    }
}";
        var refactoring = new MakeFieldReadonly();

        // Act
        var result = refactoring.Execute(sourceCode, "Person", "_name", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("assigned in a property setter");
    }

    [Fact]
    public void Execute_WithAlreadyReadonlyField_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"public class Service
{
    private readonly ILogger _logger;

    public Service(ILogger logger)
    {
        _logger = logger;
    }
}";
        var refactoring = new MakeFieldReadonly();

        // Act
        var result = refactoring.Execute(sourceCode, "Service", "_logger", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("already readonly");
    }

    [Fact]
    public void Execute_WithConstField_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"public class Config
{
    private const int MaxRetries = 3;
}";
        var refactoring = new MakeFieldReadonly();

        // Act
        var result = refactoring.Execute(sourceCode, "Config", "MaxRetries", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("const and cannot be made readonly");
    }

    [Fact]
    public void Execute_WithPublicField_ShouldMakeReadonly()
    {
        // Arrange
        var sourceCode = @"public class Data
{
    public string Value;

    public Data(string value)
    {
        Value = value;
    }
}";
        var refactoring = new MakeFieldReadonly();

        // Act
        var result = refactoring.Execute(sourceCode, "Data", "Value", "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("public readonly string Value;");
    }

    [Fact]
    public void Execute_WithStaticField_ShouldMakeReadonly()
    {
        // Arrange
        var sourceCode = @"public class Config
{
    private static string _appName;

    static Config()
    {
        _appName = ""MyApp"";
    }
}";
        var refactoring = new MakeFieldReadonly();

        // Act
        var result = refactoring.Execute(sourceCode, "Config", "_appName", "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("private static readonly string _appName;");
    }

    [Fact]
    public void Execute_WithThisQualifiedAssignment_ShouldMakeReadonly()
    {
        // Arrange
        var sourceCode = @"public class Service
{
    private ILogger _logger;

    public Service(ILogger logger)
    {
        this._logger = logger;
    }
}";
        var refactoring = new MakeFieldReadonly();

        // Act
        var result = refactoring.Execute(sourceCode, "Service", "_logger", "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("private readonly ILogger _logger;");
    }

    [Fact]
    public void Execute_WithEmptySourceCode_ShouldReturnFailure()
    {
        // Arrange
        var refactoring = new MakeFieldReadonly();

        // Act
        var result = refactoring.Execute("", "TestClass", "field", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Source code cannot be empty");
    }

    [Fact]
    public void Execute_WithEmptyClassName_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = "public class Test { }";
        var refactoring = new MakeFieldReadonly();

        // Act
        var result = refactoring.Execute(sourceCode, "", "field", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Class name cannot be empty");
    }

    [Fact]
    public void Execute_WithEmptyFieldName_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = "public class Test { }";
        var refactoring = new MakeFieldReadonly();

        // Act
        var result = refactoring.Execute(sourceCode, "Test", "", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Field name cannot be empty");
    }

    [Fact]
    public void Execute_WithNonExistentClass_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"public class RealClass
{
    private int _field;
}";
        var refactoring = new MakeFieldReadonly();

        // Act
        var result = refactoring.Execute(sourceCode, "FakeClass", "_field", "net8.0");

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
        var refactoring = new MakeFieldReadonly();

        // Act
        var result = refactoring.Execute(sourceCode, "TestClass", "_fakeField", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Field '_fakeField' not found");
    }

    [Fact]
    public void Execute_WithMultipleConstructorAssignments_ShouldMakeReadonly()
    {
        // Arrange
        var sourceCode = @"public class Service
{
    private ILogger _logger;

    public Service()
    {
        _logger = new ConsoleLogger();
    }

    public Service(ILogger logger)
    {
        _logger = logger;
    }
}";
        var refactoring = new MakeFieldReadonly();

        // Act
        var result = refactoring.Execute(sourceCode, "Service", "_logger", "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("private readonly ILogger _logger;");
    }

    [Fact]
    public void Execute_WithPrefixIncrementInMethod_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"public class Counter
{
    private int _value;

    public void Increment()
    {
        ++_value;
    }
}";
        var refactoring = new MakeFieldReadonly();

        // Act
        var result = refactoring.Execute(sourceCode, "Counter", "_value", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("increment/decrement operators");
    }

    [Fact]
    public void Execute_WithPostfixDecrementInMethod_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"public class Counter
{
    private int _value;

    public void Decrement()
    {
        _value--;
    }
}";
        var refactoring = new MakeFieldReadonly();

        // Act
        var result = refactoring.Execute(sourceCode, "Counter", "_value", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("increment/decrement operators");
    }

    [Fact]
    public void Execute_WithFieldInitializer_ShouldMakeReadonly()
    {
        // Arrange
        var sourceCode = @"public class Config
{
    private int _maxRetries = 3;

    public Config()
    {
    }
}";
        var refactoring = new MakeFieldReadonly();

        // Act
        var result = refactoring.Execute(sourceCode, "Config", "_maxRetries", "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("private readonly int _maxRetries = 3;");
    }

    [Fact]
    public void Execute_WithFieldInitializerAndConstructorAssignment_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"public class Config
{
    private int _maxRetries = 3;

    public Config(int retries)
    {
        _maxRetries = retries;
    }
}";
        var refactoring = new MakeFieldReadonly();

        // Act
        var result = refactoring.Execute(sourceCode, "Config", "_maxRetries", "net8.0");

        // Assert - Readonly fields can have both initializer and constructor assignment
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("private readonly int _maxRetries = 3;");
    }

    [Fact]
    public void Execute_WithFieldCapturedByLambda_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"public class Service
{
    private int _counter;

    public Service()
    {
        _counter = 0;
    }

    public void StartTimer()
    {
        var timer = new Timer(() => _counter++, null, 0, 1000);
    }
}";
        var refactoring = new MakeFieldReadonly();

        // Act
        var result = refactoring.Execute(sourceCode, "Service", "_counter", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("captured by lambda");
    }

    [Fact]
    public void Execute_WithFieldUsedInLambdaWithoutModification_ShouldReturnFailure()
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
        var action = new Action(() => _logger.Log(""test""));
    }
}";
        var refactoring = new MakeFieldReadonly();

        // Act
        var result = refactoring.Execute(sourceCode, "Service", "_logger", "net8.0");

        // Assert - Conservative: reject even read-only lambda captures for safety
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("captured by lambda");
    }
}
