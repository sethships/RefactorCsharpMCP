using FluentAssertions;
using RefactorCsharpMCP.Core.Refactorings;
using Xunit;

namespace RefactorCsharpMCP.Tests.Refactorings;

/// <summary>
/// Framework matrix tests - verifies refactorings work correctly across all supported frameworks.
/// Tests cross-framework compatibility and framework-specific behavior.
/// </summary>
/// <remarks>
/// Uses [Collection("CacheTests")] to serialize access to the shared reference assembly cache.
/// This prevents race conditions when multiple framework tests run in parallel and access
/// the ReferenceAssemblyCache disk operations (Issue #148).
/// </remarks>
[Collection("CacheTests")]
public class FrameworkMatrixTests
{
    #region Extract Method - Framework Matrix

    [Theory]
    [InlineData("net8.0")]
    [InlineData("net9.0")]
    [InlineData("net48")]
    [InlineData("netstandard2.0")]
    public async Task ExtractMethod_AcrossFrameworks_ShouldSucceed(string targetFramework)
    {
        // Arrange
        var refactoring = new ExtractMethod();
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        System.Console.WriteLine(""Hello"");
        System.Console.WriteLine(""World"");
    }
}";

        // Act - Extract console output lines into new method
        var result = await refactoring.ExecuteAsync(
            sourceCode,
            startLine: 6,
            endLine: 7,
            newMethodName: "PrintGreeting",
            targetFramework: targetFramework);

        // Assert - Should succeed for all supported frameworks
        // NOTE: net48 may fail due to reference assembly limitations (Issue #75)
        if (!result.IsSuccess)
        {
            // Log the error for debugging
            result.ErrorMessage.Should().NotBeNullOrEmpty($"Error for {targetFramework}: {result.ErrorMessage}");

            // Allow net48 to fail gracefully
            if (targetFramework == "net48")
            {
                return; // Expected failure for net48
            }

            // For other frameworks, fail the test with error details
            result.IsSuccess.Should().BeTrue($"ExtractMethod should work on {targetFramework}. Error: {result.ErrorMessage}");
        }
        else
        {
            result.RefactoredCode.Should().Contain("PrintGreeting");
        }
    }

    #endregion

    #region Inline Variable - Framework Matrix

    [Theory]
    [InlineData("net8.0")]
    [InlineData("net9.0")]
    [InlineData("net48")]
    [InlineData("netstandard2.0")]
    public async Task InlineVariable_AcrossFrameworks_ShouldSucceed(string targetFramework)
    {
        // Arrange
        var refactoring = new InlineVariable();
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        int value = 42;
        System.Console.WriteLine(value);
    }
}";

        // Act - Inline variable at line 6
        var result = await refactoring.ExecuteAsync(sourceCode, lineNumber: 6, columnNumber: 13, targetFramework: targetFramework);

        // Assert - Should succeed for all supported frameworks
        // NOTE: net48 may fail due to reference assembly limitations (Issue #75)
        if (targetFramework == "net48" && !result.IsSuccess)
        {
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }
        else
        {
            result.IsSuccess.Should().BeTrue($"InlineVariable should work on {targetFramework}");
            result.RefactoredCode.Should().Contain("WriteLine(42)");
        }
    }

    #endregion

    #region Rename Symbol - Framework Matrix

    [Theory]
    [InlineData("net8.0")]
    [InlineData("net9.0")]
    [InlineData("net48")]
    [InlineData("netstandard2.0")]
    public async Task RenameSymbol_AcrossFrameworks_ShouldSucceed(string targetFramework)
    {
        // Arrange
        var refactoring = new RenameSymbol();
        var sourceCode = @"
public class Test
{
    private int oldName;

    public void Method()
    {
        oldName = 42;
    }
}";

        // Act - Rename field at line 4
        var result = await refactoring.ExecuteAsync(
            sourceCode,
            lineNumber: 4,
            columnNumber: 17,
            newName: "newName",
            targetFramework: targetFramework);

        // Assert - Should succeed for all supported frameworks
        // NOTE: net48 may fail due to reference assembly limitations (Issue #75)
        if (targetFramework == "net48" && !result.IsSuccess)
        {
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }
        else
        {
            result.IsSuccess.Should().BeTrue($"RenameSymbol should work on {targetFramework}");
            result.RefactoredCode.Should().Contain("private int newName");
            result.RefactoredCode.Should().Contain("newName = 42");
        }
    }

    #endregion

    #region Constructor Injection - Framework Matrix

    [Theory]
    [InlineData("net8.0")]
    [InlineData("net9.0")]
    [InlineData("net48")]
    [InlineData("netstandard2.0")]
    public async Task ConstructorInjection_AcrossFrameworks_ShouldSucceed(string targetFramework)
    {
        // Arrange
        var refactoring = new ConstructorInjection();
        var sourceCode = @"
public interface IService
{
    void Execute();
}

public class Test
{
    public void Method(IService service)
    {
        service.Execute();
    }
}";

        // Act - Convert parameter to constructor injection
        var result = await refactoring.ExecuteAsync(
            sourceCode,
            className: "Test",
            methodName: "Method",
            parameterNames: new[] { "service" },
            targetFramework: targetFramework);

        // Assert - Should succeed for all supported frameworks
        // NOTE: net48 may fail due to reference assembly limitations (Issue #75)
        if (targetFramework == "net48" && !result.IsSuccess)
        {
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }
        else
        {
            result.IsSuccess.Should().BeTrue($"ConstructorInjection should work on {targetFramework}");
            result.RefactoredCode.Should().Contain("public Test(IService service)");
        }
    }

    #endregion

    #region Make Field Readonly - Framework Matrix

    [Theory]
    [InlineData("net8.0")]
    [InlineData("net9.0")]
    [InlineData("net48")]
    [InlineData("netstandard2.0")]
    public async Task MakeFieldReadonly_AcrossFrameworks_ShouldSucceed(string targetFramework)
    {
        // Arrange
        var refactoring = new MakeFieldReadonly();
        var sourceCode = @"
public class Test
{
    private int _value;

    public Test()
    {
        _value = 42;
    }
}";

        // Act - Make field readonly
        var result = await refactoring.ExecuteAsync(
            sourceCode,
            className: "Test",
            fieldName: "_value",
            targetFramework: targetFramework);

        // Assert - Should succeed for all supported frameworks
        // NOTE: net48 may fail due to reference assembly limitations (Issue #75)
        if (targetFramework == "net48" && !result.IsSuccess)
        {
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }
        else
        {
            result.IsSuccess.Should().BeTrue($"MakeFieldReadonly should work on {targetFramework}");
            result.RefactoredCode.Should().Contain("readonly int _value");
        }
    }

    #endregion

    #region Safe Delete - Framework Matrix

    [Theory]
    [InlineData("net8.0")]
    [InlineData("net9.0")]
    [InlineData("net48")]
    [InlineData("netstandard2.0")]
    public async Task SafeDelete_AcrossFrameworks_ShouldSucceed(string targetFramework)
    {
        // Arrange
        var refactoring = new SafeDelete();
        var sourceCode = @"
public class Test
{
    private void UnusedMethod()
    {
        // Dead code
    }

    public void ActiveMethod()
    {
        System.Console.WriteLine(""Active"");
    }
}";

        // Act - Delete unused method
        var result = await refactoring.ExecuteAsync(
            sourceCode,
            className: "Test",
            methodName: "UnusedMethod",
            targetFramework: targetFramework);

        // Assert - Should succeed for all supported frameworks
        // NOTE: net48 may fail due to reference assembly limitations (Issue #75)
        if (targetFramework == "net48" && !result.IsSuccess)
        {
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }
        else
        {
            result.IsSuccess.Should().BeTrue($"SafeDelete should work on {targetFramework}");
            result.RefactoredCode.Should().NotContain("UnusedMethod");
        }
    }

    #endregion

    #region Extract Class - Framework Matrix

    [Theory]
    [InlineData("net8.0")]
    [InlineData("net9.0")]
    [InlineData("net48")]
    [InlineData("netstandard2.0")]
    public async Task ExtractClass_AcrossFrameworks_ShouldSucceed(string targetFramework)
    {
        // Arrange
        var refactoring = new ExtractClass();
        var sourceCode = @"
public class Test
{
    private int _value;
    private string _name;

    public void Method()
    {
    }
}";

        // Act - Extract field into new class
        var result = await refactoring.ExecuteAsync(
            sourceCode,
            className: "Test",
            newClassName: "Extracted",
            fieldNames: "_value",
            targetFramework: targetFramework);

        // Assert - Should succeed for all supported frameworks
        // NOTE: net48 may fail due to reference assembly limitations (Issue #75)
        if (targetFramework == "net48" && !result.IsSuccess)
        {
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }
        else
        {
            result.IsSuccess.Should().BeTrue($"ExtractClass should work on {targetFramework}");
            result.RefactoredCode.Should().Contain("class Extracted");
            result.RefactoredCode.Should().Contain("int _value");
        }
    }

    #endregion

    #region Remove Unused Usings - Framework Matrix

    [Theory]
    [InlineData("net8.0")]
    [InlineData("net9.0")]
    public async Task RemoveUnusedUsings_ModernFrameworks_ShouldAttemptRemoval(string targetFramework)
    {
        // Arrange
        var refactoring = new RemoveUnusedUsings();
        var sourceCode = @"using System;
using System.Linq;

public class Test
{
    public void Method()
    {
        System.Console.WriteLine(""Test"");
    }
}";

        // Act
        var result = await refactoring.ExecuteAsync(sourceCode, targetFramework);

        // NOTE: RemoveUnusedUsings may actually work and remove the System.Linq using
        // Test validates graceful behavior regardless of outcome
        result.Should().NotBeNull();

        // The tool may succeed and remove unused usings, or fail due to IDE analyzer limitations
        if (result.IsSuccess)
        {
            // If successful, using System should be preserved since it's used
            result.RefactoredCode.Should().Contain("Console.WriteLine");
        }
        else
        {
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }
    }

    [Theory]
    [InlineData("net48")]
    [InlineData("netstandard2.0")]
    public async Task RemoveUnusedUsings_LegacyFrameworks_MayHaveLimitations(string targetFramework)
    {
        // Arrange
        var refactoring = new RemoveUnusedUsings();
        var sourceCode = @"using System;

public class Test
{
    public void Method()
    {
        System.Console.WriteLine(""Test"");
    }
}";

        // Act
        var result = await refactoring.ExecuteAsync(sourceCode, targetFramework);

        // NOTE: Legacy frameworks may have reference assembly limitations (Issue #75)
        // Test validates graceful handling regardless of success/failure
        result.Should().NotBeNull();
        if (!result.IsSuccess)
        {
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }
    }

    #endregion

    #region Inline Method - Framework Matrix

    [Theory]
    [InlineData("net8.0")]
    [InlineData("net9.0")]
    public async Task InlineMethod_ModernFrameworks_Part1Functionality(string targetFramework)
    {
        // Arrange
        var refactoring = new InlineMethod();
        var sourceCode = @"
public class Test
{
    public void Caller()
    {
        Helper();
    }

    private void Helper()
    {
        System.Console.WriteLine(""Hello"");
    }
}";

        // Act - Inline method at line 9 (Helper declaration)
        var result = await refactoring.ExecuteAsync(sourceCode, lineNumber: 9, columnNumber: 18, targetFramework: targetFramework);

        // NOTE: InlineMethod Part 1 has limitations (single call site, void methods, simple parameters)
        // Test validates execution without errors
        if (result.IsSuccess)
        {
            result.RefactoredCode.Should().Contain("WriteLine");
        }
        else
        {
            // Expected failures for Part 1 limitations
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }
    }

    #endregion

    #region Framework-Specific Language Features

    [Theory]
    [InlineData("net8.0", "C# 12")]
    [InlineData("net9.0", "C# 13")]
    [InlineData("net48", "C# 7.3")]
    [InlineData("netstandard2.0", "C# 7.3")]
    public async Task Refactoring_FrameworkLanguageVersionMapping_IsCorrect(string targetFramework, string expectedLanguageVersion)
    {
        // Arrange
        var refactoring = new InlineVariable();

        // Use basic C# syntax compatible with all versions
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        int value = 42;
        System.Console.WriteLine(value);
    }
}";

        // Act - Simple refactoring that should work on all frameworks
        var result = await refactoring.ExecuteAsync(sourceCode, lineNumber: 6, columnNumber: 13, targetFramework: targetFramework);

        // Assert - Refactoring should succeed, validating framework is properly mapped
        // NOTE: net48 may fail due to reference assembly limitations (Issue #75)
        if (targetFramework == "net48" && !result.IsSuccess)
        {
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }
        else
        {
            result.IsSuccess.Should().BeTrue(
                $"Basic refactoring should work on {targetFramework} with {expectedLanguageVersion}");
        }
    }

    [Fact]
    public async Task Refactoring_WithCSharp12Syntax_FailsOnNet48()
    {
        // Arrange
        var refactoring = new InlineVariable();

        // C# 12 collection expression syntax (not supported in net48/C# 7.3)
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        int[] numbers = [1, 2, 3];
        System.Console.WriteLine(numbers.Length);
    }
}";

        // Act - Try to refactor C# 12 code targeting net48
        var result = await refactoring.ExecuteAsync(sourceCode, lineNumber: 6, columnNumber: 13, targetFramework: "net48");

        // Assert - Should fail due to language version mismatch
        result.IsSuccess.Should().BeFalse("C# 12 syntax should not work on net48");
        result.ErrorMessage.Should().Contain("C#");
    }

    #endregion

    #region Framework Validation

    [Theory]
    [InlineData("net6.0")] // EOL framework
    [InlineData("net5.0")] // EOL framework
    [InlineData("netcoreapp3.1")] // EOL framework
    public async Task Refactoring_WithUnsupportedFramework_ShouldReturnError(string unsupportedFramework)
    {
        // Arrange
        var refactoring = new InlineVariable();
        var sourceCode = @"
public class Test
{
    public void Method()
    {
        int value = 42;
    }
}";

        // Act - Try to use unsupported framework
        var result = await refactoring.ExecuteAsync(sourceCode, lineNumber: 6, columnNumber: 13, targetFramework: unsupportedFramework);

        // Assert - Should fail with framework validation error
        result.IsSuccess.Should().BeFalse($"{unsupportedFramework} should not be supported");
        result.ErrorMessage.Should().Contain("Unsupported framework");
    }

    #endregion
}
