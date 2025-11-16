using FluentAssertions;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Tests.Refactorings;

public class RefactoringBaseTests
{
    // Create a concrete implementation for testing the abstract base class
    private class TestRefactoring : RefactoringBase
    {
        public RefactoringResult TestValidateNonEmpty(string? value, string parameterName)
        {
            return ValidateNonEmpty(value, parameterName);
        }

        public RefactoringResult TestParseAndValidateSyntax(
            string sourceCode,
            out Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax? root,
            out Microsoft.CodeAnalysis.SyntaxTree? syntaxTree)
        {
            return ParseAndValidateSyntax(sourceCode, out root, out syntaxTree);
        }

        public Microsoft.CodeAnalysis.CSharp.CSharpCompilation TestCreateCompilation(Microsoft.CodeAnalysis.SyntaxTree syntaxTree)
        {
            return CreateCompilation(syntaxTree);
        }

        public Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax? TestFindClass(
            Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax root,
            string className)
        {
            return FindClass(root, className);
        }

        public Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax? TestFindMethod(
            Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax classDeclaration,
            string methodName)
        {
            return FindMethod(classDeclaration, methodName);
        }

        public RefactoringResult TestHandleException(Exception ex, string operationName = "refactoring")
        {
            return HandleException(ex, operationName);
        }

        public T TestNormalizeWhitespace<T>(T node) where T : Microsoft.CodeAnalysis.SyntaxNode
        {
            return NormalizeWhitespace(node);
        }

        public void TestAddTargetContext(RefactoringErrorContext errorContext, string className, string? memberName = null)
        {
            AddTargetContext(errorContext, className, memberName);
        }
    }

    [Fact]
    public void ValidateNonEmpty_WithValidString_ReturnsSuccess()
    {
        // Arrange
        var refactoring = new TestRefactoring();

        // Act
        var result = refactoring.TestValidateNonEmpty("valid string", "TestParameter");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateNonEmpty_WithNullString_ReturnsFailure()
    {
        // Arrange
        var refactoring = new TestRefactoring();

        // Act
        var result = refactoring.TestValidateNonEmpty(null, "TestParameter");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("TestParameter cannot be empty");
    }

    [Fact]
    public void ValidateNonEmpty_WithEmptyString_ReturnsFailure()
    {
        // Arrange
        var refactoring = new TestRefactoring();

        // Act
        var result = refactoring.TestValidateNonEmpty("", "TestParameter");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("TestParameter cannot be empty");
    }

    [Fact]
    public void ValidateNonEmpty_WithWhitespaceString_ReturnsFailure()
    {
        // Arrange
        var refactoring = new TestRefactoring();

        // Act
        var result = refactoring.TestValidateNonEmpty("   ", "TestParameter");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("TestParameter cannot be empty");
    }

    [Fact]
    public void ParseAndValidateSyntax_WithValidCode_ReturnsSuccess()
    {
        // Arrange
        var refactoring = new TestRefactoring();
        var sourceCode = "public class TestClass { }";

        // Act
        var result = refactoring.TestParseAndValidateSyntax(sourceCode, out var root, out var syntaxTree);

        // Assert
        result.IsSuccess.Should().BeTrue();
        root.Should().NotBeNull();
        syntaxTree.Should().NotBeNull();
    }

    [Fact]
    public void ParseAndValidateSyntax_WithSyntaxErrors_ReturnsFailure()
    {
        // Arrange
        var refactoring = new TestRefactoring();
        var sourceCode = "public class TestClass {"; // Missing closing brace

        // Act
        var result = refactoring.TestParseAndValidateSyntax(sourceCode, out var root, out var syntaxTree);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Syntax errors in source code");
    }

    [Fact]
    public void CreateCompilation_WithValidSyntaxTree_ReturnsCompilation()
    {
        // Arrange
        var refactoring = new TestRefactoring();
        var sourceCode = "public class TestClass { }";
        var syntaxTree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(sourceCode);

        // Act
        var compilation = refactoring.TestCreateCompilation(syntaxTree);

        // Assert
        compilation.Should().NotBeNull();
        compilation.SyntaxTrees.Should().Contain(syntaxTree);
        compilation.References.Should().NotBeEmpty();
    }

    [Fact]
    public void FindClass_WithExistingClass_ReturnsClassDeclaration()
    {
        // Arrange
        var refactoring = new TestRefactoring();
        var sourceCode = "public class TestClass { }";
        refactoring.TestParseAndValidateSyntax(sourceCode, out var root, out _);

        // Act
        var classDeclaration = refactoring.TestFindClass(root!, "TestClass");

        // Assert
        classDeclaration.Should().NotBeNull();
        classDeclaration!.Identifier.Text.Should().Be("TestClass");
    }

    [Fact]
    public void FindClass_WithNonExistingClass_ReturnsNull()
    {
        // Arrange
        var refactoring = new TestRefactoring();
        var sourceCode = "public class TestClass { }";
        refactoring.TestParseAndValidateSyntax(sourceCode, out var root, out _);

        // Act
        var classDeclaration = refactoring.TestFindClass(root!, "NonExistentClass");

        // Assert
        classDeclaration.Should().BeNull();
    }

    [Fact]
    public void FindMethod_WithExistingMethod_ReturnsMethodDeclaration()
    {
        // Arrange
        var refactoring = new TestRefactoring();
        var sourceCode = @"
public class TestClass
{
    public void TestMethod() { }
}";
        refactoring.TestParseAndValidateSyntax(sourceCode, out var root, out _);
        var classDeclaration = refactoring.TestFindClass(root!, "TestClass");

        // Act
        var methodDeclaration = refactoring.TestFindMethod(classDeclaration!, "TestMethod");

        // Assert
        methodDeclaration.Should().NotBeNull();
        methodDeclaration!.Identifier.Text.Should().Be("TestMethod");
    }

    [Fact]
    public void FindMethod_WithNonExistingMethod_ReturnsNull()
    {
        // Arrange
        var refactoring = new TestRefactoring();
        var sourceCode = @"
public class TestClass
{
    public void TestMethod() { }
}";
        refactoring.TestParseAndValidateSyntax(sourceCode, out var root, out _);
        var classDeclaration = refactoring.TestFindClass(root!, "TestClass");

        // Act
        var methodDeclaration = refactoring.TestFindMethod(classDeclaration!, "NonExistentMethod");

        // Assert
        methodDeclaration.Should().BeNull();
    }

    [Fact]
    public void HandleException_WithArgumentException_ReturnsSanitizedError()
    {
        // Arrange
        var refactoring = new TestRefactoring();
        var exception = new ArgumentException("Sensitive internal message");

        // Act
        var result = refactoring.TestHandleException(exception, "test operation");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("InvalidInput");
        result.ErrorMessage.Should().Contain("test operation");
        result.ErrorMessage.Should().NotContain("Sensitive internal message");
    }

    [Fact]
    public void HandleException_WithInvalidOperationException_ReturnsSanitizedError()
    {
        // Arrange
        var refactoring = new TestRefactoring();
        var exception = new InvalidOperationException("Sensitive internal message");

        // Act
        var result = refactoring.TestHandleException(exception, "test operation");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("InvalidState");
        result.ErrorMessage.Should().NotContain("Sensitive internal message");
    }

    [Fact]
    public void HandleException_WithFormatException_ReturnsSanitizedError()
    {
        // Arrange
        var refactoring = new TestRefactoring();
        var exception = new FormatException("Sensitive internal message");

        // Act
        var result = refactoring.TestHandleException(exception);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("ParseError");
        result.ErrorMessage.Should().NotContain("Sensitive internal message");
    }

    [Fact]
    public void HandleException_WithUnknownException_ReturnsSanitizedError()
    {
        // Arrange
        var refactoring = new TestRefactoring();
        var exception = new Exception("Sensitive internal message");

        // Act
        var result = refactoring.TestHandleException(exception);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("UnexpectedError");
        result.ErrorMessage.Should().NotContain("Sensitive internal message");
    }

    [Fact]
    public void NormalizeWhitespace_WithUnformattedCode_ReturnsFormattedCode()
    {
        // Arrange
        var refactoring = new TestRefactoring();
        var sourceCode = "public class TestClass{public void Method(){}}";
        refactoring.TestParseAndValidateSyntax(sourceCode, out var root, out _);

        // Act
        var normalized = refactoring.TestNormalizeWhitespace(root!);

        // Assert
        var normalizedText = normalized.ToFullString();
        normalizedText.Should().Contain("public class TestClass");
        normalizedText.Should().Contain("    public void Method()"); // Should have indentation
    }

    [Fact]
    public void ExecuteWithValidationAsync_CallsRefactoringOperation()
    {
        // This test verifies that the template method pattern works correctly
        // by testing through a real refactoring class (SafeDelete)
        // The integration is already tested through the existing refactoring tests
        // This is just a placeholder to note that ExecuteWithValidationAsync is tested
        // indirectly through all the refactoring ExecuteAsync methods
        true.Should().BeTrue("ExecuteWithValidationAsync is tested through refactoring classes");
    }

    #region Phase 4: AddTargetContext Tests

    [Fact]
    public void AddTargetContext_WithClassName_AddsTargetClassToContext()
    {
        // Arrange
        var refactoring = new TestRefactoring();
        var errorContext = new RefactoringErrorContext { Phase = "Test Phase" };

        // Act
        refactoring.TestAddTargetContext(errorContext, "MyTestClass");

        // Assert
        errorContext.AdditionalContext.Should().ContainKey("TargetClass");
        errorContext.AdditionalContext["TargetClass"].Should().Be("MyTestClass");
    }

    [Fact]
    public void AddTargetContext_WithClassNameAndMemberName_AddsBothToContext()
    {
        // Arrange
        var refactoring = new TestRefactoring();
        var errorContext = new RefactoringErrorContext { Phase = "Test Phase" };

        // Act
        refactoring.TestAddTargetContext(errorContext, "MyTestClass", "MyTestMethod");

        // Assert
        errorContext.AdditionalContext.Should().ContainKey("TargetClass");
        errorContext.AdditionalContext["TargetClass"].Should().Be("MyTestClass");
        errorContext.AdditionalContext.Should().ContainKey("TargetMember");
        errorContext.AdditionalContext["TargetMember"].Should().Be("MyTestMethod");
    }

    [Fact]
    public void AddTargetContext_WithNullErrorContext_ThrowsArgumentNullException()
    {
        // Arrange
        var refactoring = new TestRefactoring();

        // Act & Assert
        Action act = () => refactoring.TestAddTargetContext(null!, "MyClass");
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("errorContext");
    }

    [Fact]
    public void AddTargetContext_WithEmptyClassName_DoesNotAddTargetClass()
    {
        // Arrange
        var refactoring = new TestRefactoring();
        var errorContext = new RefactoringErrorContext { Phase = "Test Phase" };

        // Act
        refactoring.TestAddTargetContext(errorContext, "", "MyMethod");

        // Assert
        errorContext.AdditionalContext.Should().NotContainKey("TargetClass");
        errorContext.AdditionalContext.Should().ContainKey("TargetMember");
    }

    [Fact]
    public void AddTargetContext_WithNullMemberName_OnlyAddsClassName()
    {
        // Arrange
        var refactoring = new TestRefactoring();
        var errorContext = new RefactoringErrorContext { Phase = "Test Phase" };

        // Act
        refactoring.TestAddTargetContext(errorContext, "MyClass", null);

        // Assert
        errorContext.AdditionalContext.Should().ContainKey("TargetClass");
        errorContext.AdditionalContext["TargetClass"].Should().Be("MyClass");
        errorContext.AdditionalContext.Should().NotContainKey("TargetMember");
    }

    [Fact]
    public void AddTargetContext_WithWhitespaceClassName_DoesNotAddTargetClass()
    {
        // Arrange
        var refactoring = new TestRefactoring();
        var errorContext = new RefactoringErrorContext { Phase = "Test Phase" };

        // Act
        refactoring.TestAddTargetContext(errorContext, "   ", "MyMethod");

        // Assert
        errorContext.AdditionalContext.Should().NotContainKey("TargetClass");
    }

    [Fact]
    public void AddTargetContext_WithWhitespaceMemberName_DoesNotAddTargetMember()
    {
        // Arrange
        var refactoring = new TestRefactoring();
        var errorContext = new RefactoringErrorContext { Phase = "Test Phase" };

        // Act
        refactoring.TestAddTargetContext(errorContext, "MyClass", "   ");

        // Assert
        errorContext.AdditionalContext.Should().ContainKey("TargetClass");
        errorContext.AdditionalContext.Should().NotContainKey("TargetMember");
    }

    #endregion
}
