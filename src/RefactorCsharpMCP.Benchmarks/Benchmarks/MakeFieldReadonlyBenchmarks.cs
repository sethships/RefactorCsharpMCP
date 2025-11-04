using BenchmarkDotNet.Attributes;
using RefactorCsharpMCP.Benchmarks.Config;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for Make Field Readonly refactoring performance.
/// Tests performance across different code sizes and field usage patterns.
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class MakeFieldReadonlyBenchmarks
{
    private string _smallCode = string.Empty;
    private string _mediumCode = string.Empty;
    private MakeFieldReadonly _refactoring = null!;

    [GlobalSetup]
    public void Setup()
    {
        _refactoring = new MakeFieldReadonly();

        // Small code sample - simple field
        _smallCode = @"
using System;

namespace TestNamespace
{
    public class Configuration
    {
        private string _apiKey;
        private int _timeout;
        private bool _enableLogging;

        public Configuration(string apiKey, int timeout, bool enableLogging)
        {
            _apiKey = apiKey;
            _timeout = timeout;
            _enableLogging = enableLogging;
        }

        public string GetApiKey() => _apiKey;
        public int GetTimeout() => _timeout;
        public bool IsLoggingEnabled() => _enableLogging;
    }
}";

        // Medium code sample - multiple fields
        _mediumCode = @"
using System;
using System.Collections.Generic;

namespace TestNamespace
{
    public class DatabaseConnection
    {
        private string _connectionString;
        private int _maxRetries;
        private TimeSpan _timeout;
        private bool _enablePooling;
        private Dictionary<string, object> _parameters;

        public DatabaseConnection(
            string connectionString,
            int maxRetries,
            TimeSpan timeout,
            bool enablePooling)
        {
            _connectionString = connectionString;
            _maxRetries = maxRetries;
            _timeout = timeout;
            _enablePooling = enablePooling;
            _parameters = new Dictionary<string, object>();
        }

        public void Connect()
        {
            Console.WriteLine($""Connecting with: {_connectionString}"");
            Console.WriteLine($""Max retries: {_maxRetries}"");
            Console.WriteLine($""Timeout: {_timeout}"");
            Console.WriteLine($""Pooling: {_enablePooling}"");
        }

        public void AddParameter(string key, object value)
        {
            _parameters.Add(key, value);
        }

        public Dictionary<string, object> GetParameters() => _parameters;
    }
}";
    }

    [Benchmark(Description = "Make field readonly in small file (~25 lines)")]
    public async Task MakeFieldReadonly_SmallFile()
    {
        var result = await _refactoring.ExecuteAsync(
            _smallCode,
            className: "Configuration",
            fieldName: "_apiKey",
            targetFramework: "net8.0");

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Refactoring failed: {result.ErrorMessage}");
        }
    }

    [Benchmark(Description = "Make field readonly in medium file (~45 lines)")]
    public async Task MakeFieldReadonly_MediumFile()
    {
        var result = await _refactoring.ExecuteAsync(
            _mediumCode,
            className: "DatabaseConnection",
            fieldName: "_connectionString",
            targetFramework: "net8.0");

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Refactoring failed: {result.ErrorMessage}");
        }
    }
}
