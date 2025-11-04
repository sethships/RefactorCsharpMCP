using BenchmarkDotNet.Attributes;
using RefactorCsharpMCP.Benchmarks.Config;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for Constructor Injection refactoring performance.
/// Tests performance across different code sizes and parameter counts.
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class ConstructorInjectionBenchmarks
{
    private string _smallCode = string.Empty;
    private string _mediumCode = string.Empty;
    private ConstructorInjection _refactoring = null!;

    [GlobalSetup]
    public void Setup()
    {
        _refactoring = new ConstructorInjection();

        // Small code sample - single parameter conversion
        _smallCode = @"
using System;

namespace TestNamespace
{
    public interface ILogger
    {
        void Log(string message);
    }

    public class UserService
    {
        public void CreateUser(string username, ILogger logger)
        {
            logger.Log($""Creating user: {username}"");
            Console.WriteLine($""User {username} created"");
        }

        public void DeleteUser(string username, ILogger logger)
        {
            logger.Log($""Deleting user: {username}"");
            Console.WriteLine($""User {username} deleted"");
        }
    }
}";

        // Medium code sample - multiple parameters conversion
        _mediumCode = @"
using System;
using System.Collections.Generic;

namespace TestNamespace
{
    public interface ILogger { void Log(string message); }
    public interface ICache { void Set(string key, object value); }
    public interface IDatabase { void Save(object entity); }

    public class OrderService
    {
        public void ProcessOrder(int orderId, ILogger logger, ICache cache, IDatabase database)
        {
            logger.Log($""Processing order {orderId}"");

            var order = new { Id = orderId, Status = ""Processing"" };
            cache.Set($""order_{orderId}"", order);
            database.Save(order);

            logger.Log($""Order {orderId} processed"");
        }

        public void CancelOrder(int orderId, ILogger logger, ICache cache, IDatabase database)
        {
            logger.Log($""Canceling order {orderId}"");

            cache.Set($""order_{orderId}"", null);

            logger.Log($""Order {orderId} canceled"");
        }

        public void UpdateOrder(int orderId, string status, ILogger logger, IDatabase database)
        {
            logger.Log($""Updating order {orderId} to {status}"");

            var order = new { Id = orderId, Status = status };
            database.Save(order);

            logger.Log($""Order {orderId} updated"");
        }
    }
}";
    }

    [Benchmark(Description = "Constructor injection in small file (~30 lines)")]
    public async Task ConstructorInjection_SmallFile()
    {
        var result = await _refactoring.ExecuteAsync(
            _smallCode,
            className: "UserService",
            methodName: "CreateUser",
            parameterNames: new[] { "logger" },
            targetFramework: "net8.0");

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Refactoring failed: {result.ErrorMessage}");
        }
    }

    [Benchmark(Description = "Constructor injection in medium file (~45 lines)")]
    public async Task ConstructorInjection_MediumFile()
    {
        var result = await _refactoring.ExecuteAsync(
            _mediumCode,
            className: "OrderService",
            methodName: "ProcessOrder",
            parameterNames: new[] { "logger", "cache", "database" },
            targetFramework: "net8.0");

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Refactoring failed: {result.ErrorMessage}");
        }
    }
}
