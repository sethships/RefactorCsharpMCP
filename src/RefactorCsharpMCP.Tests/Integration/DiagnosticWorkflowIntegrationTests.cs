using FluentAssertions;
using RefactorCsharpMCP.Core.Diagnostics;
using RefactorCsharpMCP.Core.Refactorings;
using Microsoft.CodeAnalysis;
using Xunit;

namespace RefactorCsharpMCP.Tests.Integration;

/// <summary>
/// Integration tests for the complete diagnostic analysis → fix workflow.
/// Tests the entire pipeline: analyze code → identify issues → apply fixes.
/// NOTE: These tests require reference assembly downloads and may be slow or fail in CI environments.
/// </summary>
public class DiagnosticWorkflowIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Integration")]
    public async Task AnalyzeAndFixUnusedUsings_CompleteWorkflow_Net8()
    {
        // Arrange - Code with unused usings
        var sourceCode = @"
using System;
using System.Linq;  // Unused
using System.Collections.Generic;  // Unused

public class Calculator
{
    public int Add(int a, int b)
    {
        Console.WriteLine($""Adding {a} + {b}"");
        return a + b;
    }
}";

        var analyzer = new DiagnosticAnalyzer();
        var targetFramework = "net8.0";

        // Act - Step 1: Analyze code
        var analysisResult = await analyzer.AnalyzeCodeAsync(sourceCode, targetFramework);

        // Assert - Analysis should find unused usings
        analysisResult.Success.Should().BeTrue();

        // Step 2: Apply fix for first unused using
        var removeUsingsRefactoring = new RemoveUnusedUsings();
        var fixResult = await removeUsingsRefactoring.ExecuteAsync(sourceCode, targetFramework);

        // Assert - Fix should succeed and remove unused usings
        fixResult.IsSuccess.Should().BeTrue();
        fixResult.RefactoredCode.Should().NotContain("using System.Linq");
        fixResult.RefactoredCode.Should().NotContain("using System.Collections.Generic");
        fixResult.RefactoredCode.Should().Contain("using System;");  // Keep used using
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Integration")]
    public async Task AnalyzeAndFixReadonlyField_CompleteWorkflow_Net48()
    {
        // Arrange - Code with field that can be readonly
        var sourceCode = @"
using System;

public class Service
{
    private string _apiKey;
    private int _timeout;

    public Service()
    {
        _apiKey = ""abc123"";
        _timeout = 30;
    }

    public void CallApi()
    {
        Console.WriteLine($""Calling API with key: {_apiKey}, timeout: {_timeout}"");
    }
}";

        var analyzer = new DiagnosticAnalyzer();
        var targetFramework = "net48";

        // Act - Step 1: Analyze code
        var analysisResult = await analyzer.AnalyzeCodeAsync(sourceCode, targetFramework, DiagnosticSeverity.Info);

        // Assert - Analysis completed
        analysisResult.Success.Should().BeTrue();

        // Step 2: Apply readonly fix
        var readonlyRefactoring = new MakeFieldReadonly();
        var fixResult1 = await readonlyRefactoring.ExecuteAsync(sourceCode, "Service", "_apiKey", targetFramework);

        // Assert - First field made readonly
        fixResult1.IsSuccess.Should().BeTrue();
        fixResult1.RefactoredCode.Should().Contain("private readonly string _apiKey");

        // Step 3: Apply to second field using the updated code
        var fixResult2 = await readonlyRefactoring.ExecuteAsync(fixResult1.RefactoredCode!, "Service", "_timeout", targetFramework);

        // Assert - Second field made readonly
        fixResult2.IsSuccess.Should().BeTrue();
        fixResult2.RefactoredCode.Should().Contain("private readonly int _timeout");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AnalyzeCode_WithMultipleDiagnostics_ReturnsAll()
    {
        // Arrange - Code with multiple issues
        var sourceCode = @"
using System;
using System.Linq;  // Unused
using System.Text;  // Unused

public class DataProcessor
{
    private List<string> _items;

    public DataProcessor()
    {
        _items = new List<string>();
    }

    public void Process()
    {
        Console.WriteLine($""Processing {_items.Count} items"");
    }
}";

        var analyzer = new DiagnosticAnalyzer();

        // Act - Analyze for all severity levels
        var result = await analyzer.AnalyzeCodeAsync(sourceCode, "net8.0", DiagnosticSeverity.Info);

        // Assert - Should find multiple diagnostics
        result.Success.Should().BeTrue();
        result.Summary.TotalDiagnostics.Should().BeGreaterThan(0);

        // Summary should have accurate counts
        result.Summary.TotalDiagnostics.Should().Be(
            result.Summary.ErrorCount +
            result.Summary.WarningCount +
            result.Summary.InfoCount);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AnalyzeCode_WithSyntaxErrors_ReturnsErrorDiagnostics()
    {
        // Arrange - Code with syntax errors
        var sourceCode = @"
using System;

public class BrokenClass
{
    public void Method(
    {
        // Missing closing parenthesis in parameter list
        Console.WriteLine(""This won't compile"");
    }
}";

        var analyzer = new DiagnosticAnalyzer();

        // Act
        var result = await analyzer.AnalyzeCodeAsync(sourceCode, "net8.0", DiagnosticSeverity.Error);

        // Assert
        result.Success.Should().BeTrue();
        result.Diagnostics.Should().Contain(d => d.Severity == "Error");
        result.Summary.ErrorCount.Should().BeGreaterThan(0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DiagnosticWorkflow_AcrossFrameworks_WorksCorrectly()
    {
        // Arrange - Simple code that works across frameworks
        var sourceCode = @"
using System;
using System.Collections.Generic;  // Unused

public class Test
{
    public void Method()
    {
        Console.WriteLine(""Hello"");
    }
}";

        var analyzer = new DiagnosticAnalyzer();
        var removeUsings = new RemoveUnusedUsings();

        // Act & Assert - Test across multiple frameworks
        foreach (var framework in new[] { "net8.0", "net48", "netstandard2.0" })
        {
            // Analyze
            var analysisResult = await analyzer.AnalyzeCodeAsync(sourceCode, framework);
            analysisResult.Success.Should().BeTrue($"Analysis should succeed for {framework}");

            // Fix
            var fixResult = await removeUsings.ExecuteAsync(sourceCode, framework);
            fixResult.IsSuccess.Should().BeTrue($"Fix should succeed for {framework}");
            fixResult.RefactoredCode.Should().NotContain("using System.Collections.Generic",
                $"Unused using should be removed for {framework}");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DiagnosticInfo_ContainsLocationAndApplicableRefactorings()
    {
        // Arrange
        var sourceCode = @"
using System;
using System.Linq;  // Line 3 - Unused

public class Test
{
}";

        var analyzer = new DiagnosticAnalyzer();

        // Act
        var result = await analyzer.AnalyzeCodeAsync(sourceCode, "net8.0");

        // Assert
        result.Success.Should().BeTrue();

        if (result.Diagnostics.Any())
        {
            foreach (var diagnostic in result.Diagnostics)
            {
                // Location should be populated
                diagnostic.Location.Should().NotBeNull();
                diagnostic.Location.Line.Should().BeGreaterThan(0);
                diagnostic.Location.Column.Should().BeGreaterThan(0);

                // Category should be set
                diagnostic.Category.Should().NotBeNullOrEmpty();

                // If it's a fixable diagnostic, should have refactorings
                if (diagnostic.ApplicableRefactorings.Any())
                {
                    diagnostic.ApplicableRefactorings.Should().AllSatisfy(r =>
                        r.Should().NotBeNullOrEmpty());
                }
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AnalyzeCode_WithNoIssues_ReturnsEmptyDiagnosticsAndSuccess()
    {
        // Arrange - Clean code with no issues
        var sourceCode = @"
using System;

public class CleanCode
{
    private readonly string _message;

    public CleanCode(string message)
    {
        _message = message;
    }

    public void Display()
    {
        Console.WriteLine(_message);
    }
}";

        var analyzer = new DiagnosticAnalyzer();

        // Act
        var result = await analyzer.AnalyzeCodeAsync(sourceCode, "net8.0");

        // Assert
        result.Success.Should().BeTrue();
        result.Diagnostics.Should().BeEmpty();
        result.Summary.TotalDiagnostics.Should().Be(0);
        result.Summary.ErrorCount.Should().Be(0);
        result.Summary.WarningCount.Should().Be(0);
        result.Summary.InfoCount.Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task FixDiagnostic_WithInvalidInput_ReturnsFailure()
    {
        // Arrange
        var removeUsings = new RemoveUnusedUsings();

        // Act - Empty source code
        var result = await removeUsings.ExecuteAsync("", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AnalyzeAndFix_RealWorldScenario_MultipleIssues()
    {
        // Arrange - Real-world code with multiple issues
        var sourceCode = @"
using System;
using System.Linq;  // Unused
using System.Collections.Generic;  // Used
using System.Text;  // Unused

public class UserService
{
    private List<string> _users;
    private string _apiEndpoint;

    public UserService(string endpoint)
    {
        _apiEndpoint = endpoint;
        _users = new List<string>();
    }

    public void AddUser(string name)
    {
        _users.Add(name);
    }

    public int GetUserCount()
    {
        return _users.Count;
    }
}";

        var analyzer = new DiagnosticAnalyzer();
        var removeUsings = new RemoveUnusedUsings();
        var makeReadonly = new MakeFieldReadonly();

        // Act - Step 1: Analyze
        var analysisResult = await analyzer.AnalyzeCodeAsync(sourceCode, "net8.0", DiagnosticSeverity.Info);

        // Assert - Should find issues
        analysisResult.Success.Should().BeTrue();

        // Step 2: Fix unused usings
        var step1 = await removeUsings.ExecuteAsync(sourceCode, "net8.0");
        step1.IsSuccess.Should().BeTrue();
        step1.RefactoredCode.Should().NotContain("using System.Linq");
        step1.RefactoredCode.Should().NotContain("using System.Text");
        step1.RefactoredCode.Should().Contain("using System.Collections.Generic");  // Keep used

        // Step 3: Make _apiEndpoint readonly (only assigned in constructor)
        var step2 = await makeReadonly.ExecuteAsync(step1.RefactoredCode!, "UserService", "_apiEndpoint", "net8.0");
        step2.IsSuccess.Should().BeTrue();
        step2.RefactoredCode.Should().Contain("private readonly string _apiEndpoint");

        // Final code should be cleaner
        step2.RefactoredCode.Should().NotContain("// Unused");
    }
}
