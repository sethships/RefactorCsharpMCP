using BenchmarkDotNet.Attributes;
using RefactorCsharpMCP.Benchmarks.Config;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for Safe Delete refactoring performance.
/// Tests performance across different code sizes with unused methods.
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class SafeDeleteBenchmarks
{
    private string _smallCode = string.Empty;
    private string _mediumCode = string.Empty;
    private SafeDelete _refactoring = null!;

    [GlobalSetup]
    public void Setup()
    {
        _refactoring = new SafeDelete();

        // Small code sample - unused method
        _smallCode = @"
using System;

namespace TestNamespace
{
    public class Calculator
    {
        public int Add(int a, int b)
        {
            return a + b;
        }

        private void UnusedHelper()
        {
            Console.WriteLine(""This method is never called"");
        }

        public int Multiply(int a, int b)
        {
            return a * b;
        }

        private void AnotherUnusedHelper()
        {
            Console.WriteLine(""Also never called"");
        }
    }
}";

        // Medium code sample - multiple unused methods
        _mediumCode = @"
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestNamespace
{
    public class DataService
    {
        private List<int> _data = new();

        public void AddData(int value)
        {
            _data.Add(value);
        }

        private void UnusedValidator()
        {
            Console.WriteLine(""Unused validation logic"");
        }

        public List<int> GetData()
        {
            return _data.ToList();
        }

        private void UnusedFormatter()
        {
            Console.WriteLine(""Unused formatting logic"");
        }

        public int Count()
        {
            return _data.Count;
        }

        private void UnusedHelper1()
        {
            Console.WriteLine(""Unused helper 1"");
        }

        private void UnusedHelper2()
        {
            Console.WriteLine(""Unused helper 2"");
        }

        public void Clear()
        {
            _data.Clear();
        }

        private void UnusedLogger()
        {
            Console.WriteLine(""Unused logging"");
        }
    }
}";
    }

    [Benchmark(Description = "Safe delete method in small file (~30 lines)")]
    public async Task SafeDelete_SmallFile()
    {
        var result = await _refactoring.ExecuteAsync(
            _smallCode,
            className: "Calculator",
            methodName: "UnusedHelper",
            targetFramework: "net8.0");

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Refactoring failed: {result.ErrorMessage}");
        }
    }

    [Benchmark(Description = "Safe delete method in medium file (~60 lines)")]
    public async Task SafeDelete_MediumFile()
    {
        var result = await _refactoring.ExecuteAsync(
            _mediumCode,
            className: "DataService",
            methodName: "UnusedValidator",
            targetFramework: "net8.0");

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Refactoring failed: {result.ErrorMessage}");
        }
    }
}
