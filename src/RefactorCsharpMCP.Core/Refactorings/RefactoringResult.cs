using RefactorCsharpMCP.Core.Validation;

namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Represents the result of a refactoring operation.
/// </summary>
public class RefactoringResult
{
    /// <summary>
    /// Gets a value indicating whether the refactoring operation was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the refactored code if the operation was successful; otherwise, null.
    /// </summary>
    public string? RefactoredCode { get; init; }

    /// <summary>
    /// Gets a message describing the result of the refactoring operation.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the error message if the operation failed; otherwise, null.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the validation result if the operation failed due to validation; otherwise, null.
    /// </summary>
    public ValidationResult? ValidationResult { get; init; }

    /// <summary>
    /// Gets the error code if the operation failed; otherwise, null.
    /// Provides compile-time safe error categorization for failure scenarios.
    /// </summary>
    public ErrorCode? ErrorCode { get; init; }

    /// <summary>
    /// Creates a successful refactoring result.
    /// </summary>
    /// <param name="refactoredCode">The refactored source code.</param>
    /// <param name="message">A success message describing the refactoring.</param>
    /// <returns>A successful <see cref="RefactoringResult"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when refactoredCode is null or empty.</exception>
    public static RefactoringResult Success(string refactoredCode, string message)
    {
        if (string.IsNullOrWhiteSpace(refactoredCode))
        {
            throw new ArgumentException("Successful refactoring must produce non-empty code.", nameof(refactoredCode));
        }

        return new RefactoringResult
        {
            IsSuccess = true,
            RefactoredCode = refactoredCode,
            Message = message
        };
    }

    /// <summary>
    /// Creates a failed refactoring result with a typed error code.
    /// </summary>
    /// <param name="errorCode">The error code categorizing the failure.</param>
    /// <param name="errorMessage">The error message describing why the refactoring failed.</param>
    /// <returns>A failed <see cref="RefactoringResult"/>.</returns>
    public static RefactoringResult Failure(ErrorCode errorCode, string errorMessage)
    {
        return new RefactoringResult
        {
            IsSuccess = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            Message = $"Refactoring failed: {errorMessage}"
        };
    }

    /// <summary>
    /// Creates a failed refactoring result.
    /// </summary>
    /// <param name="errorMessage">The error message describing why the refactoring failed.</param>
    /// <returns>A failed <see cref="RefactoringResult"/>.</returns>
    [Obsolete("Use Failure(ErrorCode, string) for type-safe error handling. This method defaults to ErrorCode.REFACTORING_FAILED.")]
    public static RefactoringResult Failure(string errorMessage)
    {
        return Failure(Validation.ErrorCode.REFACTORING_FAILED, errorMessage);
    }

    /// <summary>
    /// Creates a failed refactoring result from a validation failure.
    /// </summary>
    /// <param name="validationResult">The validation result that failed.</param>
    /// <returns>A failed <see cref="RefactoringResult"/> with validation details.</returns>
    public static RefactoringResult ValidationFailure(ValidationResult validationResult)
    {
        return new RefactoringResult
        {
            IsSuccess = false,
            ErrorMessage = validationResult.ErrorMessage,
            Message = $"Validation failed: {validationResult.ErrorMessage}",
            ValidationResult = validationResult
        };
    }
}
