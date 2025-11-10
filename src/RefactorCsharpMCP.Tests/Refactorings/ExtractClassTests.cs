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
        result.RefactoredCode.Should().Contain("internal class LoggingContext");
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
        result.RefactoredCode.Should().Contain("internal class Telemetry");
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
        result.RefactoredCode.Should().Contain("internal class Data");
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
        result.RefactoredCode.Should().Contain("internal class Logger");
        result.RefactoredCode.Should().Contain("private ILogger _logger;");
        result.RefactoredCode.Should().Contain("internal void Log(string msg)");
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
    public void Execute_WithEmptyFieldNamesAndMethods_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = "public class Test { }";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "Test", "NewClass", "", "");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("At least one field, method, or nested type name must be specified");
    }

    [Fact]
    public void Execute_WithNullFieldNamesAndMethods_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = "public class Test { }";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "Test", "NewClass", null, null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("At least one field, method, or nested type name must be specified");
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
        result.RefactoredCode.Should().Contain("internal class LoggerContext");
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
        result.RefactoredCode.Should().Contain("internal class LoggerContext");
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

        // Verify that both _city and _state fields were removed from UserService
        // (they should only exist in the Address class now)
        var userServiceSection = result.RefactoredCode.Substring(
            result.RefactoredCode.IndexOf("public class UserService"),
            result.RefactoredCode.IndexOf("internal class Address") - result.RefactoredCode.IndexOf("public class UserService"));

        userServiceSection.Should().NotContain("private string _city");
        userServiceSection.Should().NotContain("private string _state"); // Both fields extracted

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
        result.RefactoredCode.Should().Contain("internal class UnusedData");

        // Should indicate no external references
        result.Message.Should().NotContain("WARNING");
    }

    #endregion

    #region Semantic Analysis Edge Cases

    [Fact]
    public void Execute_WithLocalVariableShadowing_DoesNotTransformLocal()
    {
        // Arrange
        var sourceCode = @"public class UserService
{
    private string _city;

    public void ProcessLocation()
    {
        // Local variable with same name as field
        string _city = ""LocalCity"";
        System.Console.WriteLine(_city); // Should NOT be transformed
    }

    public void UseField()
    {
        System.Console.WriteLine(_city); // Should be transformed to _address._city
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "UserService", "Address", "_city");

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Should contain the extracted class with field
        result.RefactoredCode.Should().Contain("internal class Address");
        result.RefactoredCode.Should().Contain("private readonly Address _address");

        // Field reference in UseField should be transformed
        var useFieldMethod = result.RefactoredCode.Substring(
            result.RefactoredCode.IndexOf("public void UseField"),
            result.RefactoredCode.IndexOf("}", result.RefactoredCode.IndexOf("public void UseField")) -
            result.RefactoredCode.IndexOf("public void UseField"));
        useFieldMethod.Should().Contain("_address._city");

        // Local variable in ProcessLocation should NOT be transformed
        var processMethod = result.RefactoredCode.Substring(
            result.RefactoredCode.IndexOf("public void ProcessLocation"),
            result.RefactoredCode.IndexOf("}", result.RefactoredCode.IndexOf("public void ProcessLocation")) -
            result.RefactoredCode.IndexOf("public void ProcessLocation"));

        // Local variable declaration should remain unchanged
        processMethod.Should().Contain("string _city = \"LocalCity\"");

        // Local variable usage should NOT have _address prefix
        processMethod.Should().Contain("System.Console.WriteLine(_city)");
        processMethod.Should().NotContain("_address._city");
    }

    [Fact]
    public void Execute_WithParameterShadowing_DoesNotTransformParameter()
    {
        // Arrange
        var sourceCode = @"public class UserService
{
    private string _city;

    public void ProcessLocation(string _city)
    {
        // Parameter with same name as field
        System.Console.WriteLine(_city); // Should NOT be transformed (refers to parameter)
    }

    public void UseField()
    {
        System.Console.WriteLine(_city); // Should be transformed to _address._city
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "UserService", "Address", "_city");

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Should contain the extracted class
        result.RefactoredCode.Should().Contain("internal class Address");
        result.RefactoredCode.Should().Contain("private readonly Address _address");

        // Field reference in UseField should be transformed
        var useFieldMethod = result.RefactoredCode.Substring(
            result.RefactoredCode.IndexOf("public void UseField"),
            result.RefactoredCode.IndexOf("}", result.RefactoredCode.IndexOf("public void UseField")) -
            result.RefactoredCode.IndexOf("public void UseField"));
        useFieldMethod.Should().Contain("_address._city");

        // Parameter usage in ProcessLocation should NOT be transformed
        var processMethod = result.RefactoredCode.Substring(
            result.RefactoredCode.IndexOf("public void ProcessLocation"),
            result.RefactoredCode.IndexOf("}", result.RefactoredCode.IndexOf("public void ProcessLocation")) -
            result.RefactoredCode.IndexOf("public void ProcessLocation"));

        // Parameter usage should remain unchanged
        processMethod.Should().Contain("System.Console.WriteLine(_city)");
        processMethod.Should().NotContain("_address._city");
    }

    [Fact]
    public void Execute_WithPartialClass_UpdatesReferencesInAllParts()
    {
        // Arrange
        // Simulating a partial class scenario (both parts in same file for test simplicity)
        var sourceCode = @"public partial class UserService
{
    private string _city;
    private string _state;

    public void DisplayLocation()
    {
        System.Console.WriteLine($""Location: {_city}, {_state}"");
    }
}

public partial class UserService
{
    public void UpdateLocation(string newCity)
    {
        _city = newCity; // Should be transformed even though in different partial class part
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "UserService", "Address", "_city");

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Should contain the extracted class
        result.RefactoredCode.Should().Contain("internal class Address");

        // References in both partial class parts should be transformed
        result.RefactoredCode.Should().Contain("_address._city");

        // The transformation should appear in both method contexts
        var displayLocationMethod = result.RefactoredCode.Substring(
            result.RefactoredCode.IndexOf("public void DisplayLocation"),
            result.RefactoredCode.IndexOf("}", result.RefactoredCode.IndexOf("public void DisplayLocation")) -
            result.RefactoredCode.IndexOf("public void DisplayLocation"));
        displayLocationMethod.Should().Contain("_address._city");

        var updateLocationMethod = result.RefactoredCode.Substring(
            result.RefactoredCode.IndexOf("public void UpdateLocation"),
            result.RefactoredCode.IndexOf("}", result.RefactoredCode.IndexOf("public void UpdateLocation")) -
            result.RefactoredCode.IndexOf("public void UpdateLocation"));
        updateLocationMethod.Should().Contain("_address._city");

        // Should indicate references were updated
        result.Message.Should().Contain("automatically updated");
    }

    [Fact]
    public void Execute_WithUnrelatedClassSameMemberName_DoesNotTransformUnrelatedClass()
    {
        // Arrange
        var sourceCode = @"public class UserService
{
    private string _city;

    public void ProcessLocation()
    {
        System.Console.WriteLine(_city); // Should be transformed to _address._city
    }
}

public class OtherService
{
    private string _city; // Same field name but different class

    public void ProcessOther()
    {
        System.Console.WriteLine(_city); // Should NOT be transformed (different class)
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "UserService", "Address", "_city");

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Should contain both original classes and the new extracted class
        result.RefactoredCode.Should().Contain("public class UserService");
        result.RefactoredCode.Should().Contain("public class OtherService");
        result.RefactoredCode.Should().Contain("internal class Address");

        // UserService should have transformed reference
        var userServiceSection = result.RefactoredCode.Substring(
            result.RefactoredCode.IndexOf("public class UserService"),
            result.RefactoredCode.IndexOf("public class OtherService") -
            result.RefactoredCode.IndexOf("public class UserService"));
        userServiceSection.Should().Contain("_address._city");

        // OtherService should NOT have transformed reference
        var otherServiceSection = result.RefactoredCode.Substring(
            result.RefactoredCode.IndexOf("public class OtherService"),
            result.RefactoredCode.IndexOf("internal class Address") -
            result.RefactoredCode.IndexOf("public class OtherService"));

        // OtherService should still have its own _city field
        otherServiceSection.Should().Contain("private string _city");

        // OtherService method should still use plain _city (not transformed)
        otherServiceSection.Should().Contain("System.Console.WriteLine(_city)");
        otherServiceSection.Should().NotContain("_address");
    }

    [Fact]
    public void Execute_WithQualifiedMemberAccess_TransformsCorrectly()
    {
        // Arrange
        var sourceCode = @"public class UserService
{
    private string _city;
    private string _state;

    public void ProcessLocation()
    {
        // Both qualified and unqualified access
        var location = $""{this._city}, {_state}"";
        System.Console.WriteLine(location);
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "UserService", "Address", "_city");

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Should contain extracted class
        result.RefactoredCode.Should().Contain("internal class Address");
        result.RefactoredCode.Should().Contain("private readonly Address _address");

        // Qualified access should be transformed to: this._address._city
        // Note: The 'this.' prefix might be preserved depending on implementation
        result.RefactoredCode.Should().Contain("_address._city");

        // Unqualified _state should remain unchanged
        result.RefactoredCode.Should().Contain("{_state}");

        // Should indicate references were updated
        result.Message.Should().Contain("automatically updated");
    }

    [Fact]
    public void Execute_PreservesCommentsAndFormatting()
    {
        // Arrange
        var sourceCode = @"public class UserService
{
    // User's address information
    private string _city;
    private string _state; // State abbreviation

    public void DisplayLocation()
    {
        // Display the full location
        System.Console.WriteLine($""{_city}, {_state}"");
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "UserService", "Address", "_city,_state");

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Comments should be preserved in the extracted class
        result.RefactoredCode.Should().Contain("// User's address information");
        result.RefactoredCode.Should().Contain("// State abbreviation");

        // Method comment should be preserved
        result.RefactoredCode.Should().Contain("// Display the full location");

        // Should contain the extracted class
        result.RefactoredCode.Should().Contain("internal class Address");
    }

    #endregion

    #region Method-Only Extraction Tests (Service Class Pattern)

    [Fact]
    public void Execute_WithMethodsOnly_NoFields_ShouldExtractSuccessfully()
    {
        // Arrange
        var sourceCode = @"public class InlineMethod
{
    private string _data;

    private bool IsSimpleType(string typeName)
    {
        return typeName == ""int"" || typeName == ""string"" || typeName == ""bool"";
    }

    private bool IsRecursive(string methodName)
    {
        return methodName.Contains(""Recursive"");
    }

    public void Process()
    {
        var simple = IsSimpleType(""int"");
        var recursive = IsRecursive(""TestMethod"");
    }
}";
        var refactoring = new ExtractClass();

        // Act - Extract only methods, no fields
        var result = refactoring.Execute(sourceCode, "InlineMethod", "TypeChecker", null, "IsSimpleType,IsRecursive");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("internal class TypeChecker");
        result.RefactoredCode.Should().Contain("internal bool IsSimpleType(string typeName)");
        result.RefactoredCode.Should().Contain("internal bool IsRecursive(string methodName)");
        result.RefactoredCode.Should().Contain("private readonly TypeChecker _typeChecker = new TypeChecker();");
        result.RefactoredCode.Should().Contain("_typeChecker.IsSimpleType(");
        result.RefactoredCode.Should().Contain("_typeChecker.IsRecursive(");

        // Verify the original field is still in InlineMethod
        result.RefactoredCode.Should().Contain("private string _data;");
    }

    [Fact]
    public void Execute_WithMultipleMethodsOnly_ShouldExtractAllMethods()
    {
        // Arrange
        var sourceCode = @"public class UserService
{
    private string _userName;

    private void ValidateUser(string user)
    {
        // Validation logic
    }

    private string FormatUserName(string name)
    {
        return name.ToUpper();
    }

    private bool IsAdmin(string user)
    {
        return user == ""admin"";
    }

    public void ProcessUser()
    {
        ValidateUser(_userName);
        var formatted = FormatUserName(_userName);
        var admin = IsAdmin(_userName);
    }
}";
        var refactoring = new ExtractClass();

        // Act - Extract three methods without any fields
        var result = refactoring.Execute(sourceCode, "UserService", "UserValidator", null, "ValidateUser,FormatUserName,IsAdmin");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("internal class UserValidator");
        result.RefactoredCode.Should().Contain("internal void ValidateUser(string user)");
        result.RefactoredCode.Should().Contain("internal string FormatUserName(string name)");
        result.RefactoredCode.Should().Contain("internal bool IsAdmin(string user)");
        result.RefactoredCode.Should().Contain("private readonly UserValidator _userValidator = new UserValidator();");

        // Verify method calls are updated
        result.RefactoredCode.Should().Contain("_userValidator.ValidateUser(");
        result.RefactoredCode.Should().Contain("_userValidator.FormatUserName(");
        result.RefactoredCode.Should().Contain("_userValidator.IsAdmin(");

        // Original field should remain in UserService
        result.RefactoredCode.Should().Contain("private string _userName;");
    }

    [Fact]
    public void Execute_ServicePattern_RealWorldScenario()
    {
        // Arrange - Simulating the MethodResolver use case from Issue #99
        var sourceCode = @"using System;

public class InlineMethod
{
    private ILogger? _logger;
    private string _sourceCode;

    private MethodInfo? ExtractMethodInfo(string code)
    {
        _logger?.Log(""Extracting method info"");
        // Complex extraction logic
        return new MethodInfo();
    }

    private ValidationResult CanMethodBeInlined(MethodInfo method)
    {
        _logger?.Log(""Validating method"");
        // Validation logic
        return new ValidationResult { IsValid = true };
    }

    private bool IsRecursive(MethodInfo method)
    {
        // Recursion check logic
        return false;
    }

    private bool IsSimpleType(string typeName)
    {
        return typeName == ""int"" || typeName == ""string"";
    }

    public void InlineTheMethod()
    {
        var methodInfo = ExtractMethodInfo(_sourceCode);
        if (methodInfo != null)
        {
            var validation = CanMethodBeInlined(methodInfo);
            var recursive = IsRecursive(methodInfo);
            var simple = IsSimpleType(""int"");
        }
    }
}

public class MethodInfo { }
public class ValidationResult { public bool IsValid { get; set; } }
public interface ILogger { void Log(string msg); }";
        var refactoring = new ExtractClass();

        // Act - Extract all four methods into MethodResolver service class
        var result = refactoring.Execute(
            sourceCode,
            "InlineMethod",
            "MethodResolver",
            null, // No fields to extract
            "ExtractMethodInfo,CanMethodBeInlined,IsRecursive,IsSimpleType");

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify service class created
        result.RefactoredCode.Should().Contain("internal class MethodResolver");

        // Verify all methods extracted
        result.RefactoredCode.Should().Contain("internal MethodInfo? ExtractMethodInfo(string code)");
        result.RefactoredCode.Should().Contain("internal ValidationResult CanMethodBeInlined(MethodInfo method)");
        result.RefactoredCode.Should().Contain("internal bool IsRecursive(MethodInfo method)");
        result.RefactoredCode.Should().Contain("internal bool IsSimpleType(string typeName)");

        // Verify composition field created
        result.RefactoredCode.Should().Contain("private readonly MethodResolver _methodResolver = new MethodResolver();");

        // Verify method calls updated in original class
        result.RefactoredCode.Should().Contain("_methodResolver.ExtractMethodInfo(");
        result.RefactoredCode.Should().Contain("_methodResolver.CanMethodBeInlined(");
        result.RefactoredCode.Should().Contain("_methodResolver.IsRecursive(");
        result.RefactoredCode.Should().Contain("_methodResolver.IsSimpleType(");

        // Verify original fields remain in InlineMethod
        var inlineMethodStart = result.RefactoredCode.IndexOf("public class InlineMethod");
        var methodResolverStart = result.RefactoredCode.IndexOf("internal class MethodResolver");
        inlineMethodStart.Should().BeGreaterThan(-1);
        methodResolverStart.Should().BeGreaterThan(inlineMethodStart);

        var inlineMethodSection = result.RefactoredCode.Substring(
            inlineMethodStart,
            methodResolverStart - inlineMethodStart);
        inlineMethodSection.Should().Contain("private ILogger? _logger;");
        inlineMethodSection.Should().Contain("private string _sourceCode;");
    }

    [Fact]
    public void Execute_WithMethodsOnlyAndFileScopedNamespace_ShouldExtract()
    {
        // Arrange
        var sourceCode = @"namespace MyApp;

public class Service
{
    private void HelperMethod()
    {
        // Logic
    }

    public void MainMethod()
    {
        HelperMethod();
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "Service", "Helper", null, "HelperMethod");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("internal class Helper");
        result.RefactoredCode.Should().Contain("internal void HelperMethod()");
        result.RefactoredCode.Should().Contain("_helper.HelperMethod()");
    }

    #endregion

    #region Nested Type Extraction Tests

    [Fact]
    public void ExtractClass_WithNestedClass_ExtractsSuccessfully()
    {
        // Arrange
        var sourceCode = @"public class Container
{
    private string _data;

    public class NestedConfig
    {
        public int MaxSize { get; set; }
        public bool Enabled { get; set; }
    }

    public void Process(NestedConfig config)
    {
        if (config.Enabled)
        {
            // Process logic
        }
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "Container", "Configuration", null, null, "NestedConfig");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("internal class Configuration");
        result.RefactoredCode.Should().Contain("public class NestedConfig"); // Nested type stays public (not part of bug fixes)
        result.RefactoredCode.Should().Contain("private readonly Configuration _configuration = new Configuration();");
        result.RefactoredCode.Should().NotContain("Container.NestedConfig"); // Should not remain in original class
    }

    [Fact]
    public void ExtractClass_WithNestedStruct_ExtractsSuccessfully()
    {
        // Arrange
        var sourceCode = @"public class DataProcessor
{
    private int _counter;

    public struct Point
    {
        public int X;
        public int Y;
    }

    public void PlotPoint(Point p)
    {
        // Plotting logic
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "DataProcessor", "Geometry", null, null, "Point");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("internal class Geometry");
        result.RefactoredCode.Should().Contain("public struct Point");
        result.RefactoredCode.Should().NotContain("DataProcessor.Point");
    }

    [Fact]
    public void ExtractClass_WithNestedEnum_ExtractsSuccessfully()
    {
        // Arrange
        var sourceCode = @"public class Logger
{
    private string _logPath;

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    public void Log(string message, LogLevel level)
    {
        // Logging logic
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "Logger", "LogConfiguration", null, null, "LogLevel");

        // Assert
        if (!result.IsSuccess)
        {
            Console.WriteLine($"ERROR: {result.ErrorMessage}");
        }
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("internal class LogConfiguration");
        result.RefactoredCode.Should().Contain("public enum LogLevel");
        result.RefactoredCode.Should().Contain("Debug");
        result.RefactoredCode.Should().Contain("Error");
    }

    [Fact]
    public void ExtractClass_WithNestedRecord_ExtractsSuccessfully()
    {
        // Arrange
        var sourceCode = @"public class UserManager
{
    private List<User> _users;

    public record UserInfo(string Name, int Age);

    public UserInfo GetUserInfo(int userId)
    {
        // Get user info logic
        return new UserInfo(""John"", 30);
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "UserManager", "UserData", null, null, "UserInfo");

        // Assert
        result.IsSuccess.Should().BeTrue($"Refactoring should succeed, but failed with: {result.ErrorMessage}");
        result.RefactoredCode.Should().Contain("internal class UserData");
        result.RefactoredCode.Should().Contain("public record UserInfo");
    }

    [Fact]
    public void ExtractClass_WithMultipleNestedTypes_ExtractsAllSuccessfully()
    {
        // Arrange
        var sourceCode = @"public class GameEngine
{
    private int _score;

    public enum GameState
    {
        Running,
        Paused,
        GameOver
    }

    public struct Vector2D
    {
        public float X;
        public float Y;
    }

    public class Player
    {
        public string Name { get; set; }
        public int Health { get; set; }
    }

    public void Update(GameState state, Vector2D position)
    {
        // Update logic
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "GameEngine", "GameTypes", null, null, "GameState,Vector2D,Player");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("internal class GameTypes");
        result.RefactoredCode.Should().Contain("public enum GameState");
        result.RefactoredCode.Should().Contain("public struct Vector2D");
        result.RefactoredCode.Should().Contain("public class Player"); // Nested type stays public (not part of bug fixes)
        result.Message.Should().Contain("3 nested type(s)");
    }

    [Fact]
    public void ExtractClass_NestedTypeReferencesUpdated_InExtractedMethods()
    {
        // Arrange
        var sourceCode = @"public class SymbolResolutionHelper
{
    private string _data;

    public class SymbolResolutionResult
    {
        public bool Success { get; init; }
        public string ErrorMessage { get; init; }
    }

    public SymbolResolutionResult GetSymbolAtPosition(int line, int column)
    {
        return new SymbolResolutionResult
        {
            Success = true,
            ErrorMessage = null
        };
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "SymbolResolutionHelper", "PositionBasedResolver", null, "GetSymbolAtPosition", "SymbolResolutionResult");

        // Assert
        result.IsSuccess.Should().BeTrue($"Refactoring should succeed, but failed with: {result.ErrorMessage}");
        result.RefactoredCode.Should().Contain("internal class PositionBasedResolver");
        result.RefactoredCode.Should().Contain("public class SymbolResolutionResult"); // Nested type stays public (not part of bug fixes)
        result.RefactoredCode.Should().Contain("internal SymbolResolutionResult GetSymbolAtPosition");
        // The extracted method should use SymbolResolutionResult directly (it's in the same class now)
        result.RefactoredCode.Should().Contain("return new SymbolResolutionResult");
    }

    [Fact]
    public void ExtractClass_WithNonExistentNestedType_ShouldFail()
    {
        // Arrange
        var sourceCode = @"public class Container
{
    private string _data;

    public class RealNestedClass
    {
        public int Value { get; set; }
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "Container", "NewClass", null, null, "FakeNestedType");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Nested type 'FakeNestedType' not found");
    }

    [Fact]
    public void ExtractClass_WithFieldMethodAndNestedType_ExtractsAll()
    {
        // Arrange
        var sourceCode = @"public class Service
{
    private ILogger _logger;

    public enum Priority
    {
        Low,
        Medium,
        High
    }

    public void ProcessTask(Priority priority)
    {
        _logger.Log($""Processing with priority: {priority}"");
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "Service", "TaskProcessor", "_logger", "ProcessTask", "Priority");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("internal class TaskProcessor");
        result.RefactoredCode.Should().Contain("private ILogger _logger;");
        result.RefactoredCode.Should().Contain("internal void ProcessTask");
        result.RefactoredCode.Should().Contain("public enum Priority");
        result.Message.Should().Contain("1 field(s)");
        result.Message.Should().Contain("1 method(s)");
        result.Message.Should().Contain("1 nested type(s)");
    }

    [Fact]
    public void ExtractClass_WithNestedInterface_ExtractsSuccessfully()
    {
        // Arrange
        var sourceCode = @"public class Container
{
    private string _data;

    public interface IProcessor
    {
        void Process();
        string GetResult();
    }

    public void Execute(IProcessor processor)
    {
        processor.Process();
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "Container", "Interfaces", null, null, "IProcessor");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("internal class Interfaces");
        result.RefactoredCode.Should().Contain("public interface IProcessor");
        result.RefactoredCode.Should().Contain("void Process();");
        result.RefactoredCode.Should().Contain("string GetResult();");
    }

    [Fact]
    public void ExtractClass_WithNestedTypeAsFieldType_PreservesFieldDeclaration()
    {
        // Arrange
        var sourceCode = @"public class Container
{
    public class Config
    {
        public string Setting { get; set; }
    }

    private Config _config;

    public void Initialize()
    {
        _config = new Config();
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "Container", "Configuration", null, null, "Config");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("internal class Configuration");
        result.RefactoredCode.Should().Contain("internal class Config");
        // Critical: Field type reference should remain as 'Config', not '_configuration.Config'
        result.RefactoredCode.Should().Contain("private Config _config");
        result.RefactoredCode.Should().NotContain("private _configuration.Config");
        // Object creation should also use 'Config' directly
        result.RefactoredCode.Should().Contain("new Config()");
        result.RefactoredCode.Should().NotContain("new _configuration.Config()");
    }

    #endregion

    #region Bug Fix Validation Tests (Issue #112)

    [Fact]
    public void Execute_ExtractedClassHasInternalVisibility_Bug4()
    {
        // Arrange - Bug #4: Extracted classes should be internal, not public
        var sourceCode = @"public class UserService
{
    private ILogger _logger;
    private IDatabase _database;

    public void Process()
    {
        _logger.Log(""Processing"");
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "UserService", "LoggingContext", "_logger");

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Bug #4: Extracted class must have internal visibility for encapsulation
        result.RefactoredCode.Should().Contain("internal class LoggingContext");
        result.RefactoredCode.Should().NotContain("public class LoggingContext");

        // Source class remains public
        result.RefactoredCode.Should().Contain("public class UserService");
    }

    [Fact]
    public void Execute_ExtractedMethodsHaveInternalAccessibility_Bug3()
    {
        // Arrange - Bug #3: Extracted methods need internal accessibility to be callable via composition
        var sourceCode = @"public class DataProcessor
{
    private string _data;

    private void ValidateData()
    {
        // Validation logic
    }

    private string TransformData(string input)
    {
        return input.ToUpper();
    }

    public void Process()
    {
        ValidateData();
        var result = TransformData(_data);
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "DataProcessor", "DataValidator", null, "ValidateData,TransformData");

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Bug #3: Extracted methods must be internal to allow composition field access
        result.RefactoredCode.Should().Contain("internal void ValidateData()");
        result.RefactoredCode.Should().Contain("internal string TransformData(string input)");

        // Methods should not remain private (would prevent composition field access)
        result.RefactoredCode.Should().NotContain("private void ValidateData()");
        result.RefactoredCode.Should().NotContain("private string TransformData(string input)");

        // Verify composition field can call the methods
        result.RefactoredCode.Should().Contain("_dataValidator.ValidateData()");
        result.RefactoredCode.Should().Contain("_dataValidator.TransformData(");
    }

    [Fact]
    public void Execute_ExtractedMethodsRemovedFromSourceClass_Bug1()
    {
        // Arrange - Bug #1: Methods must be removed from source class after extraction
        var sourceCode = @"public class Calculator
{
    private int _value;

    private int Add(int a, int b)
    {
        return a + b;
    }

    private int Multiply(int a, int b)
    {
        return a * b;
    }

    public int Calculate()
    {
        var sum = Add(5, 3);
        var product = Multiply(sum, 2);
        return product;
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "Calculator", "MathOperations", null, "Add,Multiply");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().NotBeNullOrEmpty();

        // Bug #1: Methods should be removed from source class (stale tree reference bug)
        var sourceClassStart = result.RefactoredCode.IndexOf("public class Calculator");
        var extractedClassStart = result.RefactoredCode.IndexOf("internal class MathOperations");

        sourceClassStart.Should().BeGreaterThanOrEqualTo(0, "source class should exist");
        extractedClassStart.Should().BeGreaterThan(sourceClassStart, "extracted class should come after source class");

        var sourceClassSection = result.RefactoredCode.Substring(
            sourceClassStart,
            extractedClassStart - sourceClassStart);

        // Extracted methods should NOT appear in source class
        sourceClassSection.Should().NotContain("int Add(int a, int b)");
        sourceClassSection.Should().NotContain("int Multiply(int a, int b)");

        // But should appear in extracted class
        var extractedSection = result.RefactoredCode.Substring(extractedClassStart);
        extractedSection.Should().Contain("internal int Add(int a, int b)");
        extractedSection.Should().Contain("internal int Multiply(int a, int b)");

        // Source class should delegate to composition field
        sourceClassSection.Should().Contain("_mathOperations.Add(");
        sourceClassSection.Should().Contain("_mathOperations.Multiply(");
    }

    [Fact]
    public void Execute_AllMethodInvocationsUpdatedToDelegate_Bug2()
    {
        // Arrange - Bug #2: Direct method invocations must be updated to delegate through composition field
        var sourceCode = @"public class Validator
{
    private string _input;

    private bool IsValid(string value)
    {
        return !string.IsNullOrEmpty(value);
    }

    private bool IsNumeric(string value)
    {
        return int.TryParse(value, out _);
    }

    public void Validate()
    {
        // Direct method invocations - these need to be updated
        if (IsValid(_input) && IsNumeric(_input))
        {
            var valid = IsValid(""test"");
        }
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "Validator", "ValidationRules", null, "IsValid,IsNumeric");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().NotBeNullOrEmpty();

        // Bug #2: All direct invocations should be updated to delegate
        var sourceClassStart = result.RefactoredCode.IndexOf("public class Validator");
        var extractedClassStart = result.RefactoredCode.IndexOf("internal class ValidationRules");

        sourceClassStart.Should().BeGreaterThanOrEqualTo(0, "source class should exist");
        extractedClassStart.Should().BeGreaterThan(sourceClassStart, "extracted class should come after source class");

        var sourceClassSection = result.RefactoredCode.Substring(
            sourceClassStart,
            extractedClassStart - sourceClassStart);

        // Should delegate through composition field
        sourceClassSection.Should().Contain("_validationRules.IsValid(_input)");
        sourceClassSection.Should().Contain("_validationRules.IsNumeric(_input)");
        sourceClassSection.Should().Contain("_validationRules.IsValid(\"test\")");

        // Should NOT have direct invocations
        sourceClassSection.Should().NotContain("if (IsValid(_input)");
        sourceClassSection.Should().NotContain("&& IsNumeric(_input)");
        sourceClassSection.Should().NotContain("var valid = IsValid(\"test\")");
    }

    [Fact]
    public void Execute_MultiMethodExtraction_GeneratesValidCode()
    {
        // Arrange - Integration test: Extract multiple methods and verify generated code structure
        var sourceCode = @"public class Service
{
    private string _data;

    private void Initialize()
    {
        _data = ""initialized"";
    }

    private void Cleanup()
    {
        _data = null;
    }

    private bool Validate()
    {
        return _data != null;
    }

    public void Run()
    {
        Initialize();
        if (Validate())
        {
            // Process
        }
        Cleanup();
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "Service", "Lifecycle", null, "Initialize,Cleanup,Validate");

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify extracted class structure
        result.RefactoredCode.Should().Contain("internal class Lifecycle");
        result.RefactoredCode.Should().Contain("internal void Initialize()");
        result.RefactoredCode.Should().Contain("internal void Cleanup()");
        result.RefactoredCode.Should().Contain("internal bool Validate()");

        // Verify composition field
        result.RefactoredCode.Should().Contain("private readonly Lifecycle _lifecycle = new Lifecycle();");

        // Verify all method calls delegated
        result.RefactoredCode.Should().Contain("_lifecycle.Initialize()");
        result.RefactoredCode.Should().Contain("_lifecycle.Validate()");
        result.RefactoredCode.Should().Contain("_lifecycle.Cleanup()");

        // Verify methods removed from source
        var sourceClassStart = result.RefactoredCode.IndexOf("public class Service");
        var extractedClassStart = result.RefactoredCode.IndexOf("internal class Lifecycle");

        sourceClassStart.Should().BeGreaterThanOrEqualTo(0, "source class should exist");
        extractedClassStart.Should().BeGreaterThan(sourceClassStart, "extracted class should come after source class");

        var sourceClassSection = result.RefactoredCode.Substring(
            sourceClassStart,
            extractedClassStart - sourceClassStart);

        sourceClassSection.Should().NotContain("void Initialize()");
        sourceClassSection.Should().NotContain("void Cleanup()");
        sourceClassSection.Should().NotContain("bool Validate()");
    }

    [Fact]
    public void Execute_DogfoodingScenario_ExtractPositionBasedResolver()
    {
        // Arrange - Issue #91 dogfooding scenario: Extract PositionBasedResolver from SymbolResolutionHelper
        var sourceCode = @"public class SymbolResolutionHelper
{
    private string _data;

    public SymbolResolutionResult GetSymbolAtPosition(int line, int column)
    {
        return new SymbolResolutionResult
        {
            Success = true,
            ErrorMessage = null
        };
    }

    public class SymbolResolutionResult
    {
        public bool Success { get; init; }
        public string ErrorMessage { get; init; }
    }
}";
        var refactoring = new ExtractClass();

        // Act - Extract method and nested type
        var result = refactoring.Execute(
            sourceCode,
            "SymbolResolutionHelper",
            "PositionBasedResolver",
            null,
            "GetSymbolAtPosition",
            "SymbolResolutionResult");

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify extracted class is internal (Bug #4)
        result.RefactoredCode.Should().Contain("internal class PositionBasedResolver");

        // Verify extracted method is internal (Bug #3)
        result.RefactoredCode.Should().Contain("internal SymbolResolutionResult GetSymbolAtPosition");

        // Verify method removed from source (Bug #1)
        var sourceClassStart = result.RefactoredCode.IndexOf("public class SymbolResolutionHelper");
        var extractedClassStart = result.RefactoredCode.IndexOf("internal class PositionBasedResolver");

        sourceClassStart.Should().BeGreaterThanOrEqualTo(0, "source class should exist");
        extractedClassStart.Should().BeGreaterThan(sourceClassStart, "extracted class should come after source class");

        var sourceClassSection = result.RefactoredCode.Substring(
            sourceClassStart,
            extractedClassStart - sourceClassStart);

        sourceClassSection.Should().NotContain("GetSymbolAtPosition(int line, int column)");

        // Verify composition field created
        result.RefactoredCode.Should().Contain("private readonly PositionBasedResolver _positionBasedResolver = new PositionBasedResolver();");

        // Verify nested type moved
        result.RefactoredCode.Should().Contain("public class SymbolResolutionResult");

        // Verify operation succeeded
        result.Message.Should().Contain("Extracted");
    }

    [Fact]
    public void Execute_ExtractsProtectedInternalMethod_MakesItInternal()
    {
        // Arrange - Issue #1: Missing accessibility modifier edge case (protected internal)
        var sourceCode = @"public class DataProcessor
{
    protected internal void ProcessData(string data)
    {
        Console.WriteLine(data);
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "DataProcessor", "DataHandler", null, "ProcessData");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("internal class DataHandler");
        result.RefactoredCode.Should().Contain("internal void ProcessData(string data)");
        result.RefactoredCode.Should().NotContain("protected internal void ProcessData");
    }

    [Fact]
    public void Execute_ExtractsPrivateProtectedMethod_MakesItInternal()
    {
        // Arrange - Issue #1: Missing accessibility modifier edge case (private protected)
        var sourceCode = @"public class ServiceManager
{
    private protected void ManageService()
    {
        // Service logic
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "ServiceManager", "ServiceOperations", null, "ManageService");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("internal class ServiceOperations");
        result.RefactoredCode.Should().Contain("internal void ManageService()");
        result.RefactoredCode.Should().NotContain("private protected void ManageService");
    }

    [Fact]
    public void Execute_ExtractsMixedAccessibilityMethods_AllBecomeInternal()
    {
        // Arrange - Issue #1: Comprehensive test for all accessibility modifiers
        var sourceCode = @"public class MixedAccess
{
    public void PublicMethod() { }
    private void PrivateMethod() { }
    protected void ProtectedMethod() { }
    internal void InternalMethod() { }
    protected internal void ProtectedInternalMethod() { }
    private protected void PrivateProtectedMethod() { }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "MixedAccess",
            "ExtractedMethods",
            null,
            "PublicMethod,PrivateMethod,ProtectedMethod,InternalMethod,ProtectedInternalMethod,PrivateProtectedMethod");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("internal class ExtractedMethods");

        // All methods should be internal in extracted class
        var extractedClassStart = result.RefactoredCode.IndexOf("internal class ExtractedMethods");
        extractedClassStart.Should().BeGreaterThanOrEqualTo(0, "extracted class should exist");

        var extractedSection = result.RefactoredCode.Substring(extractedClassStart);

        extractedSection.Should().Contain("internal void PublicMethod()");
        extractedSection.Should().Contain("internal void PrivateMethod()");
        extractedSection.Should().Contain("internal void ProtectedMethod()");
        extractedSection.Should().Contain("internal void InternalMethod()");
        extractedSection.Should().Contain("internal void ProtectedInternalMethod()");
        extractedSection.Should().Contain("internal void PrivateProtectedMethod()");

        // No other accessibility modifiers should remain in extracted class
        extractedSection.Should().NotContain("public void PublicMethod");
        extractedSection.Should().NotContain("private void PrivateMethod");
        extractedSection.Should().NotContain("protected void ProtectedMethod");
        extractedSection.Should().NotContain("protected internal void");
        extractedSection.Should().NotContain("private protected void");
    }

    [Fact]
    public void Execute_ExtractStaticMethod_PreservesStaticModifier()
    {
        // Arrange - Issue #7: Edge case test for static method extraction
        var sourceCode = @"public class MathHelper
{
    private static int _multiplier = 10;

    public static int Calculate(int value)
    {
        return value * _multiplier;
    }

    private static int Square(int x)
    {
        return x * x;
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "MathHelper", "Calculator", "_multiplier", "Calculate,Square");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("internal class Calculator");

        // Static methods should be preserved as static in extracted class
        result.RefactoredCode.Should().Contain("internal static int Calculate(int value)");
        result.RefactoredCode.Should().Contain("internal static int Square(int x)");

        // Static field should also be preserved
        result.RefactoredCode.Should().Contain("private static int _multiplier");
    }

    [Fact]
    public void Execute_ExtractAsyncMethod_PreservesAsyncModifier()
    {
        // Arrange - Issue #7: Edge case test for async method extraction
        var sourceCode = @"using System.Threading.Tasks;

public class DataService
{
    private string _apiUrl;

    private async Task<string> FetchDataAsync()
    {
        await Task.Delay(100);
        return ""data"";
    }

    public async Task ProcessAsync()
    {
        var data = await FetchDataAsync();
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "DataService", "ApiClient", "_apiUrl", "FetchDataAsync");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("internal class ApiClient");

        // Async modifier should be preserved
        result.RefactoredCode.Should().Contain("internal async Task<string> FetchDataAsync()");

        // Verify delegation in source class
        result.RefactoredCode.Should().Contain("await _apiClient.FetchDataAsync()");
    }

    [Fact]
    public void Execute_ExtractGenericMethod_PreservesTypeConstraints()
    {
        // Arrange - Issue #7: Edge case test for generic method with constraints
        var sourceCode = @"using System.Collections.Generic;

public class DataProcessor
{
    private List<string> _data = new List<string>();

    private TResult Transform<TResult>(TResult defaultValue) where TResult : class
    {
        return defaultValue;
    }

    public void Process()
    {
        var result = Transform<string>(""default"");
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "DataProcessor", "Transformer", null, "Transform");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("internal class Transformer");

        // Generic method with constraints should be preserved
        result.RefactoredCode.Should().Contain("where TResult : class");
        result.RefactoredCode.Should().Contain("internal TResult Transform<TResult>");
    }

    [Fact]
    public void Execute_ExtractMethodWithAttributes_PreservesAttributes()
    {
        // Arrange - Issue #7: Edge case test for method with attributes
        var sourceCode = @"using System;

public class ApiController
{
    [Obsolete(""Use NewMethod instead"")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(""Style"", ""IDE0060"")]
    private string OldMethod(string param)
    {
        return param.ToUpper();
    }

    public void Execute()
    {
        var result = OldMethod(""test"");
    }
}";
        var refactoring = new ExtractClass();

        // Act
        var result = refactoring.Execute(sourceCode, "ApiController", "LegacyMethods", null, "OldMethod");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("internal class LegacyMethods");

        // Attributes should be preserved on extracted method
        result.RefactoredCode.Should().Contain("[Obsolete(\"Use NewMethod instead\")]");
        result.RefactoredCode.Should().Contain("[System.Diagnostics.CodeAnalysis.SuppressMessage(\"Style\", \"IDE0060\")]");
        result.RefactoredCode.Should().Contain("internal string OldMethod(string param)");
    }

    [Fact]
    public void Execute_ExtractNestedDelegate_ReturnsError()
    {
        // Arrange - Issue #3: Validate that delegate extraction is not supported
        var sourceCode = @"public class EventManager
{
    public delegate void EventHandler(string message);

    private EventHandler _handler;

    public void RaiseEvent(string msg)
    {
        _handler?.Invoke(msg);
    }
}";
        var refactoring = new ExtractClass();

        // Act - Attempt to extract a nested delegate type
        var result = refactoring.Execute(
            sourceCode,
            "EventManager",
            "EventHandlers",
            null,
            null,
            "EventHandler");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Nested delegate extraction is not supported");
        result.ErrorMessage.Should().Contain("EventHandler");
        result.ErrorMessage.Should().Contain("BaseMethodDeclarationSyntax");
    }

    #endregion
}
