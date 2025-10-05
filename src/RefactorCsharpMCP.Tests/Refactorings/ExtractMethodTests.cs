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
}
