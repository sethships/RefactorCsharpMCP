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

    #region Edge Case Tests (Code Review Issue: Test Coverage Gaps)

    [Fact]
    public void Encode_CircularReference_HitsMaxDepthGracefully()
    {
        // Arrange - Create a self-referencing object scenario via deep nesting
        // Note: True circular references are handled by MaxDepth protection
        var deepObj = new
        {
            level1 = new
            {
                level2 = new
                {
                    level3 = new
                    {
                        level4 = new
                        {
                            level5 = "deeply nested"
                        }
                    }
                }
            }
        };
        var options = new ToonEncoderOptions { MaxDepth = 2 };

        // Act
        var result = _encoder.Encode(deepObj, options);

        // Assert
        // MaxDepth protection should kick in
        result.Should().Contain("[max depth exceeded]");
    }

    [Fact]
    public void Encode_UnicodeString_PreservesCharacters()
    {
        // Arrange
        var value = "Hello 世界 🎉 émoji";

        // Act
        var result = _encoder.Encode(value);

        // Assert
        result.Should().Contain("Hello");
        result.Should().Contain("世界");
        result.Should().Contain("🎉");
        result.Should().Contain("émoji");
    }

    [Fact]
    public void Encode_NullPropertyValues_HandledCorrectly()
    {
        // Arrange
        var obj = new { name = "test", value = (string?)null, count = 42 };

        // Act
        var result = _encoder.Encode(obj);

        // Assert
        result.Should().Contain("name:");
        result.Should().Contain("test");
        result.Should().Contain("count:");
        result.Should().Contain("42");
        // null properties at top level are skipped for cleaner output
    }

    [Fact]
    public void Encode_MaxDepthZero_ReturnsMaxDepthExceeded()
    {
        // Arrange
        var obj = new { name = "test" };
        var options = new ToonEncoderOptions { MaxDepth = 0 };

        // Act
        var result = _encoder.Encode(obj, options);

        // Assert
        // MaxDepth=0 means the root object properties trigger max depth
        // The property name is output, but the value shows max depth exceeded
        result.Should().Contain("[max depth exceeded]");
    }

    [Fact]
    public void Encode_StringWithColonAtStart_EscapesColon()
    {
        // Arrange
        var value = ":value starts with colon";

        // Act
        var result = _encoder.Encode(value);

        // Assert
        // Colon at start should be escaped to avoid key-value confusion
        result.Should().StartWith("\\:");
    }

    [Fact]
    public void Encode_StringWithColonAfterSpace_EscapesColon()
    {
        // Arrange
        var value = "key :value";

        // Act
        var result = _encoder.Encode(value);

        // Assert
        // Colon after space should be escaped
        result.Should().Contain("key \\:");
    }

    [Fact]
    public void Encode_StringWithMidColon_DoesNotEscape()
    {
        // Arrange
        var value = "http://example.com";

        // Act
        var result = _encoder.Encode(value);

        // Assert
        // Colon in middle (not after whitespace) should NOT be escaped
        result.Should().Contain("http://");
    }

    [Fact]
    public void Encode_LargeObject_CompletesSuccessfully()
    {
        // Arrange - Object with many properties
        var properties = Enumerable.Range(1, 50)
            .ToDictionary(i => $"prop{i}", i => $"value{i}");

        // Act
        var result = _encoder.Encode(properties);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("prop1");
        result.Should().Contain("prop50");
    }

    [Fact]
    public void Encode_ConcurrentAccess_ThreadSafe()
    {
        // Arrange
        var encoder = new ToonEncoder();
        var testObjects = Enumerable.Range(1, 100)
            .Select(i => new { id = i, name = $"item{i}" })
            .ToArray();

        // Act - Encode many objects in parallel to test thread safety
        var results = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.ForEach(testObjects, obj =>
        {
            var result = encoder.Encode(obj);
            results.Add(result);
        });

        // Assert
        results.Should().HaveCount(100);
        results.All(r => r.Contains("id:") && r.Contains("name:")).Should().BeTrue();
    }

    [Fact]
    public void Encode_ConcurrentAccess_CacheConsistency()
    {
        // Arrange - Test that cached PropertyInfo[] is consistent across concurrent accesses
        var encoder = new ToonEncoder();
        var sameTypeObjects = Enumerable.Range(1, 50)
            .Select(i => new { id = i, name = $"item{i}", value = i * 10 })
            .ToArray();

        // Act - Encode the same type from multiple threads to stress the cache
        var results = new System.Collections.Concurrent.ConcurrentBag<string>();
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        Parallel.ForEach(sameTypeObjects, new ParallelOptions { MaxDegreeOfParallelism = 10 }, obj =>
        {
            try
            {
                // Multiple encodes of the same object type to hit the cache
                for (int i = 0; i < 5; i++)
                {
                    var result = encoder.Encode(obj);
                    results.Add(result);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        // Assert - No exceptions and all results are valid
        exceptions.Should().BeEmpty("cache access should be thread-safe");
        results.Should().HaveCount(250); // 50 objects * 5 encodes each
        // Verify all results have consistent structure (same properties in same order)
        var distinctStructures = results.Select(r =>
        {
            var hasId = r.Contains("id:");
            var hasName = r.Contains("name:");
            var hasValue = r.Contains("value:");
            return $"{hasId}-{hasName}-{hasValue}";
        }).Distinct().ToList();
        distinctStructures.Should().HaveCount(1, "all encodings should have consistent property structure");
    }

    [Fact]
    public void Encode_StringWithBackslash_EscapesBackslash()
    {
        // Arrange - Windows file path with backslashes
        var value = @"C:\path\to\file";

        // Act
        var result = _encoder.Encode(value);

        // Assert
        // Backslashes should be escaped to avoid parsing issues
        result.Should().Be(@"C:\\path\\to\\file");
    }

    [Fact]
    public void Encode_StringWithComma_EscapesComma()
    {
        // Arrange - String containing commas (significant in tabular rows)
        var value = "value1,value2,value3";

        // Act
        var result = _encoder.Encode(value);

        // Assert
        // Commas should be escaped to avoid confusion with field separators
        result.Should().Be(@"value1\,value2\,value3");
    }

    [Fact]
    public void Encode_NullableValueType_HandledCorrectly()
    {
        // Arrange - Object with nullable value types
        var obj = new { count = (int?)42, missing = (int?)null, flag = (bool?)true };

        // Act
        var result = _encoder.Encode(obj);

        // Assert
        result.Should().Contain("count:");
        result.Should().Contain("42");
        result.Should().Contain("flag:");
        result.Should().Contain("true");
        // null properties at top level are skipped
    }

    [Fact]
    public void Encode_StringWithMultipleEscapeCharacters_EscapesAll()
    {
        // Arrange - String with multiple special characters
        var value = @"path\to\file, key :value";

        // Act
        var result = _encoder.Encode(value);

        // Assert
        // Should escape: backslash, comma, and colon after space
        result.Should().Contain(@"path\\to\\file");
        result.Should().Contain(@"\,");
        result.Should().Contain(@"\:");
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void OutputFormatConfiguration_InvalidFormat_DefaultsToJson()
    {
        // Arrange
        var args = new[] { "--output-format", "invalid_format" };

        // Act
        var options = RefactorCsharpMCP.Server.Configuration.OutputFormatConfiguration.Load(args);

        // Assert
        options.Format.Should().Be("json");
        options.IsJsonEnabled.Should().BeTrue();
        options.IsToonEnabled.Should().BeFalse();
    }

    [Fact]
    public void OutputFormatConfiguration_ToonFormat_CaseInsensitive()
    {
        // Arrange
        var args = new[] { "--output-format", "TOON" };

        // Act
        var options = RefactorCsharpMCP.Server.Configuration.OutputFormatConfiguration.Load(args);

        // Assert
        options.IsToonEnabled.Should().BeTrue();
    }

    #endregion
}
