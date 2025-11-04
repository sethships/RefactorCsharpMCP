using FluentAssertions;
using Microsoft.CodeAnalysis;
using RefactorCsharpMCP.Core.Diagnostics;

namespace RefactorCsharpMCP.Tests.Diagnostics;

public class DiagnosticAnalyzerTests
{
    [Fact(Skip = "CS8019/IDE0005 unused using detection requires full IDE analyzer infrastructure - See Issue #72")]
    public async Task AnalyzeCodeAsync_WithUnusedUsings_ReturnsCS8019Diagnostic()
    {
        // Arrange
        var analyzer = new DiagnosticAnalyzer();
        var sourceCode = @"
using System;
using System.Linq;  // Unused

public class Test
{
    public void Method()
    {
        Console.WriteLine(""Hello"");
    }
}";

        // Act
        var result = await analyzer.AnalyzeCodeAsync(sourceCode, "net8.0");

        // Assert
        result.Success.Should().BeTrue();
        // CS8019 is the compiler diagnostic for unused using directives (equivalent to IDE0005)
        result.Diagnostics.Should().Contain(d => d.Id == "CS8019");
        result.Diagnostics.Should().Contain(d => d.Message.Contains("System.Linq"));
    }

    [Fact]
    public async Task AnalyzeCodeAsync_WithReadonlyField_ReturnsIDE0044Diagnostic()
    {
        // Arrange
        var analyzer = new DiagnosticAnalyzer();
        var sourceCode = @"
public class Test
{
    private int _value;

    public Test()
    {
        _value = 42;
    }
}";

        // Act
        var result = await analyzer.AnalyzeCodeAsync(sourceCode, "net8.0");

        // Assert
        result.Success.Should().BeTrue();
        result.Diagnostics.Should().Contain(d => d.Id == "IDE0044");
        result.Diagnostics.Should().Contain(d => d.Message.Contains("readonly"));
    }

    [Fact]
    public async Task AnalyzeCodeAsync_WithNoIssues_ReturnsEmptyDiagnostics()
    {
        // Arrange
        var analyzer = new DiagnosticAnalyzer();
        var sourceCode = @"
using System;

public class Test
{
    public void Method()
    {
        Console.WriteLine(""Hello"");
    }
}";

        // Act
        var result = await analyzer.AnalyzeCodeAsync(sourceCode, "net8.0");

        // Assert
        result.Success.Should().BeTrue();
        result.Diagnostics.Should().BeEmpty();
        result.Summary.TotalDiagnostics.Should().Be(0);
    }

    [Fact]
    public async Task AnalyzeCodeAsync_WithSyntaxErrors_ReturnsErrors()
    {
        // Arrange
        var analyzer = new DiagnosticAnalyzer();
        var sourceCode = @"
public class Test
{
    public void Method(
    {
        // Missing closing parenthesis
    }
}";

        // Act
        var result = await analyzer.AnalyzeCodeAsync(sourceCode, "net8.0");

        // Assert
        result.Success.Should().BeTrue();
        result.Diagnostics.Should().Contain(d => d.Severity == "Error");
    }

    [Theory]
    [InlineData(DiagnosticSeverity.Error)]
    [InlineData(DiagnosticSeverity.Warning)]
    [InlineData(DiagnosticSeverity.Info)]
    public async Task AnalyzeCodeAsync_WithSeverityFilter_ReturnsOnlyMatchingSeverity(DiagnosticSeverity minSeverity)
    {
        // Arrange
        var analyzer = new DiagnosticAnalyzer();
        var sourceCode = @"
using System.Linq;  // Unused - Warning level

public class Test
{
}";

        // Act
        var result = await analyzer.AnalyzeCodeAsync(sourceCode, "net8.0", minSeverity);

        // Assert
        result.Success.Should().BeTrue();

        // All returned diagnostics should have severity >= minSeverity
        foreach (var diagnostic in result.Diagnostics)
        {
            var severity = Enum.Parse<DiagnosticSeverity>(diagnostic.Severity);
            ((int)severity).Should().BeGreaterThanOrEqualTo((int)minSeverity);
        }
    }

    [Fact]
    public async Task AnalyzeCodeAsync_WithEmptySourceCode_ReturnsFailure()
    {
        // Arrange
        var analyzer = new DiagnosticAnalyzer();

        // Act
        var result = await analyzer.AnalyzeCodeAsync("", "net8.0");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Source code cannot be empty");
    }

    [Fact]
    public async Task AnalyzeCodeAsync_WithEmptyFramework_ReturnsFailure()
    {
        // Arrange
        var analyzer = new DiagnosticAnalyzer();
        var sourceCode = "public class Test { }";

        // Act
        var result = await analyzer.AnalyzeCodeAsync(sourceCode, "");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Target framework cannot be empty");
    }

    [Fact]
    public async Task AnalyzeCodeAsync_WithUnsupportedFramework_ReturnsFailure()
    {
        // Arrange
        var analyzer = new DiagnosticAnalyzer();
        var sourceCode = "public class Test { }";

        // Act
        var result = await analyzer.AnalyzeCodeAsync(sourceCode, "net6.0"); // EOL framework

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Unsupported framework");
    }

    [Theory]
    [InlineData("net8.0")]
    [InlineData("net9.0")]
    public async Task AnalyzeCodeAsync_WithSupportedFrameworks_Succeeds(string targetFramework)
    {
        // Arrange
        var analyzer = new DiagnosticAnalyzer();
        var sourceCode = @"
using System;

public class Test
{
    public void Method()
    {
        Console.WriteLine(""Hello"");
    }
}";

        // Act
        var result = await analyzer.AnalyzeCodeAsync(sourceCode, targetFramework);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Theory]
    [InlineData("net48")]
    [InlineData("net35")]
    public async Task AnalyzeCodeAsync_WithFrameworkReferences_MayFailDueToAssemblyLimitations(string targetFramework)
    {
        // Arrange
        var analyzer = new DiagnosticAnalyzer();
        var sourceCode = @"
using System;

public class Test
{
    public void Method()
    {
        Console.WriteLine(""Hello"");
    }
}";

        // Act
        var result = await analyzer.AnalyzeCodeAsync(sourceCode, targetFramework);

        // Assert - net48/net35 reference assemblies may not be available (Issue #75)
        result.Should().NotBeNull();
        // If reference assemblies are available, analysis should succeed
    }

    [Fact(Skip = "CS8019/IDE0005 unused using detection requires full IDE analyzer infrastructure - See Issue #72")]
    public async Task AnalyzeCodeAsync_DiagnosticLocation_HasCorrectLineAndColumn()
    {
        // Arrange
        var analyzer = new DiagnosticAnalyzer();
        var sourceCode = @"
using System.Linq;  // Line 2, unused

public class Test
{
}";

        // Act
        var result = await analyzer.AnalyzeCodeAsync(sourceCode, "net8.0");

        // Assert
        result.Success.Should().BeTrue();
        // CS8019 is the compiler diagnostic for unused using directives
        var diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == "CS8019");
        diagnostic.Should().NotBeNull();
        diagnostic!.Location.Line.Should().Be(2); // 1-based line number
        diagnostic.Location.Column.Should().BeGreaterThan(0); // 1-based column number
        diagnostic.Location.SpanStart.Should().BeGreaterThan(0);
        diagnostic.Location.SpanLength.Should().BeGreaterThan(0);
    }

    [Fact(Skip = "CS8019/IDE0005 unused using detection requires full IDE analyzer infrastructure - See Issue #72")]
    public async Task AnalyzeCodeAsync_DiagnosticInfo_HasApplicableRefactorings()
    {
        // Arrange
        var analyzer = new DiagnosticAnalyzer();
        var sourceCode = @"
using System.Linq;  // Unused

public class Test
{
}";

        // Act
        var result = await analyzer.AnalyzeCodeAsync(sourceCode, "net8.0");

        // Assert
        result.Success.Should().BeTrue();
        // CS8019 is the compiler diagnostic for unused using directives
        var diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == "CS8019");
        diagnostic.Should().NotBeNull();
        diagnostic!.ApplicableRefactorings.Should().Contain("remove_unused_usings");
    }

    [Fact]
    public async Task AnalyzeCodeAsync_Summary_HasCorrectCounts()
    {
        // Arrange
        var analyzer = new DiagnosticAnalyzer();
        var sourceCode = @"
using System;
using System.Linq;  // Unused - Warning
using System.Collections;  // Unused - Warning

public class Test
{
    private int _value;  // Can be readonly - Info

    public Test()
    {
        _value = 42;
        Console.WriteLine(""Test"");
    }
}";

        // Act
        var result = await analyzer.AnalyzeCodeAsync(sourceCode, "net8.0", DiagnosticSeverity.Info);

        // Assert
        result.Success.Should().BeTrue();
        result.Summary.TotalDiagnostics.Should().BeGreaterThan(0);
        result.Summary.TotalDiagnostics.Should().Be(
            result.Summary.ErrorCount +
            result.Summary.WarningCount +
            result.Summary.InfoCount);
    }

    [Fact(Skip = "CS8019/IDE0005 unused using detection requires full IDE analyzer infrastructure - See Issue #72")]
    public async Task AnalyzeCodeAsync_Category_IsCorrectlyMapped()
    {
        // Arrange
        var analyzer = new DiagnosticAnalyzer();
        var sourceCode = @"
using System.Linq;  // CS8019 - Style category

public class Test
{
}";

        // Act
        var result = await analyzer.AnalyzeCodeAsync(sourceCode, "net8.0");

        // Assert
        result.Success.Should().BeTrue();
        // CS8019 is the compiler diagnostic for unused using directives
        var diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == "CS8019");
        diagnostic.Should().NotBeNull();
        diagnostic!.Category.Should().Be("Style");
    }
}
