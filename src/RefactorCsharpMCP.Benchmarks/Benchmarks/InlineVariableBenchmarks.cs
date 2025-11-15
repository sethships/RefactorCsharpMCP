using BenchmarkDotNet.Attributes;
using RefactorCsharpMCP.Benchmarks.Config;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for Inline Variable refactoring performance.
/// Tests performance across different code sizes and variable usage patterns.
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class InlineVariableBenchmarks
{
    private string _smallCode = string.Empty;
    private string _mediumCode = string.Empty;
    private InlineVariable _refactoring = null!;

    [GlobalSetup]
    public void Setup()
    {
        _refactoring = new InlineVariable();

        // Small code sample - simple variable inlining
        _smallCode = @"
using System;

namespace TestNamespace
{
    public class Calculator
    {
        public int Add(int a, int b)
        {
            int result = a + b;
            return result;
        }

        public int Multiply(int x, int y)
        {
            int product = x * y;
            return product;
        }
    }
}";

        // Medium code sample - variable with multiple usages
        _mediumCode = @"
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestNamespace
{
    public class DataProcessor
    {
        public List<int> ProcessData(List<int> input)
        {
            var filtered = input.Where(x => x > 0).ToList();
            var sorted = filtered.OrderBy(x => x).ToList();
            var doubled = sorted.Select(x => x * 2).ToList();
            var result = doubled.Take(10).ToList();

            Console.WriteLine($""Processed {result.Count} items"");
            return result;
        }

        public Dictionary<string, int> Aggregate(List<string> items)
        {
            var grouped = items.GroupBy(x => x).ToList();
            var counts = grouped.ToDictionary(g => g.Key, g => g.Count());
            var filtered = counts.Where(kvp => kvp.Value > 1).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            Console.WriteLine($""Found {filtered.Count} duplicates"");
            return filtered;
        }

        public decimal CalculateAverage(List<decimal> values)
        {
            var nonZero = values.Where(v => v != 0).ToList();
            var sum = nonZero.Sum();
            var count = nonZero.Count;
            var average = sum / count;

            Console.WriteLine($""Average of {count} values: {average}"");
            return average;
        }
    }
}";
    }

    [Benchmark(Description = "Inline variable in small file (~20 lines)")]
    public void InlineVariable_SmallFile()
    {
        var result = _refactoring.Execute(_smallCode, lineNumber: 9, columnNumber: 17, targetFramework: "net8.0");

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Refactoring failed: {result.ErrorMessage}");
        }
    }

    [Benchmark(Description = "Inline variable in medium file (~50 lines)")]
    public void InlineVariable_MediumFile()
    {
        var result = _refactoring.Execute(_mediumCode, lineNumber: 12, columnNumber: 17, targetFramework: "net8.0");

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Refactoring failed: {result.ErrorMessage}");
        }
    }
}
