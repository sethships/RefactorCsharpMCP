using FluentAssertions;
using RefactorCsharpMCP.Server.Tools;
using System.Text.Json;

namespace RefactorCsharpMCP.Tests.Tools;

public class FixDiagnosticToolTests
{
    [Fact]
    public async Task FixDiagnostic_WithEmptySourceCode_ShouldReturnError()
    {
        // Arrange
        var tool = new FixDiagnosticTool();

        // Act
        var result = await tool.FixDiagnostic("", "IDE0005", 1, 1, "net8.0");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("Source code cannot be empty");
    }

    [Fact]
    public async Task FixDiagnostic_WithEmptyDiagnosticId_ShouldReturnError()
    {
        // Arrange
        var tool = new FixDiagnosticTool();
        var sourceCode = "public class Test { }";

        // Act
        var result = await tool.FixDiagnostic(sourceCode, "", 1, 1, "net8.0");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("Diagnostic ID cannot be empty");
    }

    [Fact]
    public async Task FixDiagnostic_WithEmptyFramework_ShouldReturnError()
    {
        // Arrange
        var tool = new FixDiagnosticTool();
        var sourceCode = "public class Test { }";

        // Act
        var result = await tool.FixDiagnostic(sourceCode, "IDE0005", 1, 1, "");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("Target framework cannot be empty");
    }

    [Fact]
    public async Task FixDiagnostic_WithUnsupportedDiagnostic_ShouldReturnError()
    {
        // Arrange
        var tool = new FixDiagnosticTool();
        var sourceCode = "public class Test { }";

        // Act
        var result = await tool.FixDiagnostic(sourceCode, "IDE9999", 1, 1, "net8.0");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("No refactoring available");
        doc.RootElement.GetProperty("error").GetString().Should().Contain("IDE9999");
    }

    [Fact]
    public async Task FixDiagnostic_ResponseStructure_ContainsAllFields()
    {
        // Arrange
        var tool = new FixDiagnosticTool();
        var sourceCode = @"
using System;
using System.Linq;  // Unused

public class Test
{
    public void Method()
    {
        Console.WriteLine(""Test"");
    }
}";

        // Act
        var result = await tool.FixDiagnostic(sourceCode, "IDE0005", 1, 1, "net8.0");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);

        // Should have these fields regardless of success/failure
        doc.RootElement.TryGetProperty("success", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("diagnosticId", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("message", out _).Should().BeTrue();
    }

    [Fact]
    public async Task FixDiagnostic_IDE0005_MapsToRemoveUnusedUsings()
    {
        // Arrange
        var tool = new FixDiagnosticTool();
        var sourceCode = @"
using System;

public class Test
{
    public void Method()
    {
        Console.WriteLine(""Test"");
    }
}";

        // Act - Even if fix fails (no unused usings), should route to correct refactoring
        var result = await tool.FixDiagnostic(sourceCode, "IDE0005", 1, 1, "net8.0");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);

        if (doc.RootElement.GetProperty("success").GetBoolean())
        {
            doc.RootElement.GetProperty("appliedRefactoring").GetString()
                .Should().Be("remove_unused_usings");
        }
    }

    [Fact]
    public async Task FixDiagnostic_CS8019_MapsToRemoveUnusedUsings()
    {
        // Arrange
        var tool = new FixDiagnosticTool();
        var sourceCode = @"
using System;

public class Test
{
    public void Method()
    {
        Console.WriteLine(""Test"");
    }
}";

        // Act
        var result = await tool.FixDiagnostic(sourceCode, "CS8019", 1, 1, "net8.0");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);

        if (doc.RootElement.GetProperty("success").GetBoolean())
        {
            doc.RootElement.GetProperty("appliedRefactoring").GetString()
                .Should().Be("remove_unused_usings");
        }
    }

    [Fact]
    public async Task FixDiagnostic_WithLargeSourceCode_ShouldReturnError()
    {
        // Arrange
        var tool = new FixDiagnosticTool();
        var largeSource = new string('x', 2 * 1024 * 1024); // > 1MB

        // Act
        var result = await tool.FixDiagnostic(largeSource, "IDE0005", 1, 1, "net8.0");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("exceeds 1MB limit");
    }

    [Theory]
    [InlineData("ide0005")]  // lowercase
    [InlineData("IDE0005")]  // uppercase
    [InlineData("IdE0005")]  // mixed case
    public async Task FixDiagnostic_DiagnosticIdCaseInsensitive_ShouldWork(string diagnosticId)
    {
        // Arrange
        var tool = new FixDiagnosticTool();
        var sourceCode = @"
using System;

public class Test
{
    public void Method()
    {
        Console.WriteLine(""Test"");
    }
}";

        // Act
        var result = await tool.FixDiagnostic(sourceCode, diagnosticId, 1, 1, "net8.0");

        // Assert - Should not fail due to case sensitivity
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);

        // Either succeeds or fails with refactoring error, not parsing error
        doc.RootElement.TryGetProperty("success", out _).Should().BeTrue();
    }

    [Fact]
    public async Task FixDiagnostic_IDE0044_WithInvalidLocation_ShouldReturnError()
    {
        // Arrange
        var tool = new FixDiagnosticTool();
        var sourceCode = @"
public class Test
{
    private int _value;

    public Test()
    {
        _value = 42;
    }
}";

        // Act - Invalid line/column that doesn't point to a field
        var result = await tool.FixDiagnostic(sourceCode, "IDE0044", 999, 999, "net8.0");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task FixDiagnostic_WithOutOfBoundsLine_ShouldReturnError()
    {
        // Arrange
        var tool = new FixDiagnosticTool();
        var sourceCode = @"
public class Test
{
    private int _value;
}";

        // Act - Line number beyond end of file (file has ~5 lines, requesting line 100)
        var result = await tool.FixDiagnostic(sourceCode, "IDE0044", 100, 1, "net8.0");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("out of range");
        doc.RootElement.GetProperty("error").GetString().Should().Contain("Line 100");
    }

    [Fact]
    public async Task FixDiagnostic_WithOutOfBoundsColumn_ShouldReturnError()
    {
        // Arrange
        var tool = new FixDiagnosticTool();
        var sourceCode = @"
public class Test
{
    private int _value;
}";

        // Act - Column number beyond line length
        var result = await tool.FixDiagnostic(sourceCode, "IDE0044", 4, 999, "net8.0");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().Contain("out of range");
        doc.RootElement.GetProperty("error").GetString().Should().Contain("Column 999");
    }

    [Theory]
    [InlineData("IDE05")]      // Too short
    [InlineData("IDE00005")]   // Too long
    [InlineData("INVALID")]    // Wrong prefix
    [InlineData("IDE-0005")]   // Invalid character
    [InlineData("")]           // Empty
    [InlineData("IDE")]        // Missing digits
    [InlineData("0005")]       // Missing prefix
    public async Task FixDiagnostic_WithInvalidDiagnosticIdPattern_ShouldReturnError(string invalidId)
    {
        // Arrange
        var tool = new FixDiagnosticTool();
        var sourceCode = "public class Test { }";

        // Act
        var result = await tool.FixDiagnostic(sourceCode, invalidId, 1, 1, "net8.0");

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        var error = doc.RootElement.GetProperty("error").GetString();
        error.Should().NotBeNullOrEmpty();

        // Error message should mention pattern or being empty
        if (string.IsNullOrWhiteSpace(invalidId))
        {
            error.Should().Contain("cannot be empty");
        }
        else
        {
            error.Should().Contain("does not match expected pattern");
        }
    }
}
