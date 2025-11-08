using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RefactorCsharpMCP.Core.Validation;
using RefactorCsharpMCP.Core.Validation.Handlers;
using Xunit;

namespace RefactorCsharpMCP.Tests.Validation.Handlers;

/// <summary>
/// Comprehensive tests for ParseDiagnosticHandler covering language version detection,
/// parse error classification, and feature extraction across C# 7-13.
/// </summary>
public class ParseDiagnosticHandlerTests
{
    private readonly ParseDiagnosticHandler _handler;

    public ParseDiagnosticHandlerTests()
    {
        _handler = new ParseDiagnosticHandler();
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
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var diagnostics = syntaxTree.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Warning);

        // Act
        var result = _handler.Handle(diagnostics, "net8.0", syntaxTree);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Genuine Syntax Errors

    [Fact]
    public void Handle_GenuineSyntaxError_ReturnsSyntaxError()
    {
        // Arrange - Missing semicolon
        var code = "class Test { void M() { int x } }";
        var syntaxTree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.CSharp12));
        var diagnostics = syntaxTree.GetDiagnostics();

        // Act
        var result = _handler.Handle(diagnostics, "net8.0", syntaxTree);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.SYNTAX_ERROR);
        result.ErrorMessage.Should().Contain("';'");
    }

    [Fact]
    public void Handle_MultipleSyntaxErrors_FormatsFirst3WithCount()
    {
        // Arrange - Multiple syntax errors
        var code = @"
class Test
{
    void M1() { int x }
    void M2() { int y }
    void M3() { int z }
    void M4() { int w }
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.CSharp12));
        var diagnostics = syntaxTree.GetDiagnostics();

        // Act
        var result = _handler.Handle(diagnostics, "net8.0", syntaxTree);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.SYNTAX_ERROR);
        result.ErrorMessage.Should().MatchRegex(@".*\(and \d+ more\)");
    }

    [Fact]
    public void Handle_Exactly3SyntaxErrors_DoesNotShowMoreCount()
    {
        // Arrange - Exactly 3 syntax errors
        var code = @"
class Test
{
    void M1() { int x }
    void M2() { int y }
    void M3() { int z }
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.CSharp12));
        var diagnostics = syntaxTree.GetDiagnostics();

        // Act
        var result = _handler.Handle(diagnostics, "net8.0", syntaxTree);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotContain("(and");
        result.ErrorMessage.Should().NotContain("more)");
    }

    #endregion

    #region Language Version Mismatches

    [Fact]
    public void Handle_CollectionExpression_Net48_ReturnsInputSyntaxMismatch()
    {
        // Arrange - C# 12 collection expressions not available in net48
        var code = "class Test { int[] x = [1, 2, 3]; }";
        var syntaxTree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.CSharp7_3));
        var diagnostics = syntaxTree.GetDiagnostics();

        // Act
        var result = _handler.Handle(diagnostics, "net48", syntaxTree);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.INPUT_SYNTAX_MISMATCH);
        result.ErrorMessage.Should().Contain("collection expressions");
    }

    [Fact]
    public void Handle_RecordType_Net48_ReturnsInputSyntaxMismatch()
    {
        // Arrange - C# 9 record types not available in net48
        var code = "record Person(string Name);";
        var syntaxTree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.CSharp7_3));
        var diagnostics = syntaxTree.GetDiagnostics();

        // Act
        var result = _handler.Handle(diagnostics, "net48", syntaxTree);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.INPUT_SYNTAX_MISMATCH);
        result.ErrorMessage.Should().Contain("record");
    }

    [Fact]
    public void Handle_InitOnlyProperty_Net48_ReturnsInputSyntaxMismatch()
    {
        // Arrange - C# 9 init-only properties not available in net48
        var code = "class Test { public int X { get; init; } }";
        var syntaxTree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.CSharp7_3));
        var diagnostics = syntaxTree.GetDiagnostics();

        // Act
        var result = _handler.Handle(diagnostics, "net48", syntaxTree);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.INPUT_SYNTAX_MISMATCH);
        result.ErrorMessage.Should().Contain("init");
    }

    [Fact]
    public void Handle_GlobalUsing_Net48_ReturnsInputSyntaxMismatch()
    {
        // Arrange - C# 10 global using not available in net48
        var code = "global using System;";
        var syntaxTree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.CSharp7_3));
        var diagnostics = syntaxTree.GetDiagnostics();

        // Act
        var result = _handler.Handle(diagnostics, "net48", syntaxTree);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.INPUT_SYNTAX_MISMATCH);
        result.ErrorMessage.Should().Contain("global using");
    }

    [Fact]
    public void Handle_FileScopedNamespace_Net48_ReturnsInputSyntaxMismatch()
    {
        // Arrange - C# 10 file-scoped namespace not available in net48
        var code = "namespace Test; class Foo { }";
        var syntaxTree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.CSharp7_3));
        var diagnostics = syntaxTree.GetDiagnostics();

        // Act
        var result = _handler.Handle(diagnostics, "net48", syntaxTree);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.INPUT_SYNTAX_MISMATCH);
        result.ErrorMessage.Should().Contain("file-scoped namespace");
    }

    [Fact]
    public void Handle_RequiredMembers_Net6_ReturnsInputSyntaxMismatch()
    {
        // Arrange - C# 11 required members not available in net6.0
        var code = "class Test { public required int X { get; set; } }";
        var syntaxTree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.CSharp9));
        var diagnostics = syntaxTree.GetDiagnostics();

        // Act
        var result = _handler.Handle(diagnostics, "net6.0", syntaxTree);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.INPUT_SYNTAX_MISMATCH);
        result.ErrorMessage.Should().Contain("required");
    }

    [Fact]
    public void Handle_PrimaryConstructors_Net6_ReturnsInputSyntaxMismatch()
    {
        // Arrange - C# 12 primary constructors not available in net6.0
        var code = "class Person(string name) { }";
        var syntaxTree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.CSharp10));
        var diagnostics = syntaxTree.GetDiagnostics();

        // Act
        var result = _handler.Handle(diagnostics, "net6.0", syntaxTree);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.INPUT_SYNTAX_MISMATCH);
        result.ErrorMessage.Should().Contain("primary constructor");
    }

    #endregion

    #region Framework Version Display

    [Theory]
    [InlineData("net48", "C# 7.3")]
    [InlineData("net6.0", "C# 10")]
    [InlineData("net8.0", "C# 12")]
    [InlineData("net9.0", "C# 13")]
    public void Handle_LanguageVersionMismatch_ShowsCorrectSupportedVersion(string targetFramework, string expectedVersion)
    {
        // Arrange - Use a feature not available in the target framework
        var code = "class Test { int[] x = [1, 2, 3]; }"; // C# 12 collection expression
        var languageVersion = targetFramework switch
        {
            "net48" => LanguageVersion.CSharp7_3,
            "net6.0" => LanguageVersion.CSharp10,
            "net8.0" => LanguageVersion.CSharp12,
            _ => LanguageVersion.CSharp13
        };

        var syntaxTree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(languageVersion));
        var diagnostics = syntaxTree.GetDiagnostics();

        // Act
        var result = _handler.Handle(diagnostics, targetFramework, syntaxTree);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain(expectedVersion);
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
    public void Handle_MixedSeverities_OnlyProcessesErrors()
    {
        // Arrange - Code that generates both warnings and errors
        var code = @"
#pragma warning disable CS0168
class Test
{
    void M()
    {
        int unusedVar;  // Warning (suppressed)
        int x           // Error (missing semicolon)
    }
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.CSharp12));
        var diagnostics = syntaxTree.GetDiagnostics();

        // Act
        var result = _handler.Handle(diagnostics, "net8.0", syntaxTree);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.SYNTAX_ERROR);
    }

    [Fact]
    public void Handle_UnknownDiagnosticId_FallsBackToGenericVersion()
    {
        // Arrange - Use a very new/unknown feature
        var code = "class Test { int[] x = [1, 2, 3]; }";
        var syntaxTree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.CSharp7));
        var diagnostics = syntaxTree.GetDiagnostics();

        // Act
        var result = _handler.Handle(diagnostics, "net48", syntaxTree);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.INPUT_SYNTAX_MISMATCH);
        // Should fall back to generic version message
        result.ErrorMessage.Should().MatchRegex(@"C# \d+|a newer C# version");
    }

    #endregion

    #region Thread Safety

    [Fact]
    public void Handle_ConcurrentCalls_AreThreadSafe()
    {
        // Arrange
        var code = "class Test { void M() { int x = 42; } }";
        var syntaxTree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.CSharp12));
        var diagnostics = syntaxTree.GetDiagnostics();

        // Act - Call handler concurrently from multiple threads
        var tasks = Enumerable.Range(0, 10).Select(_ =>
            Task.Run(() => _handler.Handle(diagnostics, "net8.0", syntaxTree))
        ).ToArray();

        Task.WaitAll(tasks);

        // Assert - All calls should succeed
        tasks.Should().AllSatisfy(t => t.Result.IsValid.Should().BeTrue());
    }

    #endregion
}
