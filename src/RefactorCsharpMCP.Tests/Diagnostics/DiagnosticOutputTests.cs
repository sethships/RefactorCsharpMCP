using FluentAssertions;
using RefactorCsharpMCP.Core.Diagnostics;
using Microsoft.CodeAnalysis;
using Xunit;
using Xunit.Abstractions;

namespace RefactorCsharpMCP.Tests.Diagnostics;

public class DiagnosticOutputTests
{
    private readonly ITestOutputHelper _output;

    public DiagnosticOutputTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task AnalyzeCode_WithUnusedUsings_OutputAllDiagnostics()
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
        _output.WriteLine("Analyzing code with pattern-based analyzer...");
        var result = await analyzer.AnalyzeCodeAsync(sourceCode, "net8.0", DiagnosticSeverity.Hidden);

        // Debug output
        _output.WriteLine($"Analysis Success: {result.Success}");
        _output.WriteLine($"Total Diagnostics: {result.Diagnostics.Count}");
        _output.WriteLine($"Summary - Errors: {result.Summary.ErrorCount}, Warnings: {result.Summary.WarningCount}, Info: {result.Summary.InfoCount}");

        if (result.Diagnostics.Any())
        {
            _output.WriteLine("\nAll diagnostics found:");
            foreach (var diagnostic in result.Diagnostics)
            {
                _output.WriteLine($"  [{diagnostic.Id}] {diagnostic.Severity} - {diagnostic.Message}");
                _output.WriteLine($"    Location: Line {diagnostic.Location.Line}, Column {diagnostic.Location.Column}");
                _output.WriteLine($"    Category: {diagnostic.Category}");
                if (diagnostic.ApplicableRefactorings.Any())
                {
                    _output.WriteLine($"    Refactorings: {string.Join(", ", diagnostic.ApplicableRefactorings)}");
                }
            }
        }
        else
        {
            _output.WriteLine("\nNO DIAGNOSTICS FOUND!");
        }

        // Assert
        result.Success.Should().BeTrue();
    }
}
