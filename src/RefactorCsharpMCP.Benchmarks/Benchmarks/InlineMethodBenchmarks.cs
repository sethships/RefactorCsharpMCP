using BenchmarkDotNet.Attributes;
using RefactorCsharpMCP.Benchmarks.Config;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for Inline Method refactoring performance.
/// Tests performance across different code sizes and method complexity.
/// NOTE: InlineMethod Part 1 has limitations (void methods, simple parameters, single caller).
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class InlineMethodBenchmarks
{
    private string _smallCode = string.Empty;
    private string _mediumCode = string.Empty;
    private InlineMethod _refactoring = null!;

    [GlobalSetup]
    public void Setup()
    {
        _refactoring = new InlineMethod();

        // Small code sample - simple void method
        _smallCode = @"
using System;

namespace TestNamespace
{
    public class Logger
    {
        public void LogMessage(string message)
        {
            PrintLog(message);
        }

        private void PrintLog(string text)
        {
            Console.WriteLine($""[LOG] {text}"");
        }

        public void LogError(string error)
        {
            Console.WriteLine($""[ERROR] {error}"");
        }
    }
}";

        // Medium code sample - void method with multiple statements
        _mediumCode = @"
using System;
using System.Collections.Generic;

namespace TestNamespace
{
    public class DataValidator
    {
        private List<string> _errors = new();

        public void ValidateInput(string input)
        {
            CheckNotNull(input);

            if (input.Length > 100)
            {
                _errors.Add(""Input too long"");
            }
        }

        private void CheckNotNull(string value)
        {
            if (value == null)
            {
                _errors.Add(""Value cannot be null"");
            }
            Console.WriteLine(""Null check passed"");
        }

        public void ValidateNumber(int number)
        {
            if (number < 0)
            {
                _errors.Add(""Number must be positive"");
            }
            Console.WriteLine($""Validated: {number}"");
        }

        public List<string> GetErrors() => _errors;
    }
}";
    }

    [Benchmark(Description = "Inline void method in small file (~25 lines)")]
    public async Task InlineMethod_SmallFile()
    {
        // NOTE: Part 1 limitations - void methods, simple parameters, single caller
        var result = await _refactoring.ExecuteAsync(
            _smallCode,
            lineNumber: 13,
            columnNumber: 22,
            targetFramework: "net8.0");

        // Allow graceful failure due to Part 1 limitations
        if (!result.IsSuccess)
        {
            return; // Expected failure for some scenarios
        }
    }

    [Benchmark(Description = "Inline void method in medium file (~45 lines)")]
    public async Task InlineMethod_MediumFile()
    {
        // NOTE: Part 1 limitations - void methods, simple parameters, single caller
        var result = await _refactoring.ExecuteAsync(
            _mediumCode,
            lineNumber: 21,
            columnNumber: 22,
            targetFramework: "net8.0");

        // Allow graceful failure due to Part 1 limitations
        if (!result.IsSuccess)
        {
            return; // Expected failure for some scenarios
        }
    }
}
