using FluentAssertions;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Tests.Refactorings;

public class RenameSymbolTests
{
    // ============================================================================
    // Basic Rename Tests
    // ============================================================================

    [Fact]
    public void Execute_WithLocalVariable_ShouldRenameAllReferences()
    {
        // Arrange
        var sourceCode = @"
public class Calculator
{
    public int Add(int a, int b)
    {
        var sum = a + b;
        return sum;
    }
}";
        var refactoring = new RenameSymbol();

        // Act - Position on "sum" at line 6 (var sum = ...)
        var result = refactoring.Execute(sourceCode, 6, 13, "total", "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("var total = a + b;");
        result.RefactoredCode.Should().Contain("return total;");
        result.RefactoredCode.Should().NotContain("sum");
        result.Message.Should().Contain("'sum' to 'total'");
        result.Message.Should().Contain("2 references");
    }

    [Fact]
    public void Execute_WithMethodParameter_ShouldRenameAllReferences()
    {
        // Arrange
        var sourceCode = @"
public class Greeter
{
    public string Greet(string name)
    {
        return ""Hello, "" + name;
    }
}";
        var refactoring = new RenameSymbol();

        // Act - Position on "name" parameter at line 4
        var result = refactoring.Execute(sourceCode, 4, 32, "personName", "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Greet(string personName)");
        result.RefactoredCode.Should().Contain("\"Hello, \" + personName");
        result.RefactoredCode.Should().NotContain("name");
        result.Message.Should().Contain("'name' to 'personName'");
    }

    [Fact]
    public void Execute_WithPrivateField_ShouldRenameAllReferences()
    {
        // Arrange
        var sourceCode = @"
public class Counter
{
    private int _count;

    public void Increment()
    {
        _count++;
    }

    public int GetCount()
    {
        return _count;
    }
}";
        var refactoring = new RenameSymbol();

        // Act - Position on "_count" field at line 4
        var result = refactoring.Execute(sourceCode, 4, 17, "_value", "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("private int _value;");
        result.RefactoredCode.Should().Contain("_value++;");
        result.RefactoredCode.Should().Contain("return _value;");
        result.RefactoredCode.Should().NotContain("_count");
    }

    [Fact]
    public void Execute_WithPrivateMethod_ShouldRenameAllReferences()
    {
        // Arrange
        var sourceCode = @"
public class Service
{
    private void DoWork()
    {
        // Implementation
    }

    public void Execute()
    {
        DoWork();
    }
}";
        var refactoring = new RenameSymbol();

        // Act - Position on "DoWork" method at line 4
        var result = refactoring.Execute(sourceCode, 4, 18, "PerformWork", "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("private void PerformWork()");
        result.RefactoredCode.Should().Contain("PerformWork();");
        result.RefactoredCode.Should().NotContain("DoWork");
    }

    [Fact]
    public void Execute_WithMultipleReferences_ShouldRenameAll()
    {
        // Arrange
        var sourceCode = @"
public class Processor
{
    public void Process()
    {
        var data = GetData();
        var result = Transform(data);
        Save(data);
        return;
    }
}";
        var refactoring = new RenameSymbol();

        // Act - Position on "data" at line 6
        var result = refactoring.Execute(sourceCode, 6, 13, "input", "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("var input = GetData();");
        result.RefactoredCode.Should().Contain("Transform(input)");
        result.RefactoredCode.Should().Contain("Save(input)");
        result.Message.Should().Contain("3 references");
    }

    // ============================================================================
    // Position-Based Resolution Tests
    // ============================================================================

    [Fact]
    public void Execute_PositionOnUsageNotDeclaration_ShouldResolveAndRename()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        var x = 5;
        var y = x + 10;
    }
}";
        var refactoring = new RenameSymbol();

        // Act - Position on "x" usage at line 7, not declaration
        var result = refactoring.Execute(sourceCode, 7, 17, "value", "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("var value = 5;");
        result.RefactoredCode.Should().Contain("var y = value + 10;");
    }

    [Fact]
    public void Execute_WithInvalidPosition_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method() { }
}";
        var refactoring = new RenameSymbol();

        // Act - Position out of range
        var result = refactoring.Execute(sourceCode, 100, 100, "newName", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("out of range");
    }

    [Fact]
    public void Execute_WithPositionOnWhitespace_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method() { }
}";
        var refactoring = new RenameSymbol();

        // Act - Position on whitespace/class keyword area
        var result = refactoring.Execute(sourceCode, 2, 1, "newName", "net8.0");

        // Assert - Position resolves to class, which is not supported for rename
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Only local variables, parameters, private fields, and private methods");
    }

    [Fact]
    public void Execute_WithZeroLineNumber_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = "public class Test { }";
        var refactoring = new RenameSymbol();

        // Act
        var result = refactoring.Execute(sourceCode, 0, 10, "newName", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid position");
    }

    [Fact]
    public void Execute_WithNegativeColumnNumber_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = "public class Test { }";
        var refactoring = new RenameSymbol();

        // Act
        var result = refactoring.Execute(sourceCode, 1, -1, "newName", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid position");
    }

    // ============================================================================
    // Conflict Detection Tests
    // ============================================================================

    [Fact]
    public void Execute_WithConflictingLocalVariable_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        var x = 5;
        var y = 10;
    }
}";
        var refactoring = new RenameSymbol();

        // Act - Try to rename x to y (conflict)
        var result = refactoring.Execute(sourceCode, 6, 13, "y", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("conflicts");
        result.ErrorMessage.Should().Contain("y");
    }

    [Fact]
    public void Execute_WithConflictingParameter_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method(int x, int y)
    {
        var z = x + y;
    }
}";
        var refactoring = new RenameSymbol();

        // Act - Try to rename z to x (conflict with parameter)
        var result = refactoring.Execute(sourceCode, 6, 13, "x", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("conflicts");
    }

    [Fact]
    public void Execute_WithConflictingFieldName_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    private int _field;

    public void Method()
    {
        var local = 10;
    }
}";
        var refactoring = new RenameSymbol();

        // Act - Try to rename local to _field (conflict)
        var result = refactoring.Execute(sourceCode, 8, 13, "_field", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("conflicts");
    }

    [Fact]
    public void Execute_WithNoConflicts_ShouldSucceed()
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
        var refactoring = new RenameSymbol();

        // Act - Rename to a non-conflicting name
        var result = refactoring.Execute(sourceCode, 6, 13, "newVariable", "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    // ============================================================================
    // Identifier Validation Tests
    // ============================================================================

    [Fact]
    public void Execute_WithValidIdentifier_ShouldSucceed()
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
        var refactoring = new RenameSymbol();

        // Act
        var result = refactoring.Execute(sourceCode, 6, 13, "validName_123", "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("validName_123");
    }

    [Fact]
    public void Execute_WithIdentifierStartingWithUnderscore_ShouldSucceed()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    private int field;
}";
        var refactoring = new RenameSymbol();

        // Act
        var result = refactoring.Execute(sourceCode, 4, 17, "_field", "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("_field");
    }

    [Fact]
    public void Execute_WithIdentifierStartingWithNumber_ShouldReturnFailure()
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
        var refactoring = new RenameSymbol();

        // Act
        var result = refactoring.Execute(sourceCode, 6, 13, "1invalid", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not a valid C# identifier");
    }

    [Fact]
    public void Execute_WithIdentifierContainingSpecialCharacters_ShouldReturnFailure()
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
        var refactoring = new RenameSymbol();

        // Act
        var result = refactoring.Execute(sourceCode, 6, 13, "invalid-name", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not a valid C# identifier");
    }

    [Fact]
    public void Execute_WithEmptyNewName_ShouldReturnFailure()
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
        var refactoring = new RenameSymbol();

        // Act
        var result = refactoring.Execute(sourceCode, 6, 13, "", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("New name cannot be empty");
    }

    [Fact]
    public void Execute_WithWhitespaceNewName_ShouldReturnFailure()
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
        var refactoring = new RenameSymbol();

        // Act
        var result = refactoring.Execute(sourceCode, 6, 13, "   ", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("New name cannot be empty");
    }

    // ============================================================================
    // Edge Cases
    // ============================================================================

    [Fact]
    public void Execute_WithSameNameInDifferentScopes_ShouldOnlyRenameTargetScope()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void Method1()
    {
        var x = 5;
        var y = x + 1;
    }

    public void Method2()
    {
        var x = 10;
        var z = x + 2;
    }
}";
        var refactoring = new RenameSymbol();

        // Act - Rename x in Method1
        var result = refactoring.Execute(sourceCode, 6, 13, "value1", "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("var value1 = 5;");
        result.RefactoredCode.Should().Contain("var y = value1 + 1;");
        result.RefactoredCode.Should().Contain("var x = 10;"); // Should NOT rename x in Method2
        result.RefactoredCode.Should().Contain("var z = x + 2;");
    }

    [Fact]
    public void Execute_WithThisQualifier_ShouldRenameField()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    private int _field;

    public void Method()
    {
        this._field = 10;
        var x = this._field;
    }
}";
        var refactoring = new RenameSymbol();

        // Act - Position on _field
        var result = refactoring.Execute(sourceCode, 4, 17, "_value", "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("private int _value;");
        result.RefactoredCode.Should().Contain("this._value = 10;");
        result.RefactoredCode.Should().Contain("var x = this._value;");
    }

    [Fact]
    public void Execute_WithExpressionBodiedMember_ShouldRename()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    private int _value;

    public int GetValue() => _value;
}";
        var refactoring = new RenameSymbol();

        // Act
        var result = refactoring.Execute(sourceCode, 4, 17, "_data", "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("private int _data;");
        result.RefactoredCode.Should().Contain("=> _data;");
    }

    [Fact]
    public void Execute_RenameToSameName_ShouldReturnFailure()
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
        var refactoring = new RenameSymbol();

        // Act - Try to rename x to x
        var result = refactoring.Execute(sourceCode, 6, 13, "x", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("already named 'x'");
    }

    // ============================================================================
    // Integration Tests (SyntaxTree Identity and Reference Finding)
    // ============================================================================

    [Fact]
    public void Execute_WithMultipleUsages_FindsAndRenamesAllReferences()
    {
        // Integration test verifying that SyntaxTree identity is maintained
        // and all references are found and renamed (tests the fix for SyntaxTree mismatch bug)
        var sourceCode = @"
public class TestClass
{
    public void Method()
    {
        int oldName = 5;
        Console.WriteLine(oldName);
        Console.WriteLine(oldName + 10);
        var result = oldName * 2;
    }
}";
        var refactoring = new RenameSymbol();

        // Act - Rename "oldName" at line 6, column 13 (declaration)
        var result = refactoring.Execute(sourceCode, 6, 13, "newName", "net8.0");

        // Assert - Should succeed and report correct reference count
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("4 references updated"); // Declaration + 3 usages

        // Assert - All occurrences should be renamed
        result.RefactoredCode.Should().Contain("int newName = 5;");
        result.RefactoredCode.Should().Contain("Console.WriteLine(newName);");
        result.RefactoredCode.Should().Contain("Console.WriteLine(newName + 10);");
        result.RefactoredCode.Should().Contain("var result = newName * 2;");

        // Assert - Old name should be completely gone
        result.RefactoredCode.Should().NotContain("oldName");
    }

    [Fact]
    public void Execute_WithFieldMultipleUsages_FindsAndRenamesAllReferences()
    {
        // Integration test verifying field reference finding with SyntaxTree identity
        // This test ensures the enhanced SymbolResolutionHelper maintains compilation context
        var sourceCode = @"
public class TestClass
{
    private int _oldField;

    public void Method()
    {
        var x = _oldField;
        var y = _oldField + 1;
        Console.WriteLine(_oldField);
    }
}";
        var refactoring = new RenameSymbol();

        // Act - Rename "_oldField" at line 4, column 17 (declaration)
        var result = refactoring.Execute(sourceCode, 4, 17, "_newField", "net8.0");

        // Assert - Should succeed and report correct reference count
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("4 references updated"); // Declaration + 3 usages

        // Assert - All occurrences should be renamed
        result.RefactoredCode.Should().Contain("private int _newField;");
        result.RefactoredCode.Should().Contain("var x = _newField;");
        result.RefactoredCode.Should().Contain("var y = _newField + 1;");
        result.RefactoredCode.Should().Contain("Console.WriteLine(_newField);");

        // Assert - Old name should be completely gone
        result.RefactoredCode.Should().NotContain("_oldField");
    }

    [Fact]
    public void Execute_WithThisQualifier_FindsAndRenamesAllReferences()
    {
        // Integration test verifying field renaming works with this. qualifier
        var sourceCode = @"
public class TestClass
{
    private int _value;

    public void Method()
    {
        this._value = 10;
        var x = this._value + 5;
        Console.WriteLine(this._value);
    }
}";
        var refactoring = new RenameSymbol();

        // Act - Rename "_value" at line 4, column 17 (declaration)
        var result = refactoring.Execute(sourceCode, 4, 17, "_newValue", "net8.0");

        // Assert - Should succeed
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("4 references updated");

        // Assert - All occurrences including this. qualifier should be renamed
        result.RefactoredCode.Should().Contain("private int _newValue;");
        result.RefactoredCode.Should().Contain("this._newValue = 10;");
        result.RefactoredCode.Should().Contain("var x = this._newValue + 5;");
        result.RefactoredCode.Should().Contain("Console.WriteLine(this._newValue);");

        // Assert - Old name should be gone
        result.RefactoredCode.Should().NotContain("_value");
    }

    [Fact]
    public void Execute_WithNestedScopes_RenamesOnlyTargetScope()
    {
        // Integration test verifying that renaming in nested scopes works correctly
        // (local variable shadowing a field name - they are different symbols)
        var sourceCode = @"
public class TestClass
{
    private int count = 0;

    public void Method()
    {
        int count = 5;  // Local variable shadows field
        Console.WriteLine(count);
        count = count + 1;
    }
}";
        var refactoring = new RenameSymbol();

        // Act - Rename local "count" at line 8, column 13 (local variable, not field)
        var result = refactoring.Execute(sourceCode, 8, 13, "localCount", "net8.0");

        // Assert - Should succeed
        result.IsSuccess.Should().BeTrue();

        // Assert - Only local variable should be renamed, field stays the same
        result.RefactoredCode.Should().Contain("private int count = 0;"); // Field unchanged
        result.RefactoredCode.Should().Contain("int localCount = 5;"); // Local renamed
        result.RefactoredCode.Should().Contain("Console.WriteLine(localCount);");
        result.RefactoredCode.Should().Contain("localCount = localCount + 1;");
    }

    [Fact]
    public void Execute_WithTypeConversions_FindsAllReferences()
    {
        // Integration test verifying renaming works with implicit/explicit type conversions
        var sourceCode = @"
public class TestClass
{
    public void Method()
    {
        int value = 42;
        long longValue = value;  // Implicit conversion
        double doubleValue = (double)value;  // Explicit conversion
        var result = value.ToString();
    }
}";
        var refactoring = new RenameSymbol();

        // Act - Rename "value" at line 6, column 13 (declaration)
        var result = refactoring.Execute(sourceCode, 6, 13, "number", "net8.0");

        // Assert - Should succeed and find all references
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("4 references updated");

        // Assert - All usages including conversions should be renamed
        result.RefactoredCode.Should().Contain("int number = 42;");
        result.RefactoredCode.Should().Contain("long longValue = number;");
        result.RefactoredCode.Should().Contain("double doubleValue = (double)number;");
        result.RefactoredCode.Should().Contain("var result = number.ToString();");

        // Assert - Old name should be gone
        result.RefactoredCode.Should().NotContain("int value");
        result.RefactoredCode.Should().NotContain("= value");
        result.RefactoredCode.Should().NotContain("value.");
    }

    // ============================================================================
    // Out of Scope Tests (should fail for V1)
    // ============================================================================

    [Fact]
    public void Execute_WithPublicMethod_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    public void PublicMethod()
    {
    }
}";
        var refactoring = new RenameSymbol();

        // Act
        var result = refactoring.Execute(sourceCode, 4, 17, "NewMethodName", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Only local variables, parameters, private fields, and private methods");
    }

    [Fact]
    public void Execute_WithProtectedField_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"
public class Test
{
    protected int _field;
}";
        var refactoring = new RenameSymbol();

        // Act
        var result = refactoring.Execute(sourceCode, 4, 19, "_newField", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Only local variables, parameters, private fields, and private methods");
    }

    // ============================================================================
    // Error Scenarios
    // ============================================================================

    [Fact]
    public void Execute_WithEmptySourceCode_ShouldReturnFailure()
    {
        // Arrange
        var refactoring = new RenameSymbol();

        // Act
        var result = refactoring.Execute("", 1, 1, "newName", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Source code cannot be empty");
    }

    [Fact]
    public void Execute_WithNullSourceCode_ShouldReturnFailure()
    {
        // Arrange
        var refactoring = new RenameSymbol();

        // Act
        var result = refactoring.Execute(null!, 1, 1, "newName", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Source code cannot be empty");
    }

    [Fact]
    public void Execute_WithSyntaxErrors_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = "public class Test {"; // Missing closing brace
        var refactoring = new RenameSymbol();

        // Act
        var result = refactoring.Execute(sourceCode, 1, 14, "NewTest", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Syntax errors");
    }

    // ============================================================================
    // Lambda Expression and Local Function Tests
    // ============================================================================

    [Fact]
    public void Execute_WithLambdaExpression_ShouldRename()
    {
        // Arrange - Simple lambda with single parameter
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        var items = new[] { 1, 2, 3 };
        var filtered = items.Where(x => x > 1);
    }
}";
        var refactoring = new RenameSymbol();

        // Act - Rename lambda parameter 'x' (single parameter in Where lambda)
        var result = refactoring.Execute(sourceCode, 7, 36, "item", "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue($"Error: {result.Message}");
        result.RefactoredCode.Should().Contain("item => item > 1");
    }

    [Fact]
    public void Execute_WithParenthesizedLambda_ShouldRename()
    {
        // Arrange - Lambda with multiple parameters
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        var items = new[] { 1, 2, 3 };
        var result = items.Select((x, i) => x + i);
    }
}";
        var refactoring = new RenameSymbol();

        // Act - Rename first lambda parameter 'x' (first parameter in Select lambda)
        var result = refactoring.Execute(sourceCode, 7, 36, "item", "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue($"Error: {result.Message}");
        result.RefactoredCode.Should().Contain("(item, i) => item + i");
        // Verify 'i' parameter remains unchanged
        result.RefactoredCode.Should().Contain("(item, i)");
    }

    [Fact]
    public void Execute_WithNestedLambdas_ShouldRenameInnerParameter()
    {
        // Arrange - Nested lambda expressions
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        var items = new[] { 1, 2, 3 };
        var result = items.Select(x => items.Where(y => y > x));
    }
}";
        var refactoring = new RenameSymbol();

        // Act - Rename inner lambda parameter 'y' (inner Where lambda parameter)
        var result = refactoring.Execute(sourceCode, 7, 52, "item", "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue($"Error: {result.Message}");
        result.RefactoredCode.Should().Contain("Where(item => item > x)");
        // Verify outer lambda parameter 'x' remains unchanged
        result.RefactoredCode.Should().Contain("Select(x =>");
    }

    [Fact]
    public void Execute_WithLambdaBlockBody_ShouldRename()
    {
        // Arrange - Lambda with block body
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        var items = new[] { 1, 2, 3 };
        var filtered = items.Where(x =>
        {
            return x > 1;
        });
    }
}";
        var refactoring = new RenameSymbol();

        // Act - Rename lambda parameter 'x' (Where lambda with block body)
        var result = refactoring.Execute(sourceCode, 7, 36, "value", "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue($"Error: {result.Message}");
        result.RefactoredCode.Should().Contain("Where(value =>");
        result.RefactoredCode.Should().Contain("return value > 1;");
    }

    [Fact]
    public void Execute_WithLambdaConflict_ShouldReturnFailure()
    {
        // Arrange - Lambda with local variable that conflicts with proposed name
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        var items = new[] { 1, 2, 3 };
        var filtered = items.Where(x =>
        {
            var item = x * 2;
            return item > 1;
        });
    }
}";
        var refactoring = new RenameSymbol();

        // Act - Attempt to rename lambda parameter 'x' to conflicting name 'item'
        var result = refactoring.Execute(sourceCode, 7, 36, "item", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("conflicts with existing symbols");
    }

    [Fact]
    public void Execute_WithAnonymousMethodExpression_ShouldRename()
    {
        // Arrange - Anonymous method (delegate expression)
        var sourceCode = @"
using System;
public class Test
{
    public void Method()
    {
        Func<int, bool> filter = delegate(int x) { return x > 1; };
    }
}";
        var refactoring = new RenameSymbol();

        // Act - Rename anonymous method parameter 'x' (delegate expression parameter)
        var result = refactoring.Execute(sourceCode, 7, 47, "value", "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue($"Error: {result.Message}");
        result.RefactoredCode.Should().Contain("delegate (int value)"); // Note: NormalizeWhitespace adds space
        result.RefactoredCode.Should().Contain("return value > 1;");
    }

    [Fact]
    public void Execute_WithNestedLambdas_ShouldRenameOuterParameter()
    {
        // Arrange - Nested lambda expressions with outer parameter used in inner lambda
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        var items = new[] { 1, 2, 3 };
        var result = items.Select(x => items.Where(y => y > x));
    }
}";
        var refactoring = new RenameSymbol();

        // Act - Rename outer lambda parameter 'x' (first parameter in Select lambda)
        var result = refactoring.Execute(sourceCode, 7, 35, "item", "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue($"Error: {result.Message}");
        result.RefactoredCode.Should().Contain("Select(item =>");
        result.RefactoredCode.Should().Contain("y > item"); // Inner lambda references renamed outer parameter
        // Verify inner lambda parameter 'y' remains unchanged
        result.RefactoredCode.Should().Contain("Where(y =>");
    }

    [Fact]
    public void Execute_WithLocalFunctionParameter_ShouldRename()
    {
        // Arrange - Local function with parameter
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        int LocalFunc(int x)
        {
            return x * 2;
        }
        var result = LocalFunc(5);
    }
}";
        var refactoring = new RenameSymbol();

        // Act - Rename local function parameter 'x' (parameter in local function)
        var result = refactoring.Execute(sourceCode, 6, 27, "value", "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue($"Error: {result.Message}");
        result.RefactoredCode.Should().Contain("int LocalFunc(int value)");
        result.RefactoredCode.Should().Contain("return value * 2;");
    }
}
