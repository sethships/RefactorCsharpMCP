namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Configurable options for refactoring operations.
/// Controls formatting, whitespace handling, and other refactoring behaviors.
/// </summary>
public class RefactoringOptions
{
    /// <summary>
    /// Gets the default refactoring options with standard settings.
    /// </summary>
    public static RefactoringOptions Default { get; } = new RefactoringOptions();

    /// <summary>
    /// Gets or sets whether to preserve the original code formatting.
    /// When true, original indentation and whitespace are maintained.
    /// When false (default), whitespace is normalized to standard formatting.
    /// </summary>
    /// <remarks>
    /// Preserving formatting may result in inconsistent style when refactored code
    /// is mixed with manually formatted code. Normalizing whitespace ensures
    /// consistent formatting but may override user preferences.
    /// </remarks>
    public bool PreserveFormatting { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to preserve comments during refactoring.
    /// Default is true to avoid losing important code documentation.
    /// </summary>
    public bool PreserveComments { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to preserve XML documentation comments.
    /// Default is true to maintain API documentation.
    /// </summary>
    public bool PreserveXmlDocComments { get; set; } = true;

    /// <summary>
    /// Creates a new RefactoringOptions instance with default settings.
    /// </summary>
    public RefactoringOptions()
    {
    }

    /// <summary>
    /// Creates a copy of this options instance.
    /// </summary>
    /// <returns>A new RefactoringOptions instance with the same settings.</returns>
    public RefactoringOptions Clone()
    {
        return new RefactoringOptions
        {
            PreserveFormatting = this.PreserveFormatting,
            PreserveComments = this.PreserveComments,
            PreserveXmlDocComments = this.PreserveXmlDocComments
        };
    }

    /// <summary>
    /// Creates options with formatting preservation enabled.
    /// </summary>
    /// <returns>RefactoringOptions with PreserveFormatting = true.</returns>
    public static RefactoringOptions WithFormattingPreserved()
    {
        return new RefactoringOptions
        {
            PreserveFormatting = true
        };
    }

    /// <summary>
    /// Creates options with formatting normalization enabled (default behavior).
    /// </summary>
    /// <returns>RefactoringOptions with PreserveFormatting = false.</returns>
    public static RefactoringOptions WithNormalizedFormatting()
    {
        return new RefactoringOptions
        {
            PreserveFormatting = false
        };
    }
}
