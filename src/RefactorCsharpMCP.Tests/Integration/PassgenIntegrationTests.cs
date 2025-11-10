using FluentAssertions;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Tests.Integration;

/// <summary>
/// Integration tests using real code from passgen project.
/// </summary>
public class PassgenIntegrationTests
{
    [Fact]
    public void MakeFieldReadonly_OnPasswordGeneratorFields_ShouldMakeReadonly()
    {
        // Arrange - Real code from passgen PasswordGenerator.cs
        var sourceCode = @"namespace passgen
{
    using System;

    public class PasswordGenerator
    {
        private readonly Random _random;
        private readonly int _length;
        private readonly char[] _specials;
        private readonly char[] _uppers;

        public PasswordGenerator(int length, char[] specials, char[] uppers)
        {
            _random = new Random();
            _length = length;
            _specials = specials;
            _uppers = uppers;
        }

        public string Generate()
        {
            // Use fields to generate password
            return string.Empty;
        }
    }
}";
        var refactoring = new MakeFieldReadonly();

        // Act
        var result = refactoring.Execute(sourceCode, "PasswordGenerator", "_random");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("already readonly");
    }

    [Fact]
    public void ExtractClass_OnPasswordGeneratorCharacterSets_ShouldExtractConfiguration()
    {
        // Arrange - Character set configuration from passgen
        var sourceCode = @"namespace passgen
{
    public class PasswordGenerator
    {
        public static readonly char[] DEFAULT_SPECIALS = ""~!@#$%^&*()_-+="".ToCharArray();
        public static readonly char[] DEFAULT_UPPERS = ""ABCDEFGHIJKLMNOPQRSTUVWXYZ"".ToCharArray();
        public static readonly char[] DEFAULT_LOWERS = ""abcdefghijklmnopqrstuvwxyz"".ToCharArray();
        public static readonly char[] DEFAULT_DIGITS = ""0123456789"".ToCharArray();

        private readonly char[] _specials;
        private readonly char[] _uppers;

        public string Generate()
        {
            // Generate logic
            return string.Empty;
        }
    }
}";
        var refactoring = new ExtractClass();

        // Act - Extract default character sets into configuration class
        var result = refactoring.Execute(
            sourceCode,
            "PasswordGenerator",
            "CharacterSets",
            "DEFAULT_SPECIALS,DEFAULT_UPPERS,DEFAULT_LOWERS,DEFAULT_DIGITS"
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("internal class CharacterSets");
        result.RefactoredCode.Should().Contain("DEFAULT_SPECIALS");
        result.RefactoredCode.Should().Contain("DEFAULT_UPPERS");
        result.RefactoredCode.Should().Contain("DEFAULT_LOWERS");
        result.RefactoredCode.Should().Contain("DEFAULT_DIGITS");
    }

    [Fact]
    public void AnalyzeDependencies_OnPasswordGenerator_ShouldDetectFieldUsage()
    {
        // Arrange - Simplified passgen PasswordGenerator
        var sourceCode = @"namespace passgen
{
    using System;

    public class PasswordGenerator
    {
        private readonly Random _random;
        private readonly int _length;
        private readonly char[] _specials;

        public string Generate()
        {
            var password = new char[_length];
            for (int i = 0; i < _length; i++)
            {
                password[i] = _specials[_random.Next(_specials.Length)];
            }
            return new string(password);
        }

        private void Validate()
        {
            // Validation logic
        }
    }
}";
        var analyzer = new RefactorCsharpMCP.Core.Analysis.DependencyAnalyzer();

        // Act
        var result = analyzer.AnalyzeMethodDependencies(sourceCode, "PasswordGenerator");

        // Assert
        result.Should().ContainKey("Generate");
        result["Generate"].FieldsAccessed.Should().Contain("_length");
        result["Generate"].FieldsAccessed.Should().Contain("_specials");
        result["Generate"].FieldsAccessed.Should().Contain("_random");
    }

    [Fact]
    public void SafeDelete_OnPassgenUnusedHelper_ShouldDelete()
    {
        // Arrange - Hypothetical unused validation helper
        var sourceCode = @"namespace passgen
{
    public class PasswordGenerator
    {
        public string Generate()
        {
            return ValidateAndGenerate();
        }

        private string ValidateAndGenerate()
        {
            // Main generation logic
            return string.Empty;
        }

        private bool IsValidLength(int length)
        {
            // This helper is no longer used
            return length > 0;
        }
    }
}";
        var refactoring = new SafeDelete();

        // Act
        var result = refactoring.Execute(sourceCode, "PasswordGenerator", "IsValidLength");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().NotContain("IsValidLength");
        result.RefactoredCode.Should().Contain("ValidateAndGenerate");
    }

    [Fact]
    public void AnalyzeFieldUsage_OnPasswordGeneratorConstants_ShouldDetectReadonlyAndInitializers()
    {
        // Arrange - passgen constants
        var sourceCode = @"namespace passgen
{
    public class PasswordGenerator
    {
        public const int DEFAULT_LENGTH = 12;
        public static readonly char[] DEFAULT_SPECIALS = ""~!@#$%^&*()_-+="".ToCharArray();
        private readonly int _length;
        private int _counter;

        public PasswordGenerator(int length)
        {
            _length = length;
            _counter = 0;
        }
    }
}";
        var analyzer = new RefactorCsharpMCP.Core.Analysis.DependencyAnalyzer();

        // Act
        var result = analyzer.AnalyzeFieldUsage(sourceCode, "PasswordGenerator");

        // Assert
        result.Should().ContainKey("DEFAULT_SPECIALS");
        result["DEFAULT_SPECIALS"].IsReadOnly.Should().BeTrue();
        result["DEFAULT_SPECIALS"].HasInitializer.Should().BeTrue();

        result.Should().ContainKey("_length");
        result["_length"].IsReadOnly.Should().BeTrue();
        result["_length"].HasInitializer.Should().BeFalse();

        result.Should().ContainKey("_counter");
        result["_counter"].IsReadOnly.Should().BeFalse();
        result["_counter"].HasInitializer.Should().BeFalse();
    }

    [Fact]
    public void MakeFieldReadonly_OnPassgenMutableField_ShouldDetectMutation()
    {
        // Arrange - Field that gets modified
        var sourceCode = @"namespace passgen
{
    public class PasswordGenerator
    {
        private int _retryCount;

        public PasswordGenerator()
        {
            _retryCount = 0;
        }

        public void Reset()
        {
            _retryCount = 0;
        }
    }
}";
        var refactoring = new MakeFieldReadonly();

        // Act
        var result = refactoring.Execute(sourceCode, "PasswordGenerator", "_retryCount");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("assigned outside of constructors");
    }
}
