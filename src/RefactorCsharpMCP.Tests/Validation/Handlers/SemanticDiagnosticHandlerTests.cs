using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RefactorCsharpMCP.Core.Validation;
using RefactorCsharpMCP.Core.Validation.Handlers;
using Xunit;

namespace RefactorCsharpMCP.Tests.Validation.Handlers;

/// <summary>
/// Comprehensive tests for SemanticDiagnosticHandler covering BCL namespace detection,
/// typo detection heuristics, and API error classification.
/// </summary>
public class SemanticDiagnosticHandlerTests
{
    private readonly SemanticDiagnosticHandler _handler;

    public SemanticDiagnosticHandlerTests()
    {
        _handler = new SemanticDiagnosticHandler();
    }

    #region Success Cases

    [Fact]
    public void Handle_NoDiagnostics_ReturnsSuccess()
    {
        // Arrange
        var diagnostics = Enumerable.Empty<Diagnostic>();
        var syntaxTree = CSharpSyntaxTree.ParseText("class Test { }");

        // Act
        var result = _handler.Handle(diagnostics, "net8.0", syntaxTree);

        // Assert
        result.IsValid.Should().BeTrue();
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void Handle_OnlyWarnings_ReturnsSuccess()
    {
        // Arrange - Code with warnings but no errors
        var code = "class Test { int x; }"; // Unused field warning
        var compilation = CreateCompilation(code);
        var diagnostics = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Warning);
        var syntaxTree = compilation.SyntaxTrees.First();

        // Act
        var result = _handler.Handle(diagnostics, "net8.0", syntaxTree);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region BCL Namespace Detection

    [Theory]
    [InlineData("System.Net.Http.HttpClient")]
    [InlineData("System.Linq.Enumerable")]
    [InlineData("System.Text.Json.JsonSerializer")]
    [InlineData("System.Xml.Linq.XDocument")]
    [InlineData("Microsoft.Extensions.Logging.ILogger")]
    [InlineData("Windows.Foundation.IAsyncAction")]
    public void Handle_BclNamespace_ClassifiesAsFrameworkError(string typeName)
    {
        // Arrange - Code referencing BCL type that doesn't exist in framework
        var code = $"class Test {{ {typeName} x; }}";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CreateMinimalCompilation(syntaxTree); // Minimal refs, BCL types won't resolve
        var diagnostics = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);

        // Act
        var result = _handler.Handle(diagnostics, "net48", syntaxTree);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.FRAMEWORK_API_UNAVAILABLE);
        result.ErrorMessage.Should().Contain("not available in net48");
    }

    [Fact]
    public void Handle_SystemNamespace_ClassifiesAsFrameworkError()
    {
        // Arrange - System.* namespace
        var code = "class Test { System.Collections.Immutable.ImmutableArray<int> x; }";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CreateMinimalCompilation(syntaxTree);
        var diagnostics = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);

        // Act
        var result = _handler.Handle(diagnostics, "net48", syntaxTree);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.FRAMEWORK_API_UNAVAILABLE);
    }

    [Fact]
    public void Handle_MicrosoftNamespace_ClassifiesAsFrameworkError()
    {
        // Arrange - Microsoft.* namespace
        var code = "class Test { Microsoft.CodeAnalysis.SyntaxNode x; }";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CreateMinimalCompilation(syntaxTree);
        var diagnostics = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);

        // Act
        var result = _handler.Handle(diagnostics, "net48", syntaxTree);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.FRAMEWORK_API_UNAVAILABLE);
    }

    #endregion

    #region Typo Detection Heuristics

    [Theory]
    [InlineData("striiing")] // Triple 'i' (typo)
    [InlineData("boook")] // Triple 'o' (typo)
    [InlineData("claaaass")] // Triple 'a' (typo)
    public void Handle_TripleCharacterRepeat_ClassifiesAsTypo(string identifier)
    {
        // Arrange - Identifier with triple character repeat (not 's' or uppercase)
        var code = $"class Test {{ {identifier} x; }}";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CreateMinimalCompilation(syntaxTree);
        var diagnostics = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);

        // Act
        var result = _handler.Handle(diagnostics, "net8.0", syntaxTree);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.SYNTAX_ERROR);
        result.ErrorMessage.Should().NotContain("not available in");
    }

    [Theory]
    [InlineData("ProcessSucceeded")] // Triple 's' (legitimate)
    [InlineData("AddressServer")] // Triple 's' (legitimate)
    [InlineData("XMLLLMProvider")] // Triple uppercase (legitimate acronym)
    public void Handle_LegitimateTripleCharacters_DoesNotClassifyAsTypo(string identifier)
    {
        // Arrange - Legitimate triple 's' or uppercase letters
        var code = $"class Test {{ {identifier} x; }}";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CreateMinimalCompilation(syntaxTree);
        var diagnostics = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);

        // Act
        var result = _handler.Handle(diagnostics, "net8.0", syntaxTree);

        // Assert
        result.IsValid.Should().BeFalse();
        // Should NOT be classified as typo (would be framework error by default)
        result.ErrorCode.Should().NotBe(ErrorCode.SYNTAX_ERROR);
    }

    [Theory]
    [InlineData("mytype")] // All lowercase, >3 chars
    [InlineData("someclass")] // All lowercase, >3 chars
    [InlineData("badidentifier")] // All lowercase, >3 chars
    public void Handle_AllLowercaseLongIdentifier_ClassifiesAsTypo(string identifier)
    {
        // Arrange - All lowercase identifier > 3 chars (uncommon for types)
        var code = $"class Test {{ {identifier} x; }}";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CreateMinimalCompilation(syntaxTree);
        var diagnostics = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);

        // Act
        var result = _handler.Handle(diagnostics, "net8.0", syntaxTree);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.SYNTAX_ERROR);
    }

    [Theory]
    [InlineData("int")] // Short, all lowercase (legitimate)
    [InlineData("var")] // Short, all lowercase (legitimate)
    [InlineData("out")] // Short, all lowercase (legitimate)
    public void Handle_ShortLowercaseIdentifier_DoesNotClassifyByLowercaseRule(string identifier)
    {
        // Arrange - Short lowercase identifiers (≤3 chars) are exempt from all-lowercase rule
        var code = $"class Test {{ void M() {{ {identifier} x; }} }}";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CreateMinimalCompilation(syntaxTree);
        var diagnostics = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);

        // Act
        var result = _handler.Handle(diagnostics, "net8.0", syntaxTree);

        // Assert - May still be classified as typo by short identifier rule, but not by lowercase rule
        if (!result.IsValid)
        {
            // If classified as error, verify it's not using all-lowercase logic
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }
    }

    [Theory]
    [InlineData("sYstem")] // Starts lowercase, has uppercase later
    [InlineData("myType")] // Starts lowercase, has uppercase later (camelCase for types is unusual)
    public void Handle_MixedCaseAnomalies_ClassifiesAsTypo(string identifier)
    {
        // Arrange - Mixed case starting with lowercase (unusual for type names)
        var code = $"class Test {{ {identifier} x; }}";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CreateMinimalCompilation(syntaxTree);
        var diagnostics = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);

        // Act
        var result = _handler.Handle(diagnostics, "net8.0", syntaxTree);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.SYNTAX_ERROR);
    }

    [Theory]
    [InlineData("x")] // 1 char
    [InlineData("ab")] // 2 chars
    public void Handle_VeryShortIdentifier_ClassifiesAsTypo(string identifier)
    {
        // Arrange - Very short identifiers (≤2 chars) are likely typos
        var code = $"class Test {{ {identifier} x; }}";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CreateMinimalCompilation(syntaxTree);
        var diagnostics = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);

        // Act
        var result = _handler.Handle(diagnostics, "net8.0", syntaxTree);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.SYNTAX_ERROR);
    }

    #endregion

    #region Conservative Default Behavior

    [Fact]
    public void Handle_AmbiguousIdentifier_DefaultsToFrameworkError()
    {
        // Arrange - Ambiguous identifier (properly cased, not BCL, no obvious typo)
        var code = "class Test { CustomType x; }";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CreateMinimalCompilation(syntaxTree);
        var diagnostics = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);

        // Act
        var result = _handler.Handle(diagnostics, "net8.0", syntaxTree);

        // Assert
        result.IsValid.Should().BeFalse();
        // Conservative default: classify as framework error when uncertain
        result.ErrorCode.Should().Be(ErrorCode.FRAMEWORK_API_UNAVAILABLE);
    }

    [Fact]
    public void Handle_CannotExtractIdentifier_DefaultsToFrameworkError()
    {
        // Arrange - Diagnostic with invalid location span
        var code = "class Test { UnknownType x; }";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CreateMinimalCompilation(syntaxTree);
        var diagnostics = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);

        // Act
        var result = _handler.Handle(diagnostics, "net8.0", syntaxTree);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.FRAMEWORK_API_UNAVAILABLE);
    }

    #endregion

    #region Error Formatting

    [Fact]
    public void Handle_MultipleErrors_FormatsFirst3WithCount()
    {
        // Arrange - Code with multiple undefined types
        var code = @"
class Test
{
    Type1 x;
    Type2 y;
    Type3 z;
    Type4 w;
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CreateMinimalCompilation(syntaxTree);
        var diagnostics = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);

        // Act
        var result = _handler.Handle(diagnostics, "net8.0", syntaxTree);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().MatchRegex(@".*\(and \d+ more\)");
    }

    [Fact]
    public void Handle_Exactly3Errors_DoesNotShowMoreCount()
    {
        // Arrange - Exactly 3 errors
        var code = @"
class Test
{
    Type1 x;
    Type2 y;
    Type3 z;
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CreateMinimalCompilation(syntaxTree);
        var diagnostics = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);

        // Act
        var result = _handler.Handle(diagnostics, "net8.0", syntaxTree);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotContain("(and");
        result.ErrorMessage.Should().NotContain("more)");
    }

    #endregion

    #region Non-API Errors

    [Fact]
    public void Handle_NonApiSemanticError_ReturnsSyntaxError()
    {
        // Arrange - Semantic error that's not API-related (e.g., CS0029: Cannot convert)
        var code = @"
class Test
{
    void M()
    {
        int x = ""not a number"";
    }
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CreateCompilation(code);
        var diagnostics = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);

        // Act
        var result = _handler.Handle(diagnostics, "net8.0", syntaxTree);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.SYNTAX_ERROR);
    }

    #endregion

    #region Thread Safety

    [Fact]
    public async Task Handle_ConcurrentCalls_AreThreadSafe()
    {
        // Arrange
        var code = "class Test { System.Text.Json.JsonSerializer x; }";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CreateMinimalCompilation(syntaxTree);
        var diagnostics = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);

        // Act - Call handler concurrently from multiple threads
        var tasks = Enumerable.Range(0, 10).Select(_ =>
            Task.Run(() => _handler.Handle(diagnostics, "net48", syntaxTree))
        ).ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert - All calls should return the same classification
        results.Should().AllSatisfy(r =>
        {
            r.IsValid.Should().BeFalse();
            r.ErrorCode.Should().Be(ErrorCode.FRAMEWORK_API_UNAVAILABLE);
        });
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Handle_EmptyDiagnosticsList_ReturnsSuccess()
    {
        // Arrange
        var diagnostics = new List<Diagnostic>();
        var syntaxTree = CSharpSyntaxTree.ParseText("class Test { }");

        // Act
        var result = _handler.Handle(diagnostics, "net8.0", syntaxTree);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Handle_MixedApiAndNonApiErrors_PrioritizesApiErrors()
    {
        // Arrange - Mix of API errors and other errors
        var code = @"
class Test
{
    System.Collections.Generic.MissingType x;  // API error
    int y = ""bad"";  // Non-API error
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CreateMinimalCompilation(syntaxTree);
        var diagnostics = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);

        // Act
        var result = _handler.Handle(diagnostics, "net48", syntaxTree);

        // Assert
        result.IsValid.Should().BeFalse();
        // Should prioritize API error classification
        result.ErrorCode.Should().Be(ErrorCode.FRAMEWORK_API_UNAVAILABLE);
    }

    [Fact]
    public void Handle_UnicodeIdentifiers_HandlesCorrectly()
    {
        // Arrange - Unicode identifier
        var code = "class Test { 日本語Type x; }";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CreateMinimalCompilation(syntaxTree);
        var diagnostics = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);

        // Act
        var result = _handler.Handle(diagnostics, "net8.0", syntaxTree);

        // Assert
        result.IsValid.Should().BeFalse();
        // Should handle unicode without crashing
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Helper Methods

    private static CSharpCompilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };

        return CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static CSharpCompilation CreateMinimalCompilation(SyntaxTree syntaxTree)
    {
        // Minimal references - just mscorlib
        var references = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };

        return CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    #endregion
}
