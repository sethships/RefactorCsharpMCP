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

    #region Reference Updating Tests

    [Fact]
    public void Execute_UpdatesFieldReferencesInSameClass()
    {
        // Arrange
        var sourceCode = @"public class UserService
{
    private string _city;
    private string _state;

    public void DisplayLocation()
    {
        var location = $""City: {_city}, State: {_state}"";
        System.Console.WriteLine(location);
    }

    public void UpdateCity(string newCity)
    {
        _city = newCity;
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "UserService", "Address", "_city,_state");

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Should contain the new class field
        result.RefactoredCode.Should().Contain("private readonly Address _address = new Address();");

        // Should update field references to use the new class
        result.RefactoredCode.Should().Contain("_address._city");
        result.RefactoredCode.Should().Contain("_address._state");

        // Verify that _city field was removed from UserService
        // (it should only exist in the Address class now)
        var userServiceSection = result.RefactoredCode.Substring(
            result.RefactoredCode.IndexOf("public class UserService"),
            result.RefactoredCode.IndexOf("public class Address") - result.RefactoredCode.IndexOf("public class UserService"));

        userServiceSection.Should().NotContain("private string _city");
        userServiceSection.Should().Contain("private string _state"); // _state should remain

        // Should indicate references were updated
        result.Message.Should().Contain("automatically updated");
    }

    [Fact]
    public void Execute_UpdatesMethodCallsInSameClass()
    {
        // Arrange
        var sourceCode = @"public class UserService
{
    private string _city;
    private string _state;

    public string GetFullAddress()
    {
        return $""{_city}, {_state}"";
    }

    public void DisplayInfo()
    {
        var address = GetFullAddress();
        System.Console.WriteLine(address);
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "UserService", "Address", "_city,_state", "GetFullAddress");

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Should update method call to use the new class
        result.RefactoredCode.Should().Contain("_address.GetFullAddress()");

        // Should not contain direct method call
        result.RefactoredCode.Should().NotContain("var address = GetFullAddress();");

        // Should indicate references were updated
        result.Message.Should().Contain("automatically updated");
    }

    [Fact]
    public void Execute_UpdatesMultipleReferenceTypes()
    {
        // Arrange
        var sourceCode = @"public class UserService
{
    private string _city;
    private string _state;

    public string GetFullAddress()
    {
        return $""{_city}, {_state}"";
    }

    public void UpdateLocation(string city, string state)
    {
        _city = city;
        _state = state;
        var newAddress = GetFullAddress();
        System.Console.WriteLine(newAddress);
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "UserService", "Address", "_city,_state", "GetFullAddress");

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Should update both field and method references
        result.RefactoredCode.Should().Contain("_address._city");
        result.RefactoredCode.Should().Contain("_address._state");
        result.RefactoredCode.Should().Contain("_address.GetFullAddress()");

        // Should indicate references were updated
        result.Message.Should().Contain("automatically updated");
    }

    [Fact]
    public void Execute_PreservesUnrelatedReferences()
    {
        // Arrange
        var sourceCode = @"public class UserService
{
    private string _city;
    private string _name;

    public void DisplayInfo()
    {
        System.Console.WriteLine($""Name: {_name}, City: {_city}"");
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "UserService", "Address", "_city");

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Should update only extracted field reference
        result.RefactoredCode.Should().Contain("_address._city");

        // Should preserve unrelated field reference unchanged
        result.RefactoredCode.Should().Contain("{_name}");
        result.RefactoredCode.Should().NotContain("_address._name");
    }

    [Fact]
    public void Execute_HandlesNoReferences()
    {
        // Arrange
        var sourceCode = @"public class UserService
{
    private string _unusedField;
    private string _name;

    public void DisplayInfo()
    {
        System.Console.WriteLine($""Name: {_name}"");
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "UserService", "UnusedData", "_unusedField");

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Should still succeed even with no references
        result.RefactoredCode.Should().Contain("private readonly UnusedData _unusedData");
        result.RefactoredCode.Should().Contain("public class UnusedData");

        // Should indicate no external references
        result.Message.Should().NotContain("WARNING");
    }

    #endregion
}
