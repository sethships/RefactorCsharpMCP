using FluentAssertions;
using RefactorCsharpMCP.Server.Tools;
using System.Text.Json;

namespace RefactorCsharpMCP.Tests.Tools;

public class ExtractClassToolTests
{
    [Fact]
    public async Task ExtractClass_WithValidInput_ShouldReturnSuccessResponse()
    {
        // Arrange
        var tool = new ExtractClassTool();
        var sourceCode = @"public class Service
{
    private ILogger _logger;
    private IDatabase _database;

    public void Process()
    {
        // Logic
    }
}";

        // Act
        var result = await tool.ExtractClass(sourceCode, "Service", "LoggingContext", "_logger");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("refactoredCode").GetString().Should().Contain("public class LoggingContext");
    }

    [Fact]
    public async Task ExtractClass_WithInvalidClassName_ShouldReturnError()
    {
        // Arrange
        var tool = new ExtractClassTool();
        var sourceCode = "public class Test { }";

        // Act
        var result = await tool.ExtractClass(sourceCode, "123Invalid", "NewClass", "_field");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("valid C# identifier");
    }

    [Fact]
    public async Task ExtractClass_WithEmptySourceCode_ShouldReturnError()
    {
        // Arrange
        var tool = new ExtractClassTool();

        // Act
        var result = await tool.ExtractClass("", "Test", "NewClass", "_field");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("Source code cannot be empty");
    }

    [Fact]
    public async Task ExtractClass_WithNonExistentField_ShouldReturnError()
    {
        // Arrange
        var tool = new ExtractClassTool();
        var sourceCode = @"public class Service
{
    private ILogger _logger;
}";

        // Act
        var result = await tool.ExtractClass(sourceCode, "Service", "NewClass", "_missing");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("message").GetString().Should().Contain("not found");
    }

    [Fact]
    public async Task ExtractClass_WithMethodsOnly_ShouldReturnSuccess()
    {
        // Arrange
        var tool = new ExtractClassTool();
        var sourceCode = @"public class Service
{
    private string _data;

    private void ValidateData()
    {
        // Validation logic
    }

    private string FormatData(string input)
    {
        return input.ToUpper();
    }

    public void Process()
    {
        ValidateData();
        var formatted = FormatData(_data);
    }
}";

        // Act - Extract methods only, no fields
        var result = await tool.ExtractClass(sourceCode, "Service", "DataValidator", "", "ValidateData,FormatData");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();

        var refactoredCode = doc.RootElement.GetProperty("refactoredCode").GetString();
        refactoredCode.Should().Contain("public class DataValidator");
        refactoredCode.Should().Contain("private void ValidateData()");
        refactoredCode.Should().Contain("private string FormatData(string input)");
        refactoredCode.Should().Contain("private readonly DataValidator _dataValidator");
        refactoredCode.Should().Contain("_dataValidator.ValidateData()");
        refactoredCode.Should().Contain("_dataValidator.FormatData(");
    }

    [Fact]
    public async Task ExtractClass_WithNullFieldsAndMethods_ShouldReturnError()
    {
        // Arrange
        var tool = new ExtractClassTool();
        var sourceCode = "public class Test { private int _field; }";

        // Act - Both fieldNames and methodNames are empty/null
        var result = await tool.ExtractClass(sourceCode, "Test", "NewClass", "", null);

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("At least one field or method name must be specified");
    }

    [Fact]
    public async Task ExtractClass_WithEmptyFieldsAndMethods_ShouldReturnError()
    {
        // Arrange
        var tool = new ExtractClassTool();
        var sourceCode = "public class Test { private int _field; }";

        // Act - Both fieldNames and methodNames are empty strings
        var result = await tool.ExtractClass(sourceCode, "Test", "NewClass", "", "");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("At least one field or method name must be specified");
    }

    [Fact]
    public async Task ExtractClass_ServicePattern_ShouldReturnSuccess()
    {
        // Arrange - Real-world service class extraction scenario
        var tool = new ExtractClassTool();
        var sourceCode = @"using System;

public class InlineMethod
{
    private ILogger _logger;

    private bool IsSimpleType(string typeName)
    {
        return typeName == ""int"" || typeName == ""string"";
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
}

public interface ILogger { }";

        // Act - Extract service class with methods only
        var result = await tool.ExtractClass(sourceCode, "InlineMethod", "TypeChecker", "", "IsSimpleType,IsRecursive");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();

        var refactoredCode = doc.RootElement.GetProperty("refactoredCode").GetString();
        refactoredCode.Should().Contain("public class TypeChecker");
        refactoredCode.Should().Contain("private bool IsSimpleType(string typeName)");
        refactoredCode.Should().Contain("private bool IsRecursive(string methodName)");
        refactoredCode.Should().Contain("private readonly TypeChecker _typeChecker");

        // Verify original field remains in InlineMethod
        refactoredCode.Should().Contain("private ILogger _logger");
    }
}
