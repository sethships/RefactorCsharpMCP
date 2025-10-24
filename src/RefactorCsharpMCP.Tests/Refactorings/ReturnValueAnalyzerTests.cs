using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Tests.Refactorings;

/// <summary>
/// Unit tests for ReturnValueAnalyzer's GenerateUniqueVariableName method,
/// specifically testing C# keyword collision prevention (Issue #53).
/// </summary>
public class ReturnValueAnalyzerTests
{
    /// <summary>
    /// Creates a simple semantic model for testing variable name generation.
    /// </summary>
    private (SemanticModel model, int position) CreateTestSemanticModel(string code)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("TestCompilation")
            .AddSyntaxTrees(syntaxTree)
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);

        // Position at the end of the method body where all variables are in scope
        // Look for "// Position here" comment or use end of method
        var positionMarker = code.IndexOf("// Position here");
        var position = positionMarker >= 0
            ? positionMarker
            : code.LastIndexOf("}") - 1; // Just before the closing brace

        return (semanticModel, position);
    }

    #region Keyword Collision Tests

    [Theory]
    [InlineData("class", "class1")]
    [InlineData("return", "return1")]
    [InlineData("int", "int1")]
    [InlineData("void", "void1")]
    [InlineData("string", "string1")]
    [InlineData("object", "object1")]
    [InlineData("bool", "bool1")]
    [InlineData("abstract", "abstract1")]
    [InlineData("interface", "interface1")]
    [InlineData("namespace", "namespace1")]
    public void GenerateUniqueVariableName_WithKeywordBaseName_ShouldAddSuffix(
        string keywordBaseName,
        string expected)
    {
        // Arrange
        var code = "class Test { void M() { } }";
        var (model, position) = CreateTestSemanticModel(code);
        var analyzer = new ReturnValueAnalyzer();

        // Act
        var result = analyzer.GenerateUniqueVariableName(keywordBaseName, model, position);

        // Assert
        result.Should().Be(expected,
            $"keyword '{keywordBaseName}' should be avoided by appending '1'");
        result.Should().NotBe(keywordBaseName,
            "the result should not match the keyword itself");
    }

    [Theory]
    [InlineData("__arglist", "__arglist1")]
    [InlineData("__makeref", "__makeref1")]
    [InlineData("__reftype", "__reftype1")]
    [InlineData("__refvalue", "__refvalue1")]
    public void GenerateUniqueVariableName_WithSpecialKeywords_ShouldAddSuffix(
        string specialKeyword,
        string expected)
    {
        // Arrange - Test the special low-level C# keywords added in Issue #53 fix
        var code = "class Test { void M() { } }";
        var (model, position) = CreateTestSemanticModel(code);
        var analyzer = new ReturnValueAnalyzer();

        // Act
        var result = analyzer.GenerateUniqueVariableName(specialKeyword, model, position);

        // Assert
        result.Should().Be(expected,
            $"special keyword '{specialKeyword}' should be avoided by appending '1'");
    }

    [Fact]
    public void GenerateUniqueVariableName_WithNonKeywordBaseName_ShouldReturnBaseName()
    {
        // Arrange
        var code = "class Test { void M() { } }";
        var (model, position) = CreateTestSemanticModel(code);
        var analyzer = new ReturnValueAnalyzer();

        // Act
        var result = analyzer.GenerateUniqueVariableName("result", model, position);

        // Assert
        result.Should().Be("result",
            "'result' is not a keyword and doesn't conflict with existing variables");
    }

    #endregion

    #region Existing Variable Collision Tests

    [Fact]
    public void GenerateUniqueVariableName_WithExistingVariable_ShouldAddSuffix()
    {
        // Arrange
        var code = @"
class Test
{
    void M()
    {
        int result = 10;
        // Position here
    }
}";
        var (model, position) = CreateTestSemanticModel(code);
        var analyzer = new ReturnValueAnalyzer();

        // Act
        var result = analyzer.GenerateUniqueVariableName("result", model, position);

        // Assert
        result.Should().Be("result1",
            "'result' already exists in scope, so should use 'result1'");
    }

    [Fact]
    public void GenerateUniqueVariableName_WithMultipleNumberedVariants_ShouldFindFirstAvailable()
    {
        // Arrange
        var code = @"
class Test
{
    void M()
    {
        int result = 1;
        int result1 = 2;
        int result2 = 3;
        // Position here
    }
}";
        var (model, position) = CreateTestSemanticModel(code);
        var analyzer = new ReturnValueAnalyzer();

        // Act
        var result = analyzer.GenerateUniqueVariableName("result", model, position);

        // Assert
        result.Should().Be("result3",
            "result, result1, and result2 exist, so should use result3");
    }

    #endregion

    #region Combined Keyword and Variable Collision Tests

    [Fact]
    public void GenerateUniqueVariableName_WithKeywordAndNumberedVariantExists_ShouldSkipBoth()
    {
        // Arrange - 'int' is a keyword, and 'int1' exists in scope
        var code = @"
class Test
{
    void M()
    {
        int int1 = 10;  // 'int1' exists
        // Position here
    }
}";
        var (model, position) = CreateTestSemanticModel(code);
        var analyzer = new ReturnValueAnalyzer();

        // Act
        var result = analyzer.GenerateUniqueVariableName("int", model, position);

        // Assert
        result.Should().Be("int2",
            "'int' is a keyword (avoid with int1), but 'int1' already exists, so should use 'int2'");
    }

    [Fact]
    public void GenerateUniqueVariableName_WithKeywordAndMultipleVariantsExist_ShouldFindNextAvailable()
    {
        // Arrange - 'class' is a keyword, 'class1' and 'class2' exist
        var code = @"
class Test
{
    void M()
    {
        int class1 = 1;
        int class2 = 2;
        // Position here
    }
}";
        var (model, position) = CreateTestSemanticModel(code);
        var analyzer = new ReturnValueAnalyzer();

        // Act
        var result = analyzer.GenerateUniqueVariableName("class", model, position);

        // Assert
        result.Should().Be("class3",
            "'class' is a keyword, 'class1' and 'class2' exist, so should use 'class3'");
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void GenerateUniqueVariableName_WithEmptyScope_ShouldReturnBaseNameIfNotKeyword()
    {
        // Arrange - Method with no variables
        var code = "class Test { void M() { } }";
        var (model, position) = CreateTestSemanticModel(code);
        var analyzer = new ReturnValueAnalyzer();

        // Act
        var result = analyzer.GenerateUniqueVariableName("value", model, position);

        // Assert
        result.Should().Be("value",
            "'value' is not a keyword and no variables exist in scope");
    }

    [Fact]
    public void GenerateUniqueVariableName_WithEmptyScope_KeywordBaseName_ShouldAddSuffix()
    {
        // Arrange - Method with no variables, but base name is a keyword
        var code = "class Test { void M() { } }";
        var (model, position) = CreateTestSemanticModel(code);
        var analyzer = new ReturnValueAnalyzer();

        // Act
        var result = analyzer.GenerateUniqueVariableName("for", model, position);

        // Assert
        result.Should().Be("for1",
            "'for' is a keyword, so should append '1' even with empty scope");
    }

    [Fact]
    public void GenerateUniqueVariableName_WithManyConflicts_ShouldIncrementUntilUnique()
    {
        // Arrange - Extreme case with many numbered variants
        var code = @"
class Test
{
    void M()
    {
        int value = 0;
        int value1 = 1;
        int value2 = 2;
        int value3 = 3;
        int value4 = 4;
        int value5 = 5;
        // Position here
    }
}";
        var (model, position) = CreateTestSemanticModel(code);
        var analyzer = new ReturnValueAnalyzer();

        // Act
        var result = analyzer.GenerateUniqueVariableName("value", model, position);

        // Assert
        result.Should().Be("value6",
            "should increment counter until finding first available name");
    }

    #endregion

    #region All 80 Keywords Validation Test

    [Fact]
    public void GenerateUniqueVariableName_WithAll80CSharpKeywords_ShouldAvoidAll()
    {
        // Arrange - Test that all 80 C# keywords are properly avoided
        var keywords = new[]
        {
            // Standard keywords (77)
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char",
            "checked", "class", "const", "continue", "decimal", "default", "delegate",
            "do", "double", "else", "enum", "event", "explicit", "extern", "false",
            "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit",
            "in", "int", "interface", "internal", "is", "lock", "long", "namespace",
            "new", "null", "object", "operator", "out", "override", "params", "private",
            "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
            "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
            "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
            "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
            // Special keywords (4) - Added in Issue #53 fix
            "__arglist", "__makeref", "__reftype", "__refvalue"
        };

        var code = "class Test { void M() { } }";
        var (model, position) = CreateTestSemanticModel(code);
        var analyzer = new ReturnValueAnalyzer();

        // Act & Assert
        foreach (var keyword in keywords)
        {
            var result = analyzer.GenerateUniqueVariableName(keyword, model, position);

            result.Should().NotBe(keyword,
                $"keyword '{keyword}' should be avoided");
            result.Should().Be($"{keyword}1",
                $"keyword '{keyword}' should have '1' appended");
        }

        // Verify count
        keywords.Length.Should().Be(81,
            "should have exactly 81 C# keywords (77 standard + 4 special)");
    }

    #endregion
}
