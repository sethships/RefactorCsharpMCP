using FluentAssertions;
using RefactorCsharpMCP.Server.Formatting;
using RefactorCsharpMCP.Server.Tools;
using System.Text.Json;

namespace RefactorCsharpMCP.Tests.Tools;

public class ExtractClassToolTests
{
    [Fact]
    public async Task ExtractClass_WithValidInput_ShouldReturnSuccessResponse()
    {
        // Arrange
        var tool = new ExtractClassTool(new JsonResponseFormatter());
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
        var result = await tool.ExtractClass(
            sourceCode: sourceCode,
            className: "Service",
            newClassName: "LoggingContext",
            fieldNames: "_logger",
            validateCompilation: false); // Disable validation - uses undefined ILogger/IDatabase types

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("refactoredCode").GetString().Should().Contain("internal class LoggingContext");
    }

    [Fact]
    public async Task ExtractClass_WithInvalidClassName_ShouldReturnError()
    {
        // Arrange
        var tool = new ExtractClassTool(new JsonResponseFormatter());
        var sourceCode = "public class Test { }";

        // Act
        var result = await tool.ExtractClass(
            sourceCode: sourceCode,
            className: "123Invalid",
            newClassName: "NewClass",
            fieldNames: "_field");

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
        var tool = new ExtractClassTool(new JsonResponseFormatter());

        // Act
        var result = await tool.ExtractClass(
            sourceCode: "",
            className: "Test",
            newClassName: "NewClass",
            fieldNames: "_field");

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
        var tool = new ExtractClassTool(new JsonResponseFormatter());
        var sourceCode = @"public class Service
{
    private ILogger _logger;
}";

        // Act
        var result = await tool.ExtractClass(
            sourceCode: sourceCode,
            className: "Service",
            newClassName: "NewClass",
            fieldNames: "_missing",
            validateCompilation: false); // Disable validation - uses undefined ILogger type

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
        var tool = new ExtractClassTool(new JsonResponseFormatter());
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
        var result = await tool.ExtractClass(
            sourceCode: sourceCode,
            className: "Service",
            newClassName: "DataValidator",
            fieldNames: "",
            methodNames: "ValidateData,FormatData",
            validateCompilation: true); // Keep validation enabled - uses only BCL types

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();

        var refactoredCode = doc.RootElement.GetProperty("refactoredCode").GetString();
        refactoredCode.Should().Contain("internal class DataValidator");
        refactoredCode.Should().Contain("internal void ValidateData()");
        refactoredCode.Should().Contain("internal string FormatData(string input)");
        refactoredCode.Should().Contain("private readonly DataValidator _dataValidator");
        refactoredCode.Should().Contain("_dataValidator.ValidateData()");
        refactoredCode.Should().Contain("_dataValidator.FormatData(");
    }

    [Fact]
    public async Task ExtractClass_WithNullFieldsAndMethods_ShouldReturnError()
    {
        // Arrange
        var tool = new ExtractClassTool(new JsonResponseFormatter());
        var sourceCode = "public class Test { private int _field; }";

        // Act - Both fieldNames and methodNames are empty/null
        var result = await tool.ExtractClass(
            sourceCode: sourceCode,
            className: "Test",
            newClassName: "NewClass",
            fieldNames: "",
            methodNames: null);

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("At least one field, method, or nested type name must be specified");
    }

    [Fact]
    public async Task ExtractClass_WithEmptyFieldsAndMethods_ShouldReturnError()
    {
        // Arrange
        var tool = new ExtractClassTool(new JsonResponseFormatter());
        var sourceCode = "public class Test { private int _field; }";

        // Act - Both fieldNames and methodNames are empty strings
        var result = await tool.ExtractClass(
            sourceCode: sourceCode,
            className: "Test",
            newClassName: "NewClass",
            fieldNames: "",
            methodNames: "");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("At least one field, method, or nested type name must be specified");
    }

    [Fact]
    public async Task ExtractClass_ServicePattern_ShouldReturnSuccess()
    {
        // Arrange - Real-world service class extraction scenario
        var tool = new ExtractClassTool(new JsonResponseFormatter());
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
        var result = await tool.ExtractClass(
            sourceCode: sourceCode,
            className: "InlineMethod",
            newClassName: "TypeChecker",
            fieldNames: "",
            methodNames: "IsSimpleType,IsRecursive",
            validateCompilation: true); // Keep validation enabled - ILogger is defined in source code

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();

        var refactoredCode = doc.RootElement.GetProperty("refactoredCode").GetString();
        refactoredCode.Should().Contain("internal class TypeChecker");
        refactoredCode.Should().Contain("internal bool IsSimpleType(string typeName)");
        refactoredCode.Should().Contain("internal bool IsRecursive(string methodName)");
        refactoredCode.Should().Contain("private readonly TypeChecker _typeChecker");

        // Verify original field remains in InlineMethod
        refactoredCode.Should().Contain("private ILogger _logger");
    }
}
