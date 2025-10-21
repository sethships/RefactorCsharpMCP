namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Configurable options for refactoring operations.
/// Controls formatting, whitespace handling, and other refactoring behaviors.
/// </summary>
public class RefactoringOptions
{
    /// <summary>
    /// Gets the default refactoring options with standard settings.
    /// Returns a new instance each time to prevent shared mutable state.
    /// </summary>
    public static RefactoringOptions Default => new RefactoringOptions();

    /// <summary>
    /// Gets or sets whether to preserve the original code formatting.
    /// When true, original indentation and whitespace are maintained.
    /// When false (default), whitespace is normalized to standard formatting.
    /// </summary>
    /// <remarks>
    /// Preserving formatting may result in inconsistent style when refactored code
    /// is mixed with manually formatted code. Normalizing whitespace ensures
    /// consistent formatting but may override user preferences.
    ///
    /// NOTE: Comments and XML documentation comments are always preserved via Roslyn trivia.
    /// Future versions may add explicit PreserveComments and PreserveXmlDocComments options.
    /// </remarks>
    public bool PreserveFormatting { get; set; } = false;

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
            PreserveFormatting = this.PreserveFormatting
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
