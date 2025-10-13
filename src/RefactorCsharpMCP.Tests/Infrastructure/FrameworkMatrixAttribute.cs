using System.Reflection;
using Xunit;
using Xunit.Sdk;
using RefactorCsharpMCP.Core.Infrastructure.FrameworkSupport;

namespace RefactorCsharpMCP.Tests.Infrastructure;

/// <summary>
/// xUnit MemberData-style attribute that generates test cases for all 11 supported frameworks.
/// Simplifies writing tests that should run across multiple framework versions.
/// Usage: [FrameworkMatrix] instead of [Theory] + 11 [InlineData] attributes.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class FrameworkMatrixAttribute : DataAttribute
{
    /// <summary>
    /// Optional: Filter to specific framework families.
    /// </summary>
    public FrameworkFamily Filter { get; set; } = FrameworkFamily.All;

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        var frameworks = GetFrameworksForFilter(Filter);

        foreach (var framework in frameworks)
        {
            yield return new object[] { framework };
        }
    }

    private static IEnumerable<string> GetFrameworksForFilter(FrameworkFamily filter)
    {
        var allFrameworks = FrameworkMoniker.SupportedFrameworks.ToList();

        return filter switch
        {
            FrameworkFamily.Modern => allFrameworks.Where(f => f.StartsWith("net") && !f.StartsWith("netstandard") && !f.Contains("4")),
            FrameworkFamily.Framework => allFrameworks.Where(f => f.Contains("4") || f == "net35"),
            FrameworkFamily.Standard => allFrameworks.Where(f => f.StartsWith("netstandard")),
            FrameworkFamily.All => allFrameworks,
            _ => allFrameworks
        };
    }
}

/// <summary>
/// Framework family filter for FrameworkMatrixAttribute.
/// </summary>
public enum FrameworkFamily
{
    All,
    Modern,      // net8.0, net9.0
    Framework,   // net481, net48, net472, net471, net47, net462, net35
    Standard     // netstandard2.1, netstandard2.0
}
