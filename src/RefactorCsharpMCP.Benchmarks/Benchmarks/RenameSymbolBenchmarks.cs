using BenchmarkDotNet.Attributes;
using RefactorCsharpMCP.Benchmarks.Config;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for Rename Symbol refactoring performance.
/// Tests performance across different code sizes and symbol types.
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class RenameSymbolBenchmarks
{
    private string _smallCode = string.Empty;
    private string _mediumCode = string.Empty;
    private RenameSymbol _refactoring = null!;

    [GlobalSetup]
    public void Setup()
    {
        _refactoring = new RenameSymbol();

        // Small code sample - rename field
        _smallCode = @"
using System;

namespace TestNamespace
{
    public class Person
    {
        private string _name;
        private int _age;

        public Person(string name, int age)
        {
            _name = name;
            _age = age;
        }

        public string GetInfo()
        {
            return $""{_name} is {_age} years old"";
        }
    }
}";

        // Medium code sample - rename method with multiple references
        _mediumCode = @"
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestNamespace
{
    public class OrderProcessor
    {
        private List<Order> _orders = new();

        public void AddOrder(Order order)
        {
            _orders.Add(order);
        }

        public List<Order> GetActiveOrders()
        {
            return _orders.Where(o => o.IsActive).ToList();
        }

        public decimal CalculateTotal()
        {
            var activeOrders = GetActiveOrders();
            return activeOrders.Sum(o => o.Amount);
        }

        public void ProcessOrders()
        {
            var activeOrders = GetActiveOrders();
            foreach (var order in activeOrders)
            {
                Console.WriteLine($""Processing order {order.Id}"");
            }
        }

        public int CountActiveOrders()
        {
            return GetActiveOrders().Count;
        }
    }

    public class Order
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public bool IsActive { get; set; }
    }
}";
    }

    [Benchmark(Description = "Rename field in small file (~25 lines)")]
    public async Task RenameSymbol_Field_SmallFile()
    {
        var result = await _refactoring.ExecuteAsync(
            _smallCode,
            lineNumber: 7,
            columnNumber: 24,
            newName: "_fullName",
            targetFramework: "net8.0");

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Refactoring failed: {result.ErrorMessage}");
        }
    }

    [Benchmark(Description = "Rename method in medium file (~55 lines)")]
    public async Task RenameSymbol_Method_MediumFile()
    {
        var result = await _refactoring.ExecuteAsync(
            _mediumCode,
            lineNumber: 17,
            columnNumber: 28,
            newName: "GetPendingOrders",
            targetFramework: "net8.0");

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Refactoring failed: {result.ErrorMessage}");
        }
    }
}
