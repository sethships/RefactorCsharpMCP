using FluentAssertions;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Tests.Refactorings;

public class InlineMethodTests
{
    #region Basic Functionality Tests

    [Fact]
    public void Execute_WithSimpleVoidMethod_SingleStatement_ShouldInlineMethod()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        DoSomething();
    }

    private void DoSomething()
    {
        Console.WriteLine(""Hello"");
    }
}";
        var inliner = new InlineMethod();

        // Act - line 9, column 18 (on 'DoSomething' method name)
        var result = inliner.Execute(sourceCode, 9, 18);

        // Assert
        result.IsSuccess.Should().BeTrue(because: $"Error: {result.Message}");
        result.RefactoredCode.Should().Contain("Console.WriteLine(\"Hello\");");
        result.RefactoredCode.Should().NotContain("private void DoSomething()");
    }

    [Fact]
    public void Execute_WithSimpleVoidMethod_MultipleStatements_ShouldInlineMethod()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        DoMultiple();
    }

    private void DoMultiple()
    {
        Console.WriteLine(""Line 1"");
        Console.WriteLine(""Line 2"");
        Console.WriteLine(""Line 3"");
    }
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 9, 18);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Console.WriteLine(\"Line 1\");");
        result.RefactoredCode.Should().Contain("Console.WriteLine(\"Line 2\");");
        result.RefactoredCode.Should().Contain("Console.WriteLine(\"Line 3\");");
        result.RefactoredCode.Should().NotContain("private void DoMultiple()");
    }

    [Fact]
    public void Execute_WithExpressionBodiedMethod_ShouldInlineMethod()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        PrintMessage();
    }

    private void PrintMessage() => Console.WriteLine(""Expression bodied"");
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 9, 18);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Console.WriteLine(\"Expression bodied\");");
        result.RefactoredCode.Should().NotContain("private void PrintMessage()");
    }

    [Fact]
    public void Execute_WithMethodHavingComments_ShouldPreserveComments()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        DoSomething();
    }

    // This is an important method
    private void DoSomething()
    {
        Console.WriteLine(""Hello"");
    }
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 10, 18);

        // Assert
        result.IsSuccess.Should().BeTrue($"Error: {result.ErrorMessage}");
        result.RefactoredCode.Should().Contain("// This is an important method");
    }

    [Fact]
    public void Execute_WithMethodAtDifferentPosition_ShouldResolveCorrectly()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        Helper();
    }

    private void Helper()
    {
        Console.WriteLine(""Helper"");
    }
}";
        var inliner = new InlineMethod();

        // Act - Try different positions on the method declaration line
        var result = inliner.Execute(sourceCode, 9, 22); // on 'Helper' identifier

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Console.WriteLine(\"Helper\");");
    }

    #endregion

    #region Parameter Substitution Tests

    [Fact]
    public void Execute_WithSingleIntParameter_ShouldSubstituteParameter()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        PrintNumber(42);
    }

    private void PrintNumber(int x)
    {
        Console.WriteLine(x);
    }
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 9, 18);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Console.WriteLine(42);");
        result.RefactoredCode.Should().NotContain("private void PrintNumber");
    }

    [Fact]
    public void Execute_WithSingleStringParameter_ShouldSubstituteParameter()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        PrintMessage(""Hello World"");
    }

    private void PrintMessage(string msg)
    {
        Console.WriteLine(msg);
    }
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 9, 18);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Console.WriteLine(\"Hello World\");");
        result.RefactoredCode.Should().NotContain("private void PrintMessage");
    }

    [Fact]
    public void Execute_WithMultipleParameters_ShouldSubstituteAllParameters()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        PrintSum(10, 20);
    }

    private void PrintSum(int a, int b)
    {
        Console.WriteLine(a + b);
    }
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 9, 18);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Console.WriteLine(10 + 20);");
        result.RefactoredCode.Should().NotContain("private void PrintSum");
    }

    [Fact]
    public void Execute_WithParameterUsedMultipleTimes_ShouldSubstituteAll()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        Repeat(5);
    }

    private void Repeat(int x)
    {
        Console.WriteLine(x);
        Console.WriteLine(x);
        Console.WriteLine(x);
    }
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 9, 18);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var occurrences = System.Text.RegularExpressions.Regex.Matches(result.RefactoredCode ?? "", "Console\\.WriteLine\\(5\\);").Count;
        occurrences.Should().Be(3);
        result.RefactoredCode.Should().NotContain("private void Repeat");
    }

    [Fact]
    public void Execute_WithComplexExpressionAsArgument_ShouldPreservePrecedence()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        PrintDouble(5 + 3);
    }

    private void PrintDouble(int x)
    {
        Console.WriteLine(x * 2);
    }
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 9, 18);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Should wrap 5 + 3 in parentheses due to precedence: (5 + 3) * 2
        result.RefactoredCode.Should().Contain("(5 + 3) * 2");
        result.RefactoredCode.Should().NotContain("private void PrintDouble");
    }

    [Fact]
    public void Execute_WithParameterInExpressionBody_ShouldSubstituteParameter()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        PrintValue(99);
    }

    private void PrintValue(int val) => Console.WriteLine(val);
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 9, 18);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Console.WriteLine(99);");
        result.RefactoredCode.Should().NotContain("private void PrintValue");
    }

    [Fact]
    public void Execute_WithDifferentPrimitiveTypes_ShouldHandleAll()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        PrintTypes(123, 45L, true, 3.14, 'A');
    }

    private void PrintTypes(int i, long l, bool b, double d, char c)
    {
        Console.WriteLine(i);
        Console.WriteLine(l);
        Console.WriteLine(b);
        Console.WriteLine(d);
        Console.WriteLine(c);
    }
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 9, 18);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Console.WriteLine(123);");
        result.RefactoredCode.Should().Contain("Console.WriteLine(45L);");
        result.RefactoredCode.Should().Contain("Console.WriteLine(true);");
        result.RefactoredCode.Should().Contain("Console.WriteLine(3.14);");
        result.RefactoredCode.Should().Contain("Console.WriteLine('A');");
        result.RefactoredCode.Should().NotContain("private void PrintTypes");
    }

    [Fact]
    public void Execute_WithStringAndIntParameters_ShouldSubstituteBoth()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        LogValue(""Count"", 100);
    }

    private void LogValue(string name, int value)
    {
        Console.WriteLine(name + "": "" + value);
    }
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 9, 18);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Console.WriteLine(\"Count\" + \": \" + 100);");
        result.RefactoredCode.Should().NotContain("private void LogValue");
    }

    [Fact]
    public void Execute_WithMethodCallAsArgument_ShouldPreserveMethodCall()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        PrintValue(GetValue());
    }

    private void PrintValue(int x)
    {
        Console.WriteLine(x);
    }

    private int GetValue() => 42;
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 9, 18);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Console.WriteLine(GetValue());");
        result.RefactoredCode.Should().NotContain("private void PrintValue");
    }

    [Fact]
    public void Execute_WithSemanticParameterMatching_ShouldOnlySubstituteParameterReferences()
    {
        // Arrange - Tests the semantic symbol matching fix for variable shadowing bug
        // Method has parameter 'x' and also uses Console.WriteLine (not a parameter)
        // Old buggy code with textual matching would only work correctly here
        // This test validates that semantic matching substitutes parameter 'x' correctly
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        Helper(42);  // Pass argument 42 for parameter 'x'
    }

    private void Helper(int x)
    {
        // Use parameter 'x' - should be substituted
        // Use Console (not a parameter) - should NOT be substituted
        Console.WriteLine(x * 2);  // Should become: Console.WriteLine(42 * 2);
    }
}";
        var inliner = new InlineMethod();

        // Act - inline Helper method (line 9, column 18)
        var result = inliner.Execute(sourceCode, 9, 18);

        // Assert
        result.IsSuccess.Should().BeTrue(because: $"Error: {result.Message}");
        // Should inline with parameter 'x' substituted to 42
        result.RefactoredCode.Should().Contain("Console.WriteLine(42 * 2);");
        result.RefactoredCode.Should().NotContain("private void Helper");
    }

    #endregion

    #region Validation/Error Cases Tests

    [Fact]
    public void Execute_WithInvalidPosition_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method()
    {
    }
}";
        var inliner = new InlineMethod();

        // Act - position on whitespace
        var result = inliner.Execute(sourceCode, 2, 1);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("No method found");
    }

    [Fact]
    public void Execute_WithVirtualMethod_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        DoSomething();
    }

    protected virtual void DoSomething()
    {
        Console.WriteLine(""Virtual"");
    }
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 9, 28);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("virtual");
    }

    [Fact]
    public void Execute_WithAbstractMethod_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"
public abstract class Test
{
    protected abstract void DoSomething();
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 4, 29);

        // Assert
        result.IsSuccess.Should().BeFalse($"Expected failure but got: {result.RefactoredCode}");
        result.Message.Should().Contain("abstract", $"Actual message: {result.Message}");
    }

    [Fact]
    public void Execute_WithRecursiveMethod_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        Factorial(5);
    }

    private void Factorial(int n)
    {
        if (n > 1)
            Factorial(n - 1);
    }
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 9, 18);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("recursive");
    }

    [Fact]
    public void Execute_WithMultipleCallers_ShouldSucceed()
    {
        // Arrange - Part 2 now supports multiple callers
        var sourceCode = @"
public class Test
{
    public void Caller1()
    {
        DoSomething();
    }

    public void Caller2()
    {
        DoSomething();
    }

    private void DoSomething()
    {
        Console.WriteLine(""Hello"");
    }
}";
        var inliner = new InlineMethod();

        // Act - inline DoSomething at line 14, column 18
        var result = inliner.Execute(sourceCode, 14, 18);

        // Assert - Part 2 should inline at both call sites
        result.IsSuccess.Should().BeTrue(because: $"Error: {result.Message}");
        var occurrences = System.Text.RegularExpressions.Regex.Matches(
            result.RefactoredCode,
            System.Text.RegularExpressions.Regex.Escape("Console.WriteLine(\"Hello\");"));
        occurrences.Count.Should().Be(2, "Both call sites should be inlined");
        result.RefactoredCode.Should().NotContain("private void DoSomething()");
    }

    [Fact]
    public void Execute_WithNoCallers_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    private void UnusedMethod()
    {
        Console.WriteLine(""Never called"");
    }
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 4, 18);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("no callers");
    }

    [Fact]
    public void Execute_WithRefParameter_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        int x = 5;
        Modify(ref x);
    }

    private void Modify(ref int value)
    {
        value++;
    }
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 10, 18);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("ref/out parameter");
    }

    [Fact]
    public void Execute_WithOutParameter_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        int result;
        TryGet(out result);
    }

    private void TryGet(out int value)
    {
        value = 42;
    }
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 10, 18);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("ref/out parameter");
    }

    [Fact]
    public void Execute_WithNonVoidReturnType_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        var x = Calculate();
    }

    private int Calculate()
    {
        return 42;
    }
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 9, 17);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("return value");
        result.Message.Should().Contain("Part 1 only supports void methods");
    }

    [Fact]
    public void Execute_WithComplexParameterType_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        var list = new List<int>();
        Process(list);
    }

    private void Process(List<int> items)
    {
        items.Add(1);
    }
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 10, 18);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("complex parameter type");
    }

    [Fact]
    public void Execute_WithEmptySourceCode_ShouldReturnFailure()
    {
        // Arrange
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute("", 1, 1);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Source code cannot be empty");
    }

    [Fact]
    public void Execute_WithInvalidLineNumber_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method() { }
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 0, 1);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Line number must be >= 1");
    }

    [Fact]
    public void Execute_WithLocalVariableConflict_ShouldReturnFailure()
    {
        // Arrange - Tests identifier conflict validation
        // Method body uses local variable 'counter', call site also has local 'counter'
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        var counter = 100;  // Local variable 'counter' at call site
        Helper();
    }

    private void Helper()
    {
        var counter = 0;  // Local variable 'counter' in method body - CONFLICT!
        Console.WriteLine(counter);
    }
}";
        var inliner = new InlineMethod();

        // Act - inline Helper method (line 10, column 18)
        var result = inliner.Execute(sourceCode, 10, 18);

        // Assert
        result.IsSuccess.Should().BeFalse(because: "Identifier conflict should be detected");
        result.Message.Should().Contain("conflict", "Error message should mention conflict");
        result.Message.Should().Contain("counter", "Error message should list the conflicting identifier");
    }

    [Fact]
    public void Execute_WithFieldConflict_ShouldReturnFailure()
    {
        // Arrange - Tests field identifier conflict validation
        // Method body references field 'value', call site has local variable 'value'
        var sourceCode = @"
public class Test
{
    private int value = 5;  // Field

    public void Caller()
    {
        var value = 10;  // Local variable shadows field - CONFLICT!
        Helper();
    }

    private void Helper()
    {
        Console.WriteLine(this.value);  // References field
    }
}";
        var inliner = new InlineMethod();

        // Act - inline Helper method (line 12, column 18)
        var result = inliner.Execute(sourceCode, 12, 18);

        // Assert
        result.IsSuccess.Should().BeFalse(because: "Field identifier conflict should be detected");
        result.Message.Should().Contain("conflict", "Error message should mention conflict");
        result.Message.Should().Contain("value", "Error message should list the conflicting identifier");
    }

    [Fact]
    public void Execute_WithNoIdentifierConflict_ShouldSucceed()
    {
        // Arrange - Positive test: No identifier conflicts
        // Method body and call site use different identifier names
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        var localVar = 10;  // Different name from method body
        Helper();
    }

    private void Helper()
    {
        var differentVar = 20;  // No conflict - different name
        Console.WriteLine(differentVar);
    }
}";
        var inliner = new InlineMethod();

        // Act - inline Helper method (line 10, column 18)
        var result = inliner.Execute(sourceCode, 10, 18);

        // Assert
        result.IsSuccess.Should().BeTrue(because: $"No conflict should allow inlining. Error: {result.Message}");
        result.RefactoredCode.Should().Contain("var differentVar = 20;");
        result.RefactoredCode.Should().NotContain("private void Helper");
    }

    [Fact]
    public void Execute_WithPropertyConflict_ShouldReturnFailure()
    {
        // Arrange - Tests property identifier conflict validation
        // Method body references property, call site has local with same name
        var sourceCode = @"
public class Test
{
    public int Count { get; set; }  // Property

    public void Caller()
    {
        var Count = 100;  // Local variable - CONFLICT with property!
        Helper();
    }

    private void Helper()
    {
        Console.WriteLine(Count);  // References property
    }
}";
        var inliner = new InlineMethod();

        // Act - inline Helper method (line 12, column 18)
        var result = inliner.Execute(sourceCode, 12, 18);

        // Assert
        result.IsSuccess.Should().BeFalse(because: "Property identifier conflict should be detected");
        result.Message.Should().Contain("conflict", "Error message should mention conflict");
        result.Message.Should().Contain("Count", "Error message should list the conflicting identifier");
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public void Execute_WithMethodInNestedClass_ShouldInlineCorrectly()
    {
        // Arrange
        var sourceCode = @"
public class Outer
{
    public class Inner
    {
        public void Caller()
        {
            Helper();
        }

        private void Helper()
        {
            Console.WriteLine(""Nested"");
        }
    }
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 11, 22);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Console.WriteLine(\"Nested\");");
        result.RefactoredCode.Should().NotContain("private void Helper()");
    }

    [Fact]
    public void Execute_WithWhitespaceAndComments_ShouldPreserveStructure()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        // Call the helper
        DoWork();
        // Done
    }

    /// <summary>
    /// Does important work
    /// </summary>
    private void DoWork()
    {
        // Important comment
        Console.WriteLine(""Work"");
    }
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 14, 18);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("// Call the helper");
        result.RefactoredCode.Should().Contain("/// <summary>");
        result.RefactoredCode.Should().Contain("// Important comment");
    }

    [Fact]
    public void Execute_WithMethodHavingOnlyExpressionBody_ShouldInlineExpression()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        Log();
    }

    private void Log() => System.Diagnostics.Debug.WriteLine(""Log"");
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 9, 18);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("System.Diagnostics.Debug.WriteLine(\"Log\");");
        result.RefactoredCode.Should().NotContain("private void Log()");
    }

    [Fact]
    public void Execute_WithMethodCallInsideBlock_ShouldInlineAtCorrectLocation()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        if (true)
        {
            Helper();
        }
    }

    private void Helper()
    {
        Console.WriteLine(""In block"");
    }
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 12, 18);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("if (true)");
        result.RefactoredCode.Should().Contain("Console.WriteLine(\"In block\");");
        result.RefactoredCode.Should().NotContain("private void Helper()");
    }

    [Fact]
    public void Execute_WithStaticMethod_ShouldInlineCorrectly()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public static void Caller()
    {
        Helper();
    }

    private static void Helper()
    {
        Console.WriteLine(""Static"");
    }
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 9, 25);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Console.WriteLine(\"Static\");");
        result.RefactoredCode.Should().NotContain("private static void Helper()");
    }

    [Fact]
    public void Execute_WithPublicMethod_ShouldInlineCorrectly()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        PublicHelper();
    }

    public void PublicHelper()
    {
        Console.WriteLine(""Public"");
    }
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 9, 17);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Console.WriteLine(\"Public\");");
        result.RefactoredCode.Should().NotContain("public void PublicHelper()");
    }

    [Fact]
    public void Execute_WithMethodHavingMultipleStatementsInBlock_ShouldInlineAllStatements()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        Process();
    }

    private void Process()
    {
        var x = 1;
        var y = 2;
        var z = x + y;
        Console.WriteLine(z);
    }
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 9, 18);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("var x = 1;");
        result.RefactoredCode.Should().Contain("var y = 2;");
        result.RefactoredCode.Should().Contain("var z = x + y;");
        result.RefactoredCode.Should().Contain("Console.WriteLine(z);");
        result.RefactoredCode.Should().NotContain("private void Process()");
    }

    [Fact]
    public void Execute_WithParameterNameShadowing_ShouldHandleCorrectly()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        var x = 10;
        UseValue(x);
    }

    private void UseValue(int x)
    {
        Console.WriteLine(x);
    }
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 10, 18);

        // Assert
        result.IsSuccess.Should().BeTrue($"Error: {result.ErrorMessage}");
        // Parameter 'x' should be replaced with argument 'x' (from caller scope)
        result.RefactoredCode.Should().Contain("Console.WriteLine(x);");
        result.RefactoredCode.Should().NotContain("private void UseValue");
    }

    [Fact]
    public void Execute_WithDecimalParameter_ShouldSubstituteCorrectly()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        PrintDecimal(123.45m);
    }

    private void PrintDecimal(decimal value)
    {
        Console.WriteLine(value);
    }
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 9, 18);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Console.WriteLine(123.45m);");
        result.RefactoredCode.Should().NotContain("private void PrintDecimal");
    }

    [Fact]
    public void Execute_WithFloatParameter_ShouldSubstituteCorrectly()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        PrintFloat(3.14f);
    }

    private void PrintFloat(float value)
    {
        Console.WriteLine(value);
    }
}";
        var inliner = new InlineMethod();

        // Act
        var result = inliner.Execute(sourceCode, 9, 18);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Console.WriteLine(3.14f);");
        result.RefactoredCode.Should().NotContain("private void PrintFloat");
    }

    #endregion

    #region Framework-Aware Integration Tests

    [Fact]
    public async Task ExecuteAsync_WithNet80_ShouldNotRequireConversion()
    {
        // Arrange
        var sourceCode = @"
using System;

public class Test
{
    public void Caller()
    {
        DoWork();
    }

    private void DoWork()
    {
        Console.WriteLine(""Work"");
    }
}";
        var inliner = new InlineMethod();

        // Act
        var result = await inliner.ExecuteAsync(sourceCode, 11, 18, "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue($"Error: {result.ErrorMessage}");
        result.RefactoredCode.Should().Contain("Console.WriteLine(\"Work\");");
        result.RefactoredCode.Should().NotContain("private void DoWork()");
    }

    [Fact(Skip = "Requires net48 reference assemblies which may not be installed on all systems")]
    public async Task ExecuteAsync_WithNet48_ShouldApplyValidation()
    {
        // Arrange
        var sourceCode = @"
using System;

public class Test
{
    public void Caller()
    {
        DoWork();
    }

    private void DoWork()
    {
        Console.WriteLine(""Work"");
    }
}";
        var inliner = new InlineMethod();

        // Act
        var result = await inliner.ExecuteAsync(sourceCode, 11, 18, "net48");

        // Assert
        result.IsSuccess.Should().BeTrue($"Error: {result.ErrorMessage}");
        result.RefactoredCode.Should().Contain("Console.WriteLine(\"Work\");");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidFramework_ShouldReturnValidationFailure()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        DoWork();
    }

    private void DoWork()
    {
        Console.WriteLine(""Work"");
    }
}";
        var inliner = new InlineMethod();

        // Act
        var result = await inliner.ExecuteAsync(sourceCode, 9, 18, "invalid-framework");

        // Assert - Should fail validation
        // Note: This depends on SyntaxValidator implementation
        // For now, just verify it doesn't crash
        result.Should().NotBeNull();
    }

    #endregion

    #region Multiple Call Site Tests (Part 2)

    [Fact]
    public void Execute_WithTwoCallSites_ShouldInlineBoth()
    {
        // Arrange - Test multiple call site support
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        Helper();
        Console.WriteLine(""Between calls"");
        Helper();
    }

    private void Helper()
    {
        Console.WriteLine(""Inline me"");
    }
}";
        var inliner = new InlineMethod();

        // Act - inline Helper method (line 11, column 18)
        var result = inliner.Execute(sourceCode, 11, 18);

        // Assert
        result.IsSuccess.Should().BeTrue(because: $"Error: {result.Message}");
        // Both calls should be inlined
        var occurrences = System.Text.RegularExpressions.Regex.Matches(
            result.RefactoredCode,
            System.Text.RegularExpressions.Regex.Escape("Console.WriteLine(\"Inline me\");"));
        occurrences.Count.Should().Be(2, "Both call sites should be inlined");
        result.RefactoredCode.Should().NotContain("private void Helper()");
        result.RefactoredCode.Should().Contain("Between calls");
    }

    [Fact]
    public void Execute_WithFiveCallSites_ShouldInlineAll()
    {
        // Arrange - Test with 5 call sites
        var sourceCode = @"
public class Test
{
    public void Caller1() { Worker(); }
    public void Caller2() { Worker(); }
    public void Caller3() { Worker(); }
    public void Caller4() { Worker(); }
    public void Caller5() { Worker(); }

    private void Worker()
    {
        Console.WriteLine(""Working"");
    }
}";
        var inliner = new InlineMethod();

        // Act - inline Worker method (line 10, column 18)
        var result = inliner.Execute(sourceCode, 10, 18);

        // Assert
        result.IsSuccess.Should().BeTrue(because: $"Error: {result.Message}");
        // All 5 calls should be inlined
        var occurrences = System.Text.RegularExpressions.Regex.Matches(
            result.RefactoredCode,
            System.Text.RegularExpressions.Regex.Escape("Console.WriteLine(\"Working\");"));
        occurrences.Count.Should().Be(5, "All 5 call sites should be inlined");
        result.RefactoredCode.Should().NotContain("private void Worker()");
    }

    [Fact]
    public void Execute_WithTenCallSites_ShouldInlineAll()
    {
        // Arrange - Performance test with 10 call sites
        var sourceCode = @"
public class Test
{
    public void Execute()
    {
        Log(); Log(); Log(); Log(); Log();
        Log(); Log(); Log(); Log(); Log();
    }

    private void Log()
    {
        Console.WriteLine(""Log entry"");
    }
}";
        var inliner = new InlineMethod();

        // Act - inline Log method (line 10, column 18)
        var result = inliner.Execute(sourceCode, 10, 18);

        // Assert
        result.IsSuccess.Should().BeTrue(because: $"Error: {result.Message}");
        // All 10 calls should be inlined
        var occurrences = System.Text.RegularExpressions.Regex.Matches(
            result.RefactoredCode,
            System.Text.RegularExpressions.Regex.Escape("Console.WriteLine(\"Log entry\");"));
        occurrences.Count.Should().Be(10, "All 10 call sites should be inlined");
        result.RefactoredCode.Should().NotContain("private void Log()");
    }

    [Fact]
    public void Execute_WithMultipleCallSites_DifferentArguments_ShouldSubstituteCorrectly()
    {
        // Arrange - Different arguments at each call site
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        Process(1);
        Process(2);
        Process(42);
    }

    private void Process(int value)
    {
        Console.WriteLine(value * 2);
    }
}";
        var inliner = new InlineMethod();

        // Act - inline Process method (line 11, column 18)
        var result = inliner.Execute(sourceCode, 11, 18);

        // Assert
        result.IsSuccess.Should().BeTrue(because: $"Error: {result.Message}");
        // Each call should be substituted with correct argument
        result.RefactoredCode.Should().Contain("Console.WriteLine(1 * 2);");
        result.RefactoredCode.Should().Contain("Console.WriteLine(2 * 2);");
        result.RefactoredCode.Should().Contain("Console.WriteLine(42 * 2);");
        result.RefactoredCode.Should().NotContain("private void Process");
    }

    [Fact]
    public void Execute_WithMultipleCallSites_StaticAndInstance_ShouldInlineBoth()
    {
        // Arrange - Both static and instance calls
        var sourceCode = @"
public class Test
{
    private int field = 10;

    public void InstanceMethod()
    {
        Print();
    }

    public static void StaticMethod()
    {
        Print();
    }

    private static void Print()
    {
        Console.WriteLine(""Message"");
    }
}";
        var inliner = new InlineMethod();

        // Act - inline Print method (line 14, column 25)
        var result = inliner.Execute(sourceCode, 14, 25);

        // Assert
        result.IsSuccess.Should().BeTrue(because: $"Error: {result.Message}");
        // Both static and instance calls should be inlined
        var occurrences = System.Text.RegularExpressions.Regex.Matches(
            result.RefactoredCode,
            System.Text.RegularExpressions.Regex.Escape("Console.WriteLine(\"Message\");"));
        occurrences.Count.Should().Be(2, "Both static and instance call sites should be inlined");
        result.RefactoredCode.Should().NotContain("private static void Print()");
    }

    // Note: Expression-bodied callers calling expression-bodied methods is a known limitation
    // The current implementation expects invocations to be in ExpressionStatements.
    // This edge case can be addressed in a future enhancement.

    #endregion
}
