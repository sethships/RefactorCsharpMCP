using RefactorCsharpMCP.Core.Refactorings;
using Xunit;

namespace RefactorCsharpMCP.Tests.Refactorings;

/// <summary>
/// Unit tests for RefactoringOptions class.
/// Tests default values, factory methods, cloning, and immutability of Default property.
/// </summary>
public class RefactoringOptionsTests
{
    [Fact]
    public void Constructor_CreatesDefaultValues()
    {
        // Act
        var options = new RefactoringOptions();

        // Assert
        Assert.False(options.PreserveFormatting);
        Assert.True(options.PreserveComments);
        Assert.True(options.PreserveXmlDocComments);
    }

    [Fact]
    public void Default_ReturnsNewInstanceEachTime()
    {
        // Act
        var default1 = RefactoringOptions.Default;
        var default2 = RefactoringOptions.Default;

        // Assert
        Assert.NotSame(default1, default2); // Different instances
        Assert.False(default1.PreserveFormatting);
        Assert.False(default2.PreserveFormatting);
    }

    [Fact]
    public void Default_ModifyingOneInstance_DoesNotAffectOthers()
    {
        // Arrange
        var default1 = RefactoringOptions.Default;
        var default2 = RefactoringOptions.Default;

        // Act
        default1.PreserveFormatting = true;
        default1.PreserveComments = false;

        // Assert
        Assert.True(default1.PreserveFormatting);
        Assert.False(default1.PreserveComments);
        Assert.False(default2.PreserveFormatting); // Unaffected
        Assert.True(default2.PreserveComments); // Unaffected
    }

    [Fact]
    public void WithFormattingPreserved_ReturnsOptionsWithPreserveFormattingTrue()
    {
        // Act
        var options = RefactoringOptions.WithFormattingPreserved();

        // Assert
        Assert.True(options.PreserveFormatting);
        Assert.True(options.PreserveComments); // Other defaults unchanged
        Assert.True(options.PreserveXmlDocComments);
    }

    [Fact]
    public void WithNormalizedFormatting_ReturnsOptionsWithPreserveFormattingFalse()
    {
        // Act
        var options = RefactoringOptions.WithNormalizedFormatting();

        // Assert
        Assert.False(options.PreserveFormatting);
        Assert.True(options.PreserveComments); // Other defaults unchanged
        Assert.True(options.PreserveXmlDocComments);
    }

    [Fact]
    public void Clone_CreatesDeepCopy()
    {
        // Arrange
        var original = new RefactoringOptions
        {
            PreserveFormatting = true,
            PreserveComments = false,
            PreserveXmlDocComments = false
        };

        // Act
        var clone = original.Clone();

        // Assert
        Assert.NotSame(original, clone);
        Assert.Equal(original.PreserveFormatting, clone.PreserveFormatting);
        Assert.Equal(original.PreserveComments, clone.PreserveComments);
        Assert.Equal(original.PreserveXmlDocComments, clone.PreserveXmlDocComments);
    }

    [Fact]
    public void Clone_ModifyingClone_DoesNotAffectOriginal()
    {
        // Arrange
        var original = new RefactoringOptions
        {
            PreserveFormatting = false,
            PreserveComments = true,
            PreserveXmlDocComments = true
        };

        // Act
        var clone = original.Clone();
        clone.PreserveFormatting = true;
        clone.PreserveComments = false;

        // Assert
        Assert.False(original.PreserveFormatting); // Unchanged
        Assert.True(original.PreserveComments); // Unchanged
        Assert.True(clone.PreserveFormatting); // Modified
        Assert.False(clone.PreserveComments); // Modified
    }

    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var options = new RefactoringOptions();

        // Act
        options.PreserveFormatting = true;
        options.PreserveComments = false;
        options.PreserveXmlDocComments = false;

        // Assert
        Assert.True(options.PreserveFormatting);
        Assert.False(options.PreserveComments);
        Assert.False(options.PreserveXmlDocComments);
    }

    [Fact]
    public void WithFormattingPreserved_ReturnsNewInstance()
    {
        // Act
        var options1 = RefactoringOptions.WithFormattingPreserved();
        var options2 = RefactoringOptions.WithFormattingPreserved();

        // Assert
        Assert.NotSame(options1, options2);
    }

    [Fact]
    public void WithNormalizedFormatting_ReturnsNewInstance()
    {
        // Act
        var options1 = RefactoringOptions.WithNormalizedFormatting();
        var options2 = RefactoringOptions.WithNormalizedFormatting();

        // Assert
        Assert.NotSame(options1, options2);
    }
}
