using FluentAssertions;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Tests.Refactorings;

public class ExtractMethodTests
{
    [Fact]
    public void Execute_WithValidSimpleCode_ShouldExtractMethod()
    {
        // Arrange
        var sourceCode = @"public class TestClass
{
    public void OriginalMethod()
    {
        var x = 1;
        var y = 2;
        var z = x + y;
        Console.WriteLine(z);
    }
}";
        var extractor = new ExtractMethod();

        // Act
        var result = extractor.Execute(sourceCode, 6, 7, "CalculateSum");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().NotBeNullOrEmpty();
        // Note: Data flow analysis now correctly detects that x is used in extracted code
        result.RefactoredCode.Should().Contain("CalculateSum(x);");
        result.RefactoredCode.Should().Contain("CalculateSum(int x)");
        result.Message.Should().Contain("Extracted method 'CalculateSum'");
    }

    [Fact]
    public void Execute_WithEmptySourceCode_ShouldReturnFailure()
    {
        // Arrange
        var extractor = new ExtractMethod();

        // Act
        var result = extractor.Execute("", 1, 2, "TestMethod");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Source code cannot be empty");
    }

    [Fact]
    public void Execute_WithEmptyMethodName_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = "public class Test { }";
        var extractor = new ExtractMethod();

        // Act
        var result = extractor.Execute(sourceCode, 1, 1, "");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Method name cannot be empty");
    }

    [Fact]
    public void Execute_WithInvalidLineRange_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = "public class Test { }";
        var extractor = new ExtractMethod();

        // Act
        var result = extractor.Execute(sourceCode, 5, 3, "TestMethod");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid line range");
    }

    [Fact]
    public void Execute_WithLineRangeBeyondSourceCode_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"public class Test
{
    void Method() { }
}";
        var extractor = new ExtractMethod();

        // Act
        var result = extractor.Execute(sourceCode, 1, 100, "TestMethod");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No method found containing lines");
    }

    [Fact]
    public void Execute_WithSingleLineExtraction_ShouldWork()
    {
        // Arrange
        var sourceCode = @"public class TestClass
{
    public void Method()
    {
        Console.WriteLine(""Hello"");
    }
}";
        var extractor = new ExtractMethod();

        // Act
        var result = extractor.Execute(sourceCode, 5, 5, "PrintHello");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("PrintHello();");
        result.RefactoredCode.Should().Contain("PrintHello()");
    }

    [Fact]
    public void RefactoringResult_Success_ShouldHaveCorrectProperties()
    {
        // Act
        var result = RefactoringResult.Success("refactored code", "Success message");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Be("refactored code");
        result.Message.Should().Be("Success message");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void RefactoringResult_Failure_ShouldHaveCorrectProperties()
    {
        // Act
        var result = RefactoringResult.Failure("Error occurred");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Error occurred");
        result.Message.Should().Contain("Refactoring failed");
        result.RefactoredCode.Should().BeNull();
    }

    [Fact]
    public void Execute_WithInstanceFieldAccess_ShouldNotGenerateThisParameter()
    {
        // Arrange - Regression test for issue #60
        var sourceCode = @"public class PasswordGenerator
{
    private int _length;
    private char[] _charSet;

    public void GeneratePassword()
    {
        var password = new StringBuilder();
        for (int i = 0; i < _length; ++i)
        {
            password.Append(_charSet[i % _charSet.Length]);
        }
        Console.WriteLine(password.ToString());
    }
}";
        var extractor = new ExtractMethod();

        // Act - Extract the loop that uses instance fields _length and _charSet
        var result = extractor.Execute(sourceCode, 9, 12, "BuildPasswordString");

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Should NOT contain invalid 'this' parameter syntax
        result.RefactoredCode.Should().NotContain("PasswordGenerator this");
        result.RefactoredCode.Should().NotContain("(this,");
        result.RefactoredCode.Should().NotContain("(this)");
        // Should be a valid instance method that can access _length and _charSet directly
        result.RefactoredCode.Should().Contain("BuildPasswordString(password);");
        result.RefactoredCode.Should().Contain("private void BuildPasswordString(StringBuilder password)");
        // Should still use the instance fields
        result.RefactoredCode.Should().Contain("_length");
        result.RefactoredCode.Should().Contain("_charSet");
    }

    [Fact]
    public void Execute_WithVariableDeclaredOutsideButAssignedInside_ShouldDeclareLocally()
    {
        // Arrange - Regression test for issue #60 (flags variable case)
        var sourceCode = @"public class PasswordGenerator
{
    private int _length;

    public void GeneratePassword()
    {
        var password = new StringBuilder();
        var charTypes = new List<char[]> { new[] { 'a', 'b' }, new[] { '1', '2' } };
        bool[] flags;
        do
        {
            password.Clear();
            flags = new bool[charTypes.Count];
            for (int i = 0; i < _length; ++i)
            {
                password.Append('x');
                flags[i % flags.Length] = true;
            }
        }
        while (Array.Exists(flags, f => !f));
        Console.WriteLine(password);
    }
}";
        var extractor = new ExtractMethod();

        // Act - Extract the do-while loop where flags is assigned but declared outside
        var result = extractor.Execute(sourceCode, 10, 20, "GeneratePasswordWithRetry");

        // Assert
        result.IsSuccess.Should().BeTrue();
        // The extracted method should declare flags locally since it's assigned in the extracted region
        result.RefactoredCode.Should().Contain("bool[] flags;");
        // Should NOT have 'flags' as a parameter since it's declared locally
        result.RefactoredCode.Should().NotContain("GeneratePasswordWithRetry(StringBuilder password, List<char[]> charTypes, bool[] flags)");
        // Should compile without undeclared variable errors
        result.RefactoredCode.Should().Contain("GeneratePasswordWithRetry(password, charTypes);");
    }
}
