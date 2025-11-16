using FluentAssertions;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Tests.Refactorings;

public class InlineVariableTests
{
    #region Basic Functionality Tests

    [Fact]
    public void Execute_WithSimpleLiteral_ShouldInlineVariable()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        var x = 5;
        Console.WriteLine(x);
    }
}";
        var inliner = new InlineVariable();

        // Act - line 6, column 13 (on 'x' in 'var x = 5')
        var result = inliner.Execute(sourceCode, 6, 13, "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Console.WriteLine(5);");
        result.RefactoredCode.Should().NotContain("var x = 5");
    }

    [Fact]
    public void Execute_WithStringLiteral_ShouldInlineVariable()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        var name = ""test"";
        Console.WriteLine(name);
    }
}";
        var inliner = new InlineVariable();

        // Act
        var result = inliner.Execute(sourceCode, 6, 13, "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Console.WriteLine(\"test\");");
        result.RefactoredCode.Should().NotContain("var name = \"test\"");
    }

    [Fact]
    public void Execute_WithMethodCall_ShouldInlineVariable()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        var result = Calculate();
        Console.WriteLine(result);
    }

    private int Calculate() => 42;
}";
        var inliner = new InlineVariable();

        // Act
        var result = inliner.Execute(sourceCode, 6, 13, "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Console.WriteLine(Calculate());");
        result.RefactoredCode.Should().NotContain("var result = Calculate()");
    }

    [Fact]
    public void Execute_WithObjectCreation_ShouldInlineVariable()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        var obj = new Object();
        Console.WriteLine(obj);
    }
}";
        var inliner = new InlineVariable();

        // Act
        var result = inliner.Execute(sourceCode, 6, 13, "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Console.WriteLine(new Object());");
        result.RefactoredCode.Should().NotContain("var obj = new Object()");
    }

    #endregion

    #region Multiple Uses Tests

    [Fact]
    public void Execute_WithMultipleReferences_ShouldInlineAll()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        var x = 10;
        Console.WriteLine(x);
        Console.WriteLine(x);
        Console.WriteLine(x);
    }
}";
        var inliner = new InlineVariable();

        // Act
        var result = inliner.Execute(sourceCode, 6, 13, "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Console.WriteLine(10);");
        result.RefactoredCode.Should().NotContain("var x = 10");
        // Verify all 3 references were replaced
        var count = result.RefactoredCode?.Split(new[] { "Console.WriteLine(10);" }, StringSplitOptions.None).Length - 1;
        count.Should().Be(3);
    }

    [Fact]
    public void Execute_WithSingleUse_ShouldInlineAndRemove()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        var temp = 42;
        Console.WriteLine(temp);
    }
}";
        var inliner = new InlineVariable();

        // Act
        var result = inliner.Execute(sourceCode, 6, 13, "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Console.WriteLine(42);");
        result.RefactoredCode.Should().NotContain("var temp = 42");
        result.Message.Should().Contain("1 reference");
    }

    #endregion

    #region Operator Precedence Tests

    [Fact]
    public void Execute_WithBinaryExpression_ShouldAddParentheses()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        var x = 1 + 2;
        var y = x * 3;
    }
}";
        var inliner = new InlineVariable();

        // Act
        var result = inliner.Execute(sourceCode, 6, 13, "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("var y = (1 + 2) * 3;");
    }

    [Fact]
    public void Execute_WithComplexExpression_ShouldPreservePrecedence()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        var a = 1 + 2;
        var b = a * 3 + 4;
    }
}";
        var inliner = new InlineVariable();

        // Act
        var result = inliner.Execute(sourceCode, 6, 13, "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("var b = (1 + 2) * 3 + 4;");
    }

    [Fact]
    public void Execute_WithHighPrecedenceExpression_ShouldNotAddParentheses()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        var x = Calculate();
        var y = x + 1;
    }

    private int Calculate() => 42;
}";
        var inliner = new InlineVariable();

        // Act
        var result = inliner.Execute(sourceCode, 6, 13, "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("var y = Calculate() + 1;");
        result.RefactoredCode.Should().NotContain("(Calculate())");
    }

    #endregion

    #region Edge Cases and Failure Tests

    [Fact]
    public void Execute_WithUninitializedVariable_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        int x;
        x = 5;
        Console.WriteLine(x);
    }
}";
        var inliner = new InlineVariable();

        // Act
        var result = inliner.Execute(sourceCode, 6, 13, "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("no initializer");
    }

    [Fact]
    public void Execute_WithParameterNotVariable_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method(int parameter)
    {
        Console.WriteLine(parameter);
    }
}";
        var inliner = new InlineVariable();

        // Act - trying to inline parameter at line 4
        var result = inliner.Execute(sourceCode, 4, 28, "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No local variable found");
    }

    [Fact]
    public void Execute_WithFieldNotVariable_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    private int _field = 10;

    public void Method()
    {
        Console.WriteLine(_field);
    }
}";
        var inliner = new InlineVariable();

        // Act
        var result = inliner.Execute(sourceCode, 4, 17, "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No local variable found");
    }

    [Fact]
    public void Execute_WithMultipleAssignments_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        var x = 1;
        x = 2;
        Console.WriteLine(x);
    }
}";
        var inliner = new InlineVariable();

        // Act
        var result = inliner.Execute(sourceCode, 6, 13, "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("assigned after its declaration");
    }

    [Fact]
    public void Execute_WithIncrementOperator_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        var count = 0;
        count++;
        Console.WriteLine(count);
    }
}";
        var inliner = new InlineVariable();

        // Act
        var result = inliner.Execute(sourceCode, 6, 13, "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("increment/decrement operators");
    }

    [Fact]
    public void Execute_WithDecrementOperator_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        var count = 10;
        count--;
        Console.WriteLine(count);
    }
}";
        var inliner = new InlineVariable();

        // Act
        var result = inliner.Execute(sourceCode, 6, 13, "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("increment/decrement operators");
    }

    [Fact]
    public void Execute_WithLambdaCapture_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        var x = 10;
        Action a = () => Console.WriteLine(x);
        a();
    }
}";
        var inliner = new InlineVariable();

        // Act
        var result = inliner.Execute(sourceCode, 6, 13, "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("lambda");
        result.ErrorMessage.Should().Contain("not supported");
    }

    #endregion

    #region Input Validation Tests

    [Fact]
    public void Execute_WithEmptySourceCode_ShouldReturnFailure()
    {
        // Arrange
        var inliner = new InlineVariable();

        // Act
        var result = inliner.Execute("", 1, 1, "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cannot be empty");
    }

    [Fact]
    public void Execute_WithInvalidLineNumber_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        var x = 5;
    }
}";
        var inliner = new InlineVariable();

        // Act
        var result = inliner.Execute(sourceCode, 0, 1, "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Line number must be >= 1");
    }

    [Fact]
    public void Execute_WithInvalidColumnNumber_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        var x = 5;
    }
}";
        var inliner = new InlineVariable();

        // Act
        var result = inliner.Execute(sourceCode, 6, 0, "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Column number must be >= 1");
    }

    #endregion

    #region Framework Validation Tests

    [Fact]
    public async Task ExecuteAsync_WithValidFramework_ShouldValidateInput()
    {
        // Arrange
        var sourceCode = @"
using System;

public class Test
{
    public void Method()
    {
        var x = 5;
        Console.WriteLine(x);
    }
}";
        var inliner = new InlineVariable();

        // Act
        var result = await inliner.ExecuteAsync(sourceCode, 8, 13, "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue($"Error: {result.ErrorMessage}");
        result.RefactoredCode.Should().Contain("Console.WriteLine(5);");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidFramework_ShouldReturnValidationFailure()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        var x = 5;
        Console.WriteLine(x);
    }
}";
        var inliner = new InlineVariable();

        // Act
        var result = await inliner.ExecuteAsync(sourceCode, 6, 13, "invalid-framework");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region Complex Expression Tests

    [Fact]
    public void Execute_WithNestedBinaryExpressions_ShouldHandleCorrectly()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        var x = 1 + 2 * 3;
        var y = x + 4;
    }
}";
        var inliner = new InlineVariable();

        // Act
        var result = inliner.Execute(sourceCode, 6, 13, "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("var y = 1 + 2 * 3 + 4;");
    }

    [Fact]
    public void Execute_WithMultipleVariablesInStatement_ShouldInlineCorrectOne()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        var x = 10;
        var y = 20;
        var z = x + y;
    }
}";
        var inliner = new InlineVariable();

        // Act - inline 'x'
        var result = inliner.Execute(sourceCode, 6, 13, "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("var z = 10 + y;");
        result.RefactoredCode.Should().NotContain("var x = 10");
        result.RefactoredCode.Should().Contain("var y = 20"); // y should remain
    }

    #endregion

    #region Additional Edge Case Tests (from CR Issue #6)

    [Fact]
    public void Execute_WithConditionalExpression_ShouldInlineCorrectly()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method(bool condition)
    {
        var result = condition ? 10 : 20;
        Console.WriteLine(result);
    }
}";
        var inliner = new InlineVariable();

        // Act
        var result = inliner.Execute(sourceCode, 6, 13, "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Console.WriteLine(condition ? 10 : 20);");
        result.RefactoredCode.Should().NotContain("var result = condition");
    }

    [Fact]
    public void Execute_WithVariableUsedInNestedScopes_ShouldInlineAllReferences()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method(bool condition)
    {
        var x = 42;
        if (condition)
        {
            Console.WriteLine(x);
        }
        else
        {
            Console.WriteLine(x);
        }
        Console.WriteLine(x);
    }
}";
        var inliner = new InlineVariable();

        // Act
        var result = inliner.Execute(sourceCode, 6, 13, "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Console.WriteLine(42);");
        result.RefactoredCode.Should().NotContain("var x = 42");
        // All 3 references should be inlined
        var count = result.RefactoredCode?.Split(new[] { "Console.WriteLine(42);" }, StringSplitOptions.None).Length - 1;
        count.Should().Be(3);
    }

    [Fact]
    public void Execute_WithSideEffectExpression_ShouldInlineButPreserveSemantics()
    {
        // Arrange - Note: This tests that we CAN inline, but implementation should ideally warn about side effects in future
        var sourceCode = @"
public class Test
{
    private int _counter = 0;

    public void Method()
    {
        var value = GetNextValue();
        Console.WriteLine(value);
    }

    private int GetNextValue() => _counter++;
}";
        var inliner = new InlineVariable();

        // Act
        var result = inliner.Execute(sourceCode, 8, 13, "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Console.WriteLine(GetNextValue());");
        result.RefactoredCode.Should().NotContain("var value = GetNextValue()");
    }

    [Fact]
    public void Execute_WithSwitchExpression_ShouldInlineCorrectly()
    {
        // Arrange - C# 8+ switch expression
        var sourceCode = @"
public class Test
{
    public void Method(int day)
    {
        var name = day switch
        {
            1 => ""Monday"",
            2 => ""Tuesday"",
            _ => ""Unknown""
        };
        Console.WriteLine(name);
    }
}";
        var inliner = new InlineVariable();

        // Act
        var result = inliner.Execute(sourceCode, 6, 13, "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Console.WriteLine(day switch");
        result.RefactoredCode.Should().NotContain("var name = day switch");
    }

    [Fact]
    public void Execute_WithOutOfOrderUsage_ShouldInlineCorrectly()
    {
        // Arrange - Variable declared in middle of method
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        Console.WriteLine(""Start"");
        var temp = 123;
        Console.WriteLine(""Middle"");
        Console.WriteLine(temp);
        Console.WriteLine(""End"");
    }
}";
        var inliner = new InlineVariable();

        // Act
        var result = inliner.Execute(sourceCode, 7, 13, "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Console.WriteLine(123);");
        result.RefactoredCode.Should().NotContain("var temp = 123");
        result.RefactoredCode.Should().Contain("Console.WriteLine(\"Start\");");
        result.RefactoredCode.Should().Contain("Console.WriteLine(\"Middle\");");
        result.RefactoredCode.Should().Contain("Console.WriteLine(\"End\");");
    }

    #endregion
}
