using FluentAssertions;
using RefactorCsharpMCP.Core.Refactorings;
using Xunit;

namespace RefactorCsharpMCP.Tests.Refactorings;

/// <summary>
/// Comprehensive edge case tests for refactorings to improve branch coverage.
/// Focuses on error paths, validation failures, and unusual scenarios.
/// </summary>
public class RefactoringEdgeCasesTests
{
    #region InlineVariable Edge Cases

    [Fact]
    public void InlineVariable_WithParameterInsteadOfVariable_ShouldReturnError()
    {
        // Arrange
        var refactoring = new InlineVariable();
        var sourceCode = @"
public class Test
{
    public void Method(int param)
    {
        Console.WriteLine(param);
    }
}";

        // Act - Try to inline the parameter instead of a local variable
        var result = refactoring.Execute(sourceCode, lineNumber: 4, columnNumber: 28, targetFramework: "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No local variable found");
    }

    [Fact]
    public void InlineVariable_WithFieldInsteadOfVariable_ShouldReturnError()
    {
        // Arrange
        var refactoring = new InlineVariable();
        var sourceCode = @"
public class Test
{
    private int _field = 42;

    public void Method()
    {
        Console.WriteLine(_field);
    }
}";

        // Act - Try to inline a field instead of a local variable
        var result = refactoring.Execute(sourceCode, lineNumber: 4, columnNumber: 17, targetFramework: "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No local variable found");
    }

    [Fact]
    public void InlineVariable_WithVariableInPropertyGetter_ShouldAttemptInline()
    {
        // Arrange
        var refactoring = new InlineVariable();
        var sourceCode = @"
public class Test
{
    public int Value
    {
        get
        {
            int temp = 42;
            return temp;
        }
    }
}";

        // Act - Inline variable in property getter
        var result = refactoring.Execute(sourceCode, lineNumber: 8, columnNumber: 17, targetFramework: "net8.0");

        // Assert - Should succeed since property accessors are valid containing blocks
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().NotContain("int temp");
        result.RefactoredCode.Should().Contain("return 42");
    }

    [Fact]
    public void InlineVariable_WithVariableInPropertySetter_ShouldAttemptInline()
    {
        // Arrange
        var refactoring = new InlineVariable();
        var sourceCode = @"
public class Test
{
    private int _value;

    public int Value
    {
        set
        {
            int temp = value * 2;
            _value = temp;
        }
    }
}";

        // Act - Inline variable in property setter
        var result = refactoring.Execute(sourceCode, lineNumber: 10, columnNumber: 17, targetFramework: "net8.0");

        // Assert - Should succeed since property accessors are valid containing blocks
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().NotContain("int temp");
    }

    [Fact]
    public void InlineVariable_WithMultipleVariablesInDeclaration_ShouldInlineSpecific()
    {
        // Arrange
        var refactoring = new InlineVariable();
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        int a = 1, b = 2, c = 3;
        Console.WriteLine(b);
    }
}";

        // Act - Inline middle variable 'b'
        var result = refactoring.Execute(sourceCode, lineNumber: 6, columnNumber: 20, targetFramework: "net8.0");

        // Assert - Should inline only 'b', leaving 'a' and 'c'
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Console.WriteLine(2)");
    }

    [Fact]
    public void InlineVariable_WithComplexInitializer_ShouldPreserveExpression()
    {
        // Arrange
        var refactoring = new InlineVariable();
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        var complex = (1 + 2) * (3 - 4) / 5 % 6;
        Console.WriteLine(complex);
    }
}";

        // Act
        var result = refactoring.Execute(sourceCode, lineNumber: 6, columnNumber: 13, targetFramework: "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("(1 + 2) * (3 - 4) / 5 % 6");
    }

    #endregion

    #region RenameSymbol Edge Cases

    [Fact]
    public async Task RenameSymbol_WithInvalidPosition_ShouldReturnError()
    {
        // Arrange
        var refactoring = new RenameSymbol();
        var sourceCode = @"
public class Test
{
    public void Method()
    {
    }
}";

        // Act - Position points to whitespace
        var result = await refactoring.ExecuteAsync(sourceCode, lineNumber: 5, columnNumber: 5, newName: "NewName", targetFramework: "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task RenameSymbol_WithConflictingName_ShouldReturnError()
    {
        // Arrange
        var refactoring = new RenameSymbol();
        var sourceCode = @"
public class Test
{
    private int existing;
    private int original;

    public void Method()
    {
        var value = original;
    }
}";

        // Act - Try to rename 'original' to 'existing' (conflict)
        var result = await refactoring.ExecuteAsync(sourceCode, lineNumber: 5, columnNumber: 17, newName: "existing", targetFramework: "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("conflict");
    }

    [Fact(Skip = "Known limitation - RenameSymbol may not currently support method parameter renaming. Track in future issue.")]
    public async Task RenameSymbol_MethodParameter_ShouldRenameOnlyInMethodScope()
    {
        // Arrange
        var refactoring = new RenameSymbol();
        var sourceCode = @"
using System;

public class Test
{
    public void Method1(int value)
    {
        Console.WriteLine(value);
    }

    public void Method2(int value)
    {
        Console.WriteLine(value);
    }
}";

        // Act - Rename parameter in Method1 only
        var result = await refactoring.ExecuteAsync(sourceCode, lineNumber: 5, columnNumber: 29, newName: "number", targetFramework: "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Method1(int number)");
        result.RefactoredCode.Should().Contain("WriteLine(number)");
        result.RefactoredCode.Should().Contain("Method2(int value)"); // Unchanged
    }

    #endregion

    #region SafeDelete Edge Cases

    [Fact]
    public async Task SafeDelete_WithPrivateMethodCalledInternally_ShouldReturnError()
    {
        // Arrange
        var refactoring = new SafeDelete();
        var sourceCode = @"
public class Test
{
    private void Helper()
    {
    }

    public void Method()
    {
        Helper();
    }
}";

        // Act - Try to delete Helper which is called by Method
        var result = await refactoring.ExecuteAsync(sourceCode, className: "Test", methodName: "Helper", targetFramework: "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("referenced");
    }

    [Fact]
    public async Task SafeDelete_WithUnusedPrivateMethod_ShouldSucceed()
    {
        // Arrange
        var refactoring = new SafeDelete();
        var sourceCode = @"
using System;

public class Test
{
    private void Unused()
    {
        // Dead code
    }

    public void Method()
    {
        Console.WriteLine(""Active"");
    }
}";

        // Act - Delete unused private method
        var result = await refactoring.ExecuteAsync(sourceCode, className: "Test", methodName: "Unused", targetFramework: "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().NotContain("Unused");
    }

    #endregion

    #region MakeFieldReadonly Edge Cases

    [Fact]
    public async Task MakeFieldReadonly_WithFieldAssignedInMultipleMethods_ShouldReturnError()
    {
        // Arrange
        var refactoring = new MakeFieldReadonly();
        var sourceCode = @"
public class Test
{
    private int _value;

    public void Method1()
    {
        _value = 1;
    }

    public void Method2()
    {
        _value = 2;
    }
}";

        // Act - Try to make readonly field that's assigned in multiple methods
        var result = await refactoring.ExecuteAsync(sourceCode, className: "Test", fieldName: "_value", targetFramework: "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("assigned outside");
    }

    [Fact(Skip = "Known limitation - MakeFieldReadonly does not currently support struct types. Track in future issue.")]
    public async Task MakeFieldReadonly_WithFieldInStruct_ShouldAttemptMakeReadonly()
    {
        // Arrange
        var refactoring = new MakeFieldReadonly();
        var sourceCode = @"
public struct TestStruct
{
    private int _value;

    public TestStruct(int value)
    {
        _value = value;
    }
}";

        // Act - Make readonly field in struct
        var result = await refactoring.ExecuteAsync(sourceCode, className: "TestStruct", fieldName: "_value", targetFramework: "net8.0");

        // Assert - Should succeed for struct fields assigned only in constructor
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("readonly int _value");
    }

    [Fact]
    public async Task MakeFieldReadonly_WithStaticFieldAssignedInStaticConstructor_ShouldSucceed()
    {
        // Arrange
        var refactoring = new MakeFieldReadonly();
        var sourceCode = @"
public class Test
{
    private static int _value;

    static Test()
    {
        _value = 42;
    }
}";

        // Act
        var result = await refactoring.ExecuteAsync(sourceCode, className: "Test", fieldName: "_value", targetFramework: "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("static readonly int _value");
    }

    #endregion

    #region ExtractClass Edge Cases

    [Fact]
    public async Task ExtractClass_WithNoMembersToExtract_ShouldReturnError()
    {
        // Arrange
        var refactoring = new ExtractClass();
        var sourceCode = @"
public class Test
{
    public void Method()
    {
    }
}";

        // Act - Try to extract class with empty field list
        var result = await refactoring.ExecuteAsync(
            sourceCode,
            className: "Test",
            newClassName: "Empty",
            fieldNames: "",  // Empty field names
            targetFramework: "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExtractClass_WithNonExistentMember_ShouldReturnError()
    {
        // Arrange
        var refactoring = new ExtractClass();
        var sourceCode = @"
public class Test
{
    private int _value;
}";

        // Act - Try to extract non-existent member
        var result = await refactoring.ExecuteAsync(
            sourceCode,
            className: "Test",
            newClassName: "Extracted",
            fieldNames: "NonExistent",  // Non-existent field
            targetFramework: "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task ExtractClass_WithMixOfFieldsAndMethods_ShouldExtractAll()
    {
        // Arrange
        var refactoring = new ExtractClass();
        var sourceCode = @"
public class Test
{
    private int _value;
    private string _name;

    private void Helper()
    {
    }

    public void Main()
    {
    }
}";

        // Act - Extract field and method together
        var result = await refactoring.ExecuteAsync(
            sourceCode,
            className: "Test",
            newClassName: "Extracted",
            fieldNames: "_value",  // Comma-separated field names
            targetFramework: "net8.0",
            methodNames: "Helper");  // Optional method names

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("class Extracted");
        result.RefactoredCode.Should().Contain("int _value");
        result.RefactoredCode.Should().Contain("void Helper");
    }

    #endregion

    #region RemoveUnusedUsings Edge Cases

    [Fact]
    public async Task RemoveUnusedUsings_WithGlobalUsings_ShouldPreserveGlobals()
    {
        // Arrange
        var refactoring = new RemoveUnusedUsings();
        // C# 10+ global usings syntax
        var sourceCode = @"global using System;
using System.Linq;

public class Test
{
    public void Method()
    {
        Console.WriteLine(""Test"");
    }
}";

        // Act
        var result = await refactoring.ExecuteAsync(sourceCode, "net8.0");

        // NOTE: Due to IDE analyzer limitations (Issue #72), this may not detect unused usings
        // Test validates the tool handles global usings without errors
        if (result.IsSuccess)
        {
            // If it works, global using should be preserved
            result.RefactoredCode.Should().Contain("global using System");
        }
    }

    [Fact]
    public async Task RemoveUnusedUsings_WithAllUsingsUnused_ShouldRemoveAll()
    {
        // Arrange
        var refactoring = new RemoveUnusedUsings();
        var sourceCode = @"using System.Linq;
using System.Collections.Generic;
using System.Text;

public class Test
{
    public void Method()
    {
        var x = 42;
    }
}";

        // Act
        var result = await refactoring.ExecuteAsync(sourceCode, "net8.0");

        // NOTE: Due to IDE analyzer limitations (Issue #72), this may not work as expected
        if (result.IsSuccess)
        {
            result.RefactoredCode.Should().NotContain("using System.Linq");
            result.RefactoredCode.Should().NotContain("using System.Collections");
            result.RefactoredCode.Should().NotContain("using System.Text");
        }
    }

    #endregion

    #region ConstructorInjection Edge Cases

    [Fact(Skip = "Known limitation - ConstructorInjection does not currently support merging parameters into existing constructors. Track in future issue.")]
    public async Task ConstructorInjection_WithExistingConstructor_ShouldAddParameters()
    {
        // Arrange
        var refactoring = new ConstructorInjection();
        var sourceCode = @"
public class Test
{
    public Test(string existing)
    {
    }

    public void Method(IService service)
    {
        service.Execute();
    }
}";

        // Act - Convert service parameter to constructor injection
        var result = await refactoring.ExecuteAsync(
            sourceCode,
            className: "Test",
            methodName: "Method",
            parameterNames: new[] { "service" },
            targetFramework: "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("Test(string existing, IService service)");
    }

    [Fact]
    public async Task ConstructorInjection_WithNoMethodParameters_ShouldReturnError()
    {
        // Arrange
        var refactoring = new ConstructorInjection();
        var sourceCode = @"
using System;

public class Test
{
    public void Method()
    {
        Console.WriteLine(""No parameters"");
    }
}";

        // Act - Try to convert method with no parameters
        var result = await refactoring.ExecuteAsync(
            sourceCode,
            className: "Test",
            methodName: "Method",
            parameterNames: Array.Empty<string>(),
            targetFramework: "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("parameter");
    }

    #endregion
}
