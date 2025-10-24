using System.Text;
using RefactorCsharpMCP.Tests.Infrastructure;

namespace RefactorCsharpMCP.Tests.TestData;

/// <summary>
/// Fluent builder for creating framework-specific test source code.
/// Automatically adjusts syntax based on target framework's C# language version.
/// </summary>
public class FrameworkSourceBuilder
{
    private string? _targetFramework;
    private readonly List<string> _usings = new();
    private readonly List<string> _classes = new();
    private string? _namespace;

    public FrameworkSourceBuilder ForFramework(string targetFramework)
    {
        _targetFramework = targetFramework;
        return this;
    }

    public FrameworkSourceBuilder WithNamespace(string namespaceName)
    {
        _namespace = namespaceName;
        return this;
    }

    public FrameworkSourceBuilder WithUsing(params string[] usingDirectives)
    {
        _usings.AddRange(usingDirectives);
        return this;
    }

    public FrameworkSourceBuilder WithClass(string className, string? baseClass = null, params string[] members)
    {
        var sb = new StringBuilder();

        // Build class declaration
        sb.Append($"public class {className}");
        if (!string.IsNullOrEmpty(baseClass))
        {
            sb.Append($" : {baseClass}");
        }
        sb.AppendLine();
        sb.AppendLine("{");

        // Add members
        foreach (var member in members)
        {
            sb.AppendLine($"    {member}");
        }

        sb.AppendLine("}");

        _classes.Add(sb.ToString());
        return this;
    }

    public FrameworkSourceBuilder WithMethod(
        string className,
        string methodSignature,
        string methodBody,
        bool isStatic = false,
        bool isAsync = false)
    {
        var modifier = isStatic ? "static " : "";
        var asyncKeyword = isAsync ? "async " : "";

        var method = $@"    public {modifier}{asyncKeyword}{methodSignature}
    {{
        {methodBody}
    }}";

        // Find the class and add the method
        // For simplicity, we'll just add to the last class
        if (_classes.Count > 0)
        {
            var lastClass = _classes[_classes.Count - 1];
            _classes[_classes.Count - 1] = lastClass.Replace("}", method + Environment.NewLine + "}");
        }

        return this;
    }

    public string Build()
    {
        if (string.IsNullOrEmpty(_targetFramework))
        {
            throw new InvalidOperationException("Target framework must be specified with ForFramework()");
        }

        var sb = new StringBuilder();

        // Add nullable directive for C# 8.0+ if supported
        if (FrameworkMappings.HasNullableTypes(_targetFramework))
        {
            sb.AppendLine("#nullable enable");
        }

        // Add using directives
        foreach (var usingDirective in _usings)
        {
            sb.AppendLine($"using {usingDirective};");
        }

        if (_usings.Count > 0)
        {
            sb.AppendLine();
        }

        // Add namespace if specified
        if (!string.IsNullOrEmpty(_namespace))
        {
            sb.AppendLine($"namespace {_namespace};");
            sb.AppendLine();
        }

        // Add classes
        foreach (var classDeclaration in _classes)
        {
            sb.AppendLine(classDeclaration);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Creates a simple class with a single method for testing.
    /// For netstandard targets, avoids Console which requires separate package.
    /// </summary>
    public static string CreateSimpleClass(string targetFramework, string className = "TestClass", string methodName = "TestMethod")
    {
        // For netstandard targets, avoid Console which requires separate package
        var normalized = targetFramework.ToLowerInvariant();
        var isNetStandard = normalized.StartsWith("netstandard");

        var methodBody = isNetStandard
            ? @"var x = 1;
        var y = 2;
        var sum = x + y;"  // No Console for netstandard
            : @"var x = 1;
        var y = 2;
        Console.WriteLine(x + y);";

        return new FrameworkSourceBuilder()
            .ForFramework(targetFramework)
            .WithUsing("System")
            .WithClass(className,
                members: new[]
                {
                    $@"public void {methodName}()
    {{
        {methodBody}
    }}"
                })
            .Build();
    }

    /// <summary>
    /// Creates a class with fields for testing refactorings.
    /// </summary>
    public static string CreateClassWithFields(string targetFramework, string className = "DataClass")
    {
        return new FrameworkSourceBuilder()
            .ForFramework(targetFramework)
            .WithUsing("System")
            .WithClass(className,
                members: new[]
                {
                    "private int _count;",
                    "private string _name;",
                    "",
                    @"public void Initialize()
    {
        _count = 0;
        _name = ""Test"";
    }"
                })
            .Build();
    }

    /// <summary>
    /// Creates a class with a method that has multiple parameters.
    /// </summary>
    public static string CreateClassWithParameters(string targetFramework)
    {
        return new FrameworkSourceBuilder()
            .ForFramework(targetFramework)
            .WithUsing("System")
            .WithClass("Calculator",
                members: new[]
                {
                    @"public int Add(int a, int b)
    {
        return a + b;
    }",
                    @"public int Multiply(int x, int y, int z)
    {
        return x * y * z;
    }"
                })
            .Build();
    }

    /// <summary>
    /// Creates an async method (C# 5.0+).
    /// </summary>
    public static string CreateAsyncMethod(string targetFramework)
    {
        if (!FrameworkMappings.HasPatternMatching(targetFramework))
        {
            // Fallback for very old frameworks
            return CreateSimpleClass(targetFramework);
        }

        return new FrameworkSourceBuilder()
            .ForFramework(targetFramework)
            .WithUsing("System", "System.Threading.Tasks")
            .WithClass("AsyncClass",
                members: new[]
                {
                    @"public async Task<int> GetValueAsync()
    {
        await Task.Delay(100);
        return 42;
    }"
                })
            .Build();
    }
}
