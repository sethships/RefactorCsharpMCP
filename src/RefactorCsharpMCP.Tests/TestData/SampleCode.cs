using RefactorCsharpMCP.Tests.Infrastructure;

namespace RefactorCsharpMCP.Tests.TestData;

/// <summary>
/// Library of common sample code patterns for testing refactorings across frameworks.
/// All code samples are designed to be compatible with their respective framework versions.
/// </summary>
public static class SampleCode
{
    /// <summary>
    /// Simple class with basic method - compatible with all frameworks.
    /// </summary>
    public const string SimpleClass = @"using System;

public class SimpleClass
{
    public void DoSomething()
    {
        var x = 1;
        var y = 2;
        var sum = x + y;
        Console.WriteLine(sum);
    }
}";

    /// <summary>
    /// Class with fields and properties - compatible with all frameworks.
    /// </summary>
    public const string ClassWithFields = @"using System;

public class DataClass
{
    private int _count;
    private string _name;

    public int Count { get { return _count; } set { _count = value; } }

    public void Initialize()
    {
        _count = 0;
        _name = ""Test"";
    }
}";

    /// <summary>
    /// Generic class - compatible with .NET Framework 2.0+ (all supported frameworks).
    /// </summary>
    public const string GenericClass = @"using System;
using System.Collections.Generic;

public class GenericContainer<T>
{
    private List<T> _items;

    public GenericContainer()
    {
        _items = new List<T>();
    }

    public void Add(T item)
    {
        _items.Add(item);
    }

    public int Count
    {
        get { return _items.Count; }
    }
}";

    /// <summary>
    /// Method with LINQ - compatible with .NET Framework 3.5+ (all supported frameworks).
    /// </summary>
    public const string MethodWithLinq = @"using System;
using System.Collections.Generic;
using System.Linq;

public class LinqExample
{
    public int GetSum(List<int> numbers)
    {
        return numbers.Where(n => n > 0).Sum();
    }
}";

    /// <summary>
    /// Async method - compatible with C# 5.0+ (all supported frameworks except net35).
    /// </summary>
    public const string AsyncMethod = @"using System;
using System.Threading.Tasks;

public class AsyncExample
{
    public async Task<int> GetValueAsync()
    {
        await Task.Delay(100);
        return 42;
    }

    public async Task ProcessAsync()
    {
        var result = await GetValueAsync();
        Console.WriteLine(result);
    }
}";

    /// <summary>
    /// Nullable reference types - compatible with C# 8.0+ only (net8.0, net9.0, netstandard2.1).
    /// </summary>
    public const string NullableTypes = @"#nullable enable
using System;

public class NullableExample
{
    private string? _nullableField;

    public string? GetValue()
    {
        return _nullableField;
    }

    public void SetValue(string? value)
    {
        _nullableField = value;
    }
}";

    /// <summary>
    /// Tuple return type - compatible with C# 7.0+ (excludes net35).
    /// </summary>
    public const string TupleReturn = @"using System;

public class TupleExample
{
    public (int, string) GetData()
    {
        return (42, ""Test"");
    }

    public void UseData()
    {
        var (id, name) = GetData();
        Console.WriteLine($""{id}: {name}"");
    }
}";

    /// <summary>
    /// Pattern matching - compatible with C# 7.0+ (excludes net35).
    /// </summary>
    public const string PatternMatching = @"using System;

public class PatternExample
{
    public string Describe(object obj)
    {
        return obj switch
        {
            int i => $""Integer: {i}"",
            string s => $""String: {s}"",
            null => ""Null value"",
            _ => ""Unknown type""
        };
    }
}";

    /// <summary>
    /// Collection expressions - compatible with C# 12+ only (net8.0, net9.0).
    /// </summary>
    public const string CollectionExpressions = @"using System;
using System.Collections.Generic;

public class CollectionExample
{
    public List<int> GetNumbers()
    {
        int[] numbers = [1, 2, 3, 4, 5];
        return [.. numbers];
    }
}";

    /// <summary>
    /// Records - compatible with C# 9.0+ (net8.0, net9.0).
    /// </summary>
    public const string RecordType = @"using System;

public record Person(string Name, int Age);

public class RecordExample
{
    public Person CreatePerson()
    {
        return new Person(""John"", 30);
    }
}";

    /// <summary>
    /// Constructor injection pattern - compatible with all frameworks.
    /// </summary>
    public const string ConstructorInjection = @"using System;

public interface ILogger
{
    void Log(string message);
}

public class Service
{
    private readonly ILogger _logger;

    public Service(ILogger logger)
    {
        _logger = logger;
    }

    public void DoWork()
    {
        _logger.Log(""Working..."");
    }
}";

    /// <summary>
    /// Class with multiple methods for refactoring - compatible with all frameworks.
    /// </summary>
    public const string MultipleMethodsClass = @"using System;

public class Calculator
{
    public int Add(int a, int b)
    {
        return a + b;
    }

    public int Subtract(int a, int b)
    {
        return a - b;
    }

    public int Multiply(int a, int b)
    {
        return a * b;
    }

    public double Divide(int a, int b)
    {
        if (b == 0)
        {
            throw new DivideByZeroException();
        }
        return (double)a / b;
    }
}";

    /// <summary>
    /// Gets sample code appropriate for a target framework.
    /// Uses FrameworkMappings to determine feature availability.
    /// </summary>
    public static string GetSampleForFramework(string targetFramework, SampleCodeType type)
    {
        return type switch
        {
            SampleCodeType.Simple => SimpleClass,
            SampleCodeType.WithFields => ClassWithFields,
            SampleCodeType.Generic => GenericClass,
            SampleCodeType.Linq => MethodWithLinq,
            SampleCodeType.Async => FrameworkMappings.HasPatternMatching(targetFramework) ? AsyncMethod : SimpleClass,
            SampleCodeType.Nullable => FrameworkMappings.HasNullableTypes(targetFramework) ? NullableTypes : ClassWithFields,
            SampleCodeType.Tuple => FrameworkMappings.HasTuples(targetFramework) ? TupleReturn : SimpleClass,
            SampleCodeType.PatternMatching => FrameworkMappings.HasPatternMatching(targetFramework) ? PatternMatching : SimpleClass,
            SampleCodeType.CollectionExpressions => FrameworkMappings.HasCollectionExpressions(targetFramework) ? CollectionExpressions : GenericClass,
            SampleCodeType.Record => FrameworkMappings.HasRecords(targetFramework) ? RecordType : SimpleClass,
            SampleCodeType.ConstructorInjection => ConstructorInjection,
            SampleCodeType.MultipleMethods => MultipleMethodsClass,
            _ => SimpleClass
        };
    }
}

/// <summary>
/// Types of sample code available.
/// </summary>
public enum SampleCodeType
{
    Simple,
    WithFields,
    Generic,
    Linq,
    Async,
    Nullable,
    Tuple,
    PatternMatching,
    CollectionExpressions,
    Record,
    ConstructorInjection,
    MultipleMethods
}
