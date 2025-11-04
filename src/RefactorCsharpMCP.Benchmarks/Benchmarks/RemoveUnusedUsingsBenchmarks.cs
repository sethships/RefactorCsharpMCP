using BenchmarkDotNet.Attributes;
using RefactorCsharpMCP.Benchmarks.Config;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for Remove Unused Usings refactoring performance.
/// Tests performance across different code sizes and using directive counts.
/// NOTE: This refactoring has IDE analyzer limitations (Issue #72).
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class RemoveUnusedUsingsBenchmarks
{
    private string _smallCode = string.Empty;
    private string _mediumCode = string.Empty;
    private RemoveUnusedUsings _refactoring = null!;

    [GlobalSetup]
    public void Setup()
    {
        _refactoring = new RemoveUnusedUsings();

        // Small code sample - few unused usings
        _smallCode = @"using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TestNamespace
{
    public class SimpleClass
    {
        public void Method()
        {
            Console.WriteLine(""Hello World"");
        }

        public List<int> GetNumbers()
        {
            return new List<int> { 1, 2, 3 };
        }
    }
}";

        // Medium code sample - many unused usings
        _mediumCode = @"using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http;
using System.Xml;
using System.Data;
using System.ComponentModel;

namespace TestNamespace
{
    public class DataProcessor
    {
        public List<int> ProcessData(List<int> input)
        {
            return input.Where(x => x > 0).ToList();
        }

        public string FormatText(string text)
        {
            return text.ToUpper();
        }

        public void LogMessage(string message)
        {
            Console.WriteLine($""[{DateTime.Now}] {message}"");
        }

        public Dictionary<string, int> CountOccurrences(string[] items)
        {
            return items.GroupBy(x => x)
                       .ToDictionary(g => g.Key, g => g.Count());
        }
    }
}";
    }

    [Benchmark(Description = "Remove unused usings in small file (~20 lines)")]
    public async Task RemoveUnusedUsings_SmallFile()
    {
        // NOTE: May not detect all unused usings due to IDE analyzer limitations (Issue #72)
        var result = await _refactoring.ExecuteAsync(_smallCode, "net8.0");

        // Allow graceful failure due to known limitations
        if (!result.IsSuccess && result.ErrorMessage?.Contains("IDE analyzer") == true)
        {
            return; // Expected failure
        }

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Refactoring failed: {result.ErrorMessage}");
        }
    }

    [Benchmark(Description = "Remove unused usings in medium file (~40 lines)")]
    public async Task RemoveUnusedUsings_MediumFile()
    {
        // NOTE: May not detect all unused usings due to IDE analyzer limitations (Issue #72)
        var result = await _refactoring.ExecuteAsync(_mediumCode, "net8.0");

        // Allow graceful failure due to known limitations
        if (!result.IsSuccess && result.ErrorMessage?.Contains("IDE analyzer") == true)
        {
            return; // Expected failure
        }

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Refactoring failed: {result.ErrorMessage}");
        }
    }
}
