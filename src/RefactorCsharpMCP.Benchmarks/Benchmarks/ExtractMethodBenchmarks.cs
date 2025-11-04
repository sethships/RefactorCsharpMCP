using BenchmarkDotNet.Attributes;
using RefactorCsharpMCP.Benchmarks.Config;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for Extract Method refactoring performance.
/// Tests performance across different code sizes and complexity levels.
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class ExtractMethodBenchmarks
{
    private string _smallCode = string.Empty;
    private ExtractMethod _refactoring = null!;

    /// <summary>
    /// Setup test data before benchmarks run.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _refactoring = new ExtractMethod();

        // Small code sample (~50 lines)
        _smallCode = @"
using System;

namespace TestNamespace
{
    public class Calculator
    {
        public int Add(int a, int b)
        {
            int result = a + b;
            Console.WriteLine($""Adding {a} + {b}"");
            Console.WriteLine($""Result: {result}"");
            return result;
        }

        public int Subtract(int a, int b)
        {
            int result = a - b;
            Console.WriteLine($""Subtracting {a} - {b}"");
            Console.WriteLine($""Result: {result}"");
            return result;
        }

        public int Multiply(int a, int b)
        {
            int result = a * b;
            Console.WriteLine($""Multiplying {a} * {b}"");
            Console.WriteLine($""Result: {result}"");
            return result;
        }

        public int Divide(int a, int b)
        {
            if (b == 0)
            {
                throw new DivideByZeroException();
            }
            int result = a / b;
            Console.WriteLine($""Dividing {a} / {b}"");
            Console.WriteLine($""Result: {result}"");
            return result;
        }
    }
}";
    }

    /// <summary>
    /// Benchmark: Extract method from small code file.
    /// Baseline performance test for typical refactoring scenario.
    /// </summary>
    [Benchmark(Description = "Extract method from small file (~50 lines)")]
    public async Task ExtractMethod_SmallFile()
    {
        var result = await _refactoring.ExecuteAsync(
            _smallCode,
            startLine: 10,
            endLine: 11,
            newMethodName: "LogOperation",
            targetFramework: "net8.0");

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Refactoring failed: {result.ErrorMessage}");
        }
    }
}
