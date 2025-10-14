using FluentAssertions;
using RefactorCsharpMCP.Core.Validation;
using Xunit;

namespace RefactorCsharpMCP.Tests.Validation;

/// <summary>
/// Tests for SyntaxValidator - validates input/output code compatibility with target frameworks.
/// </summary>
public class SyntaxValidatorTests
{
    private readonly SyntaxValidator _validator;

    public SyntaxValidatorTests()
    {
        _validator = new SyntaxValidator();
    }

    #region Input Validation Tests

    [Fact]
    public async Task ValidateInputAsync_ValidModernCode_WithNet8_Succeeds()
    {
        // Arrange - Modern C# code compatible with net8.0
        var sourceCode = @"
using System;

class Test
{
    public void Method()
    {
        var numbers = new[] { 1, 2, 3 };
        Console.WriteLine(numbers.Length);
    }
}";

        // Act
        var result = await _validator.ValidateInputAsync(sourceCode, "net8.0");

        // Assert
        result.IsValid.Should().BeTrue();
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public async Task ValidateInputAsync_ValidCSharp73Code_WithNet48_Succeeds()
    {
        // Arrange - Standard C# 7.3 code compatible with net48
        var sourceCode = @"
using System;

class Test
{
    public void Method()
    {
        var x = 42;
        Console.WriteLine(x);
    }
}";

        // Act
        var result = await _validator.ValidateInputAsync(sourceCode, "net48");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateInputAsync_ValidCSharp30Code_WithNet35_Succeeds()
    {
        // Arrange - C# 3.0 code compatible with net35
        var sourceCode = @"
using System;

class Test
{
    public void Method()
    {
        var numbers = new[] { 1, 2, 3 };
        int sum = 0;
        foreach (var n in numbers)
        {
            sum += n;
        }
    }
}";

        // Act
        var result = await _validator.ValidateInputAsync(sourceCode, "net35");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateInputAsync_SyntaxErrors_ReturnsSyntaxError()
    {
        // Arrange - Code with genuine syntax errors
        var sourceCode = @"
class Test
{
    public void Method(
    {
        // Missing closing parenthesis
    }
}";

        // Act
        var result = await _validator.ValidateInputAsync(sourceCode, "net8.0");

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.SYNTAX_ERROR);
        result.ErrorMessage.Should().Contain("syntax errors");
    }

    [Fact]
    public async Task ValidateInputAsync_UnsupportedFramework_ReturnsUnknownFramework()
    {
        // Arrange
        var sourceCode = "class Test { }";

        // Act
        var result = await _validator.ValidateInputAsync(sourceCode, "net1.0");

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.UNKNOWN_FRAMEWORK);
        result.ErrorMessage.Should().Contain("Unsupported framework");
    }

    [Fact]
    public async Task ValidateInputAsync_EmptySource_FailsValidation()
    {
        // Arrange
        var sourceCode = string.Empty;

        // Act & Assert
        var result = await _validator.ValidateInputAsync(sourceCode, "net8.0");
        result.IsValid.Should().BeFalse();
    }

    #endregion

    #region Output Validation Tests

    [Fact]
    public async Task ValidateOutputAsync_ValidRefactoredCode_WithNet8_Succeeds()
    {
        // Arrange - Valid refactored code compatible with net8.0
        var refactoredCode = @"
using System;

class Test
{
    public void Method()
    {
        ProcessData();
    }

    private void ProcessData()
    {
        Console.WriteLine(""Processing"");
    }
}";

        // Act
        var result = await _validator.ValidateOutputAsync(refactoredCode, "net8.0");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateOutputAsync_ValidNet48Code_WithNet48_Succeeds()
    {
        // Arrange
        var refactoredCode = @"
using System;
using System.Collections.Generic;

class Test
{
    private readonly List<int> _numbers = new List<int>();

    public void AddNumber(int n)
    {
        _numbers.Add(n);
    }
}";

        // Act
        var result = await _validator.ValidateOutputAsync(refactoredCode, "net48");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateOutputAsync_SyntaxErrors_ReturnsSyntaxError()
    {
        // Arrange - Refactored code with syntax errors
        var refactoredCode = @"
class Test
{
    public void Method()
    {
        var x = ;  // Missing expression
    }
}";

        // Act
        var result = await _validator.ValidateOutputAsync(refactoredCode, "net8.0");

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.SYNTAX_ERROR);
    }

    [Fact]
    public async Task ValidateOutputAsync_ValidCode_WithDifferentFrameworks_Succeeds()
    {
        // Arrange - Simple code that works on all frameworks
        var code = @"
class Test
{
    public void Method()
    {
        var x = 42;
    }
}";

        // Act - Test multiple frameworks
        var net8Result = await _validator.ValidateOutputAsync(code, "net8.0");
        var net48Result = await _validator.ValidateOutputAsync(code, "net48");
        var net35Result = await _validator.ValidateOutputAsync(code, "net35");

        // Assert
        net8Result.IsValid.Should().BeTrue();
        net48Result.IsValid.Should().BeTrue();
        net35Result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Framework Compatibility Tests

    [Theory]
    [InlineData("net9.0")]
    [InlineData("net8.0")]
    public async Task ValidateInputAsync_ModernNetFrameworks_AcceptModernSyntax(string framework)
    {
        // Arrange - Modern C# syntax (pattern matching)
        var sourceCode = @"
using System;

class Test
{
    public void Method(string input)
    {
        if (input != null)
        {
            Console.WriteLine(input);
        }
    }
}";

        // Act
        var result = await _validator.ValidateInputAsync(sourceCode, framework);

        // Assert
        result.IsValid.Should().BeTrue($"framework {framework} should support this syntax");
    }

    [Theory]
    [InlineData("net48")]
    [InlineData("net472")]
    [InlineData("net462")]
    public async Task ValidateInputAsync_NetFramework_AcceptsCSharp73Syntax(string framework)
    {
        // Arrange - C# 7.3 syntax
        var sourceCode = @"
using System;

class Test
{
    public void Method()
    {
        var tuple = (1, 2);
        Console.WriteLine(tuple.Item1);
    }
}";

        // Act
        var result = await _validator.ValidateInputAsync(sourceCode, framework);

        // Assert
        result.IsValid.Should().BeTrue($"framework {framework} should support C# 7.3 syntax");
    }

    [Fact]
    public async Task ValidateInputAsync_NetStandard20_SupportsCSharp73()
    {
        // Arrange - C# 7.3 code
        var sourceCode = @"
using System;

class Test
{
    public void Method()
    {
        var numbers = new[] { 1, 2, 3 };
    }
}";

        // Act
        var result = await _validator.ValidateInputAsync(sourceCode, "netstandard2.0");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Error Message Quality Tests

    [Fact]
    public async Task ValidateInputAsync_ProvidesmeaningfulErrorMessages()
    {
        // Arrange - Code with syntax error
        var sourceCode = @"
class Test
{
    public void Method(
    {
    }
}";

        // Act
        var result = await _validator.ValidateInputAsync(sourceCode, "net48");

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.SuggestedAction.Should().NotBeNullOrEmpty();
        result.SuggestedAction.Should().Contain("Fix syntax errors");
    }

    [Fact]
    public async Task ValidateInputAsync_UnsupportedFramework_ProvidesClearGuidance()
    {
        // Arrange
        var sourceCode = "class Test { }";

        // Act
        var result = await _validator.ValidateInputAsync(sourceCode, "netfx5.0");

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.UNKNOWN_FRAMEWORK);
        result.ErrorMessage.Should().Contain("Unsupported framework");
        result.SuggestedAction.Should().Contain("Microsoft-supported");
    }

    #endregion
}
