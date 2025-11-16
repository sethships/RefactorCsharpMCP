using Microsoft.CodeAnalysis.Text;
using RefactorCsharpMCP.Core.Refactorings;
using Xunit;

namespace RefactorCsharpMCP.Tests.Refactorings;

/// <summary>
/// Unit tests for RefactoringErrorContext class.
/// Tests error categorization, message generation, and structured logging.
/// </summary>
public class RefactoringErrorContextTests
{
    [Fact]
    public void FromException_ArgumentException_CategorizesAsInvalidInput()
    {
        // Arrange
        var exception = new ArgumentException("Invalid parameter");
        var phase = "Validation";

        // Act
        var context = RefactoringErrorContext.FromException(exception, phase);

        // Assert
        Assert.Equal(ErrorCategory.InvalidInput, context.Category);
        Assert.Equal(phase, context.Phase);
        Assert.Equal("Invalid parameter", context.FullExceptionMessage);
        Assert.Equal("ArgumentException", context.ExceptionType);
    }

    [Fact]
    public void FromException_ArgumentNullException_CategorizesAsInvalidInput()
    {
        // Arrange
        var exception = new ArgumentNullException("paramName", "Value cannot be null");
        var phase = "Parsing";

        // Act
        var context = RefactoringErrorContext.FromException(exception, phase);

        // Assert
        Assert.Equal(ErrorCategory.InvalidInput, context.Category);
        Assert.Equal("Parsing", context.Phase);
        Assert.Equal("ArgumentNullException", context.ExceptionType);
    }

    [Fact]
    public void FromException_InvalidOperationException_CategorizesAsInvalidState()
    {
        // Arrange
        var exception = new InvalidOperationException("Operation not valid in current state");
        var phase = "Refactoring";

        // Act
        var context = RefactoringErrorContext.FromException(exception, phase);

        // Assert
        Assert.Equal(ErrorCategory.InvalidState, context.Category);
        Assert.Equal("Refactoring", context.Phase);
    }

    [Fact]
    public void FromException_FormatException_CategorizesAsParseError()
    {
        // Arrange
        var exception = new FormatException("Invalid format");
        var phase = "Code Generation";

        // Act
        var context = RefactoringErrorContext.FromException(exception, phase);

        // Assert
        Assert.Equal(ErrorCategory.ParseError, context.Category);
        Assert.Equal("Code Generation", context.Phase);
    }

    [Fact]
    public void FromException_UnknownException_CategorizesAsUnexpectedError()
    {
        // Arrange
        var exception = new NotImplementedException("Feature not implemented");
        var phase = "Analysis";

        // Act
        var context = RefactoringErrorContext.FromException(exception, phase);

        // Assert
        Assert.Equal(ErrorCategory.UnexpectedError, context.Category);
        Assert.Equal("Analysis", context.Phase);
    }

    [Fact]
    public void FromException_WithSourceLocation_CapturesLocation()
    {
        // Arrange
        var exception = new Exception("Test error");
        var phase = "Transformation";
        var location = new LinePosition(42, 15);

        // Act
        var context = RefactoringErrorContext.FromException(exception, phase, location);

        // Assert
        Assert.NotNull(context.SourceLocation);
        Assert.Equal(42, context.SourceLocation.Value.Line);
        Assert.Equal(15, context.SourceLocation.Value.Character);
    }

    [Fact]
    public void ToUserMessage_WithoutLocation_ReturnsGenericMessage()
    {
        // Arrange
        var context = new RefactoringErrorContext
        {
            Category = ErrorCategory.InvalidInput,
            Phase = "Validation"
        };

        // Act
        var message = context.ToUserMessage();

        // Assert
        Assert.Contains("Invalid input provided", message);
        Assert.Contains("during Validation", message);
        Assert.Contains("Please check the code and try again", message);
        Assert.DoesNotContain("line", message.ToLower());
    }

    [Fact]
    public void ToUserMessage_WithLocation_IncludesLineAndColumn()
    {
        // Arrange
        var context = new RefactoringErrorContext
        {
            Category = ErrorCategory.ParseError,
            Phase = "Syntax Parsing",
            SourceLocation = new LinePosition(10, 5)
        };

        // Act
        var message = context.ToUserMessage();

        // Assert
        Assert.Contains("Code parsing error", message);
        Assert.Contains("line 11", message); // 0-indexed → 1-indexed
        Assert.Contains("column 6", message);
        Assert.Contains("during Syntax Parsing", message);
    }

    [Theory]
    [InlineData(ErrorCategory.InvalidInput, "Invalid input provided")]
    [InlineData(ErrorCategory.InvalidState, "Invalid operation state")]
    [InlineData(ErrorCategory.ParseError, "Code parsing error")]
    [InlineData(ErrorCategory.SymbolResolution, "Symbol resolution failed")]
    [InlineData(ErrorCategory.ValidationFailure, "Validation failed")]
    [InlineData(ErrorCategory.UnexpectedError, "Unexpected error occurred")]
    public void ToUserMessage_CategoryMapping_ReturnsCorrectMessage(ErrorCategory category, string expectedText)
    {
        // Arrange
        var context = new RefactoringErrorContext
        {
            Category = category,
            Phase = "Test Phase"
        };

        // Act
        var message = context.ToUserMessage();

        // Assert
        Assert.Contains(expectedText, message);
    }

    [Fact]
    public void ToLogMessage_IncludesAllDetails()
    {
        // Arrange
        var context = new RefactoringErrorContext
        {
            Category = ErrorCategory.SymbolResolution,
            Phase = "Semantic Analysis",
            SourceLocation = new LinePosition(20, 10),
            FullExceptionMessage = "Symbol 'Foo' not found",
            ExceptionType = "SymbolNotFoundException",
            Timestamp = new DateTime(2025, 1, 15, 10, 30, 45, DateTimeKind.Utc)
        };
        context.AdditionalContext["Operation"] = "ExtractMethod";
        context.AdditionalContext["TargetFramework"] = "net8.0";

        // Act
        var logMessage = context.ToLogMessage();

        // Assert
        Assert.Contains("2025-01-15T10:30:45", logMessage);
        Assert.Contains("Semantic Analysis", logMessage);
        Assert.Contains("SymbolResolution", logMessage);
        Assert.Contains("SymbolNotFoundException", logMessage);
        Assert.Contains("Line 21", logMessage);
        Assert.Contains("Column 11", logMessage);
        Assert.Contains("Symbol 'Foo' not found", logMessage);
        Assert.Contains("Operation=ExtractMethod", logMessage);
        Assert.Contains("TargetFramework=net8.0", logMessage);
    }

    [Fact]
    public void ToLogMessage_WithoutSourceLocation_ShowsUnknown()
    {
        // Arrange
        var context = new RefactoringErrorContext
        {
            Category = ErrorCategory.UnexpectedError,
            Phase = "Test Phase",
            FullExceptionMessage = "Test exception",
            ExceptionType = "TestException"
        };

        // Act
        var logMessage = context.ToLogMessage();

        // Assert
        Assert.Contains("Unknown location", logMessage);
    }

    [Fact]
    public void ToLogMessage_WithoutAdditionalContext_ShowsNoContext()
    {
        // Arrange
        var context = new RefactoringErrorContext
        {
            Category = ErrorCategory.ValidationFailure,
            Phase = "Input Validation",
            FullExceptionMessage = "Validation error",
            ExceptionType = "ValidationException"
        };

        // Act
        var logMessage = context.ToLogMessage();

        // Assert
        Assert.Contains("No additional context", logMessage);
    }

    [Fact]
    public void AdditionalContext_IsInitializedEmpty()
    {
        // Act
        var context = new RefactoringErrorContext();

        // Assert
        Assert.NotNull(context.AdditionalContext);
        Assert.Empty(context.AdditionalContext);
    }

    [Fact]
    public void AdditionalContext_CanBeModified()
    {
        // Arrange
        var context = new RefactoringErrorContext();

        // Act
        context.AdditionalContext["Key1"] = "Value1";
        context.AdditionalContext["Key2"] = "Value2";

        // Assert
        Assert.Equal(2, context.AdditionalContext.Count);
        Assert.Equal("Value1", context.AdditionalContext["Key1"]);
        Assert.Equal("Value2", context.AdditionalContext["Key2"]);
    }

    [Fact]
    public void Timestamp_IsSetToUtcNow_ByDefault()
    {
        // Arrange
        var beforeCreate = DateTime.UtcNow;

        // Act
        var context = new RefactoringErrorContext();
        var afterCreate = DateTime.UtcNow;

        // Assert
        Assert.InRange(context.Timestamp, beforeCreate, afterCreate);
        Assert.Equal(DateTimeKind.Utc, context.Timestamp.Kind);
    }

    #region Phase 4: Stack Trace and Inner Exception Tests

    [Fact]
    public void FromException_CapturesStackTrace()
    {
        // Arrange
        Exception exception;
        try
        {
            throw new InvalidOperationException("Test exception with stack trace");
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        // Act
        var context = RefactoringErrorContext.FromException(exception, "Test Phase");

        // Assert
        Assert.NotNull(context.StackTrace);
        Assert.Contains("FromException_CapturesStackTrace", context.StackTrace);
        Assert.Contains("RefactoringErrorContextTests", context.StackTrace);
    }

    [Fact]
    public void FromException_WithInnerException_CapturesInnerExceptionChain()
    {
        // Arrange
        var innerException = new ArgumentException("Inner exception message");
        var outerException = new InvalidOperationException("Outer exception message", innerException);

        // Act
        var context = RefactoringErrorContext.FromException(outerException, "Test Phase");

        // Assert
        Assert.Single(context.InnerExceptions);
        Assert.Equal("Inner exception message", context.InnerExceptions[0].Message);
        Assert.Equal("ArgumentException", context.InnerExceptions[0].ExceptionType);
    }

    [Fact]
    public void FromException_WithMultipleInnerExceptions_CapturesFullChain()
    {
        // Arrange
        var innermost = new FormatException("Innermost exception");
        var middle = new ArgumentException("Middle exception", innermost);
        var outer = new InvalidOperationException("Outer exception", middle);

        // Act
        var context = RefactoringErrorContext.FromException(outer, "Test Phase");

        // Assert
        Assert.Equal(2, context.InnerExceptions.Count);

        // First inner exception (immediate inner)
        Assert.Equal("Middle exception", context.InnerExceptions[0].Message);
        Assert.Equal("ArgumentException", context.InnerExceptions[0].ExceptionType);

        // Second inner exception (innermost)
        Assert.Equal("Innermost exception", context.InnerExceptions[1].Message);
        Assert.Equal("FormatException", context.InnerExceptions[1].ExceptionType);
    }

    [Fact]
    public void FromException_WithInnerExceptionWithStackTrace_CapturesInnerStackTrace()
    {
        // Arrange
        Exception innerException;
        try
        {
            throw new ArgumentException("Inner with stack trace");
        }
        catch (Exception ex)
        {
            innerException = ex;
        }

        var outerException = new InvalidOperationException("Outer", innerException);

        // Act
        var context = RefactoringErrorContext.FromException(outerException, "Test Phase");

        // Assert
        Assert.Single(context.InnerExceptions);
        Assert.NotNull(context.InnerExceptions[0].StackTrace);
        Assert.Contains("FromException_WithInnerExceptionWithStackTrace_CapturesInnerStackTrace",
            context.InnerExceptions[0].StackTrace);
    }

    [Fact]
    public void FromException_WithoutInnerException_HasEmptyInnerExceptionsList()
    {
        // Arrange
        var exception = new InvalidOperationException("No inner exception");

        // Act
        var context = RefactoringErrorContext.FromException(exception, "Test Phase");

        // Assert
        Assert.Empty(context.InnerExceptions);
    }

    [Fact]
    public void ToLogMessage_WithStackTrace_IncludesStackTraceInOutput()
    {
        // Arrange
        var context = new RefactoringErrorContext
        {
            Category = ErrorCategory.UnexpectedError,
            Phase = "Test Phase",
            FullExceptionMessage = "Test exception",
            ExceptionType = "TestException",
            StackTrace = "   at TestClass.TestMethod() in TestFile.cs:line 42\n   at Program.Main()"
        };

        // Act
        var logMessage = context.ToLogMessage();

        // Assert
        Assert.Contains("Stack Trace:", logMessage);
        Assert.Contains("at TestClass.TestMethod()", logMessage);
        Assert.Contains("TestFile.cs:line 42", logMessage);
    }

    [Fact]
    public void ToLogMessage_WithInnerExceptions_IncludesInnerExceptionDetails()
    {
        // Arrange
        var context = new RefactoringErrorContext
        {
            Category = ErrorCategory.UnexpectedError,
            Phase = "Test Phase",
            FullExceptionMessage = "Outer exception",
            ExceptionType = "OuterException"
        };
        context.InnerExceptions.Add(new ExceptionDetail
        {
            Message = "First inner exception",
            ExceptionType = "FirstInnerException",
            StackTrace = "   at Inner1.Method()"
        });
        context.InnerExceptions.Add(new ExceptionDetail
        {
            Message = "Second inner exception",
            ExceptionType = "SecondInnerException",
            StackTrace = "   at Inner2.Method()"
        });

        // Act
        var logMessage = context.ToLogMessage();

        // Assert
        Assert.Contains("Inner Exceptions (2):", logMessage);
        Assert.Contains("[1] Type: FirstInnerException, Message: First inner exception", logMessage);
        Assert.Contains("Stack Trace:    at Inner1.Method()", logMessage);
        Assert.Contains("[2] Type: SecondInnerException, Message: Second inner exception", logMessage);
        Assert.Contains("Stack Trace:    at Inner2.Method()", logMessage);
    }

    [Fact]
    public void ToLogMessage_WithoutStackTrace_DoesNotIncludeStackTraceSection()
    {
        // Arrange
        var context = new RefactoringErrorContext
        {
            Category = ErrorCategory.UnexpectedError,
            Phase = "Test Phase",
            FullExceptionMessage = "Test exception",
            ExceptionType = "TestException",
            StackTrace = null
        };

        // Act
        var logMessage = context.ToLogMessage();

        // Assert
        Assert.DoesNotContain("Stack Trace:", logMessage);
    }

    [Fact]
    public void ToLogMessage_WithoutInnerExceptions_DoesNotIncludeInnerExceptionsSection()
    {
        // Arrange
        var context = new RefactoringErrorContext
        {
            Category = ErrorCategory.UnexpectedError,
            Phase = "Test Phase",
            FullExceptionMessage = "Test exception",
            ExceptionType = "TestException"
        };

        // Act
        var logMessage = context.ToLogMessage();

        // Assert
        Assert.DoesNotContain("Inner Exceptions", logMessage);
    }

    [Fact]
    public void ExceptionDetail_CanBeCreatedWithAllProperties()
    {
        // Act
        var detail = new ExceptionDetail
        {
            Message = "Test message",
            ExceptionType = "TestException",
            StackTrace = "Test stack trace"
        };

        // Assert
        Assert.Equal("Test message", detail.Message);
        Assert.Equal("TestException", detail.ExceptionType);
        Assert.Equal("Test stack trace", detail.StackTrace);
    }

    [Fact]
    public void ExceptionDetail_PropertiesDefaultToEmpty()
    {
        // Act
        var detail = new ExceptionDetail();

        // Assert
        Assert.Equal(string.Empty, detail.Message);
        Assert.Equal(string.Empty, detail.ExceptionType);
        Assert.Null(detail.StackTrace);
    }

    #endregion
}
