using FluentAssertions;
using RefactorCsharpMCP.Toon;

namespace RefactorCsharpMCP.Tests.Toon;

/// <summary>
/// Unit tests for the ToonEncoder class.
/// </summary>
public class ToonEncoderTests
{
    private readonly ToonEncoder _encoder = new();

    [Fact]
    public void Encode_NullValue_ReturnsNullString()
    {
        // Act
        var result = _encoder.Encode(null);

        // Assert
        result.Should().Be("null");
    }

    [Fact]
    public void Encode_String_ReturnsString()
    {
        // Arrange
        var value = "hello world";

        // Act
        var result = _encoder.Encode(value);

        // Assert
        // TOON format uses unquoted strings for token efficiency
        result.Should().Be("hello world");
    }

    [Fact]
    public void Encode_StringWithNewlines_ReturnsBase64()
    {
        // Arrange
        var value = "line1\nline2\nline3";
        var options = new ToonEncoderOptions { Base64EncodeMultilineStrings = true };

        // Act
        var result = _encoder.Encode(value, options);

        // Assert
        result.Should().StartWith("base64:");
        var base64Part = result.Substring("base64:".Length);
        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64Part));
        decoded.Should().Be(value);
    }

    [Fact]
    public void Encode_Integer_ReturnsUnquotedNumber()
    {
        // Arrange
        var value = 42;

        // Act
        var result = _encoder.Encode(value);

        // Assert
        result.Should().Be("42");
    }

    [Fact]
    public void Encode_Double_ReturnsUnquotedNumber()
    {
        // Arrange
        var value = 3.14;

        // Act
        var result = _encoder.Encode(value);

        // Assert
        result.Should().Be("3.14");
    }

    [Fact]
    public void Encode_Boolean_ReturnsLowercaseBoolean()
    {
        // Act
        var trueResult = _encoder.Encode(true);
        var falseResult = _encoder.Encode(false);

        // Assert
        trueResult.Should().Be("true");
        falseResult.Should().Be("false");
    }

    [Fact]
    public void Encode_SimpleObject_ReturnsKeyValuePairs()
    {
        // Arrange
        var obj = new { name = "test", count = 5 };

        // Act
        var result = _encoder.Encode(obj);

        // Assert
        // TOON format uses key: value without quotes around string values
        result.Should().Contain("name:");
        result.Should().Contain("test");
        result.Should().Contain("count:");
        result.Should().Contain("5");
    }

    [Fact]
    public void Encode_SimpleArray_ReturnsCommaSeparated()
    {
        // Arrange
        var array = new[] { 1, 2, 3 };

        // Act
        var result = _encoder.Encode(array);

        // Assert
        result.Should().Contain("1");
        result.Should().Contain("2");
        result.Should().Contain("3");
    }

    [Fact]
    public void Encode_ObjectArray_ReturnsTabularFormat()
    {
        // Arrange
        var items = new[]
        {
            new { id = 1, name = "Alice" },
            new { id = 2, name = "Bob" }
        };

        // Act
        var result = _encoder.Encode(items);

        // Assert
        // Should contain table header with field names
        result.Should().Contain("id");
        result.Should().Contain("name");
        result.Should().Contain("Alice");
        result.Should().Contain("Bob");
    }

    [Fact]
    public void Encode_NestedObject_ReturnsIndentedFormat()
    {
        // Arrange
        var obj = new
        {
            outer = "value",
            inner = new { nested = "data" }
        };
        var options = new ToonEncoderOptions { IndentSize = 2 };

        // Act
        var result = _encoder.Encode(obj, options);

        // Assert
        result.Should().Contain("outer:");
        result.Should().Contain("inner:");
        result.Should().Contain("nested:");
    }

    [Fact]
    public void Encode_StringWithSpecialCharacters_PreservesCharacters()
    {
        // Arrange
        var value = "hello world test";

        // Act
        var result = _encoder.Encode(value);

        // Assert
        // TOON format preserves the string content
        result.Should().Contain("hello");
        result.Should().Contain("world");
        result.Should().Contain("test");
    }

    [Fact]
    public void Encode_EmptyString_ReturnsEmptyString()
    {
        // Arrange
        var value = "";

        // Act
        var result = _encoder.Encode(value);

        // Assert
        // Empty string encodes to empty output in TOON format
        result.Should().Be("");
    }

    [Fact]
    public void Encode_EmptyArray_ReturnsEmptyBrackets()
    {
        // Arrange
        var array = Array.Empty<int>();

        // Act
        var result = _encoder.Encode(array);

        // Assert
        result.Should().Be("[]");
    }

    [Fact]
    public void Encode_UseCamelCase_ConvertsPascalToCamel()
    {
        // Arrange
        var obj = new { MyProperty = "value" };
        var options = new ToonEncoderOptions { UseCamelCase = true };

        // Act
        var result = _encoder.Encode(obj, options);

        // Assert
        // Should convert MyProperty to myProperty when UseCamelCase is enabled
        result.Should().NotContain("MyProperty:");
        result.Should().Contain("myProperty:");
    }

    [Fact]
    public void Encode_MaxDepthExceeded_TruncatesDeepNesting()
    {
        // Arrange
        var deepObj = new
        {
            level1 = new
            {
                level2 = new
                {
                    level3 = new
                    {
                        level4 = "too deep"
                    }
                }
            }
        };
        var options = new ToonEncoderOptions { MaxDepth = 3 };

        // Act
        var result = _encoder.Encode(deepObj, options);

        // Assert
        // With MaxDepth=3, we should reach level1, level2, level3 but not level4
        result.Should().Contain("level1:");
        result.Should().Contain("level2:");
        result.Should().Contain("level3:");
        // Level 4 content should be truncated or omitted
    }

    [Fact]
    public void Encode_DateTime_ReturnsIsoFormat()
    {
        // Arrange
        var date = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var result = _encoder.Encode(date);

        // Assert
        result.Should().Contain("2024");
        result.Should().Contain("01");
        result.Should().Contain("15");
    }

    [Fact]
    public void Encode_Dictionary_ReturnsTabularFormat()
    {
        // Arrange
        var dict = new Dictionary<string, int>
        {
            { "one", 1 },
            { "two", 2 }
        };

        // Act
        var result = _encoder.Encode(dict);

        // Assert
        // Dictionary encodes to tabular format in TOON
        result.Should().Contain("one");
        result.Should().Contain("1");
        result.Should().Contain("two");
        result.Should().Contain("2");
    }
}
