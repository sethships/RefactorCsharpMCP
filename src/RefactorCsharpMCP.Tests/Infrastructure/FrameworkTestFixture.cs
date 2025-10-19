using RefactorCsharpMCP.Core.Infrastructure.FrameworkSupport;

namespace RefactorCsharpMCP.Tests.Infrastructure;

/// <summary>
/// Base class for all framework-aware tests.
/// Provides shared infrastructure for testing across multiple .NET frameworks.
///
/// IMPORTANT: All test classes inheriting from this fixture MUST add the attribute
/// [Collection("CacheTests")] to serialize access to the shared reference assembly cache.
/// Failure to do so will cause intermittent test failures due to cache concurrency issues.
///
/// Inherit from this class to get access to CompilationFactory and ReferenceAssemblyResolver.
/// </summary>
public abstract class FrameworkTestFixture : IDisposable
{
    /// <summary>
    /// Resolver for framework-specific reference assemblies.
    /// </summary>
    protected ReferenceAssemblyResolver Resolver { get; }

    /// <summary>
    /// Factory for creating framework-aware Roslyn compilations.
    /// </summary>
    protected CompilationFactory CompilationFactory { get; }

    /// <summary>
    /// All supported framework monikers (11 total).
    /// </summary>
    protected static IReadOnlySet<string> SupportedFrameworks => FrameworkMoniker.SupportedFrameworks;

    /// <summary>
    /// Modern .NET frameworks (net9.0, net8.0).
    /// </summary>
    protected static readonly string[] ModernFrameworks = { "net9.0", "net8.0" };

    /// <summary>
    /// .NET Framework monikers (net481, net48, net472, net471, net47, net462, net35).
    /// </summary>
    protected static readonly string[] DotNetFrameworks = { "net481", "net48", "net472", "net471", "net47", "net462", "net35" };

    /// <summary>
    /// .NET Standard frameworks (netstandard2.1, netstandard2.0).
    /// </summary>
    protected static readonly string[] NetStandardFrameworks = { "netstandard2.1", "netstandard2.0" };

    protected FrameworkTestFixture()
    {
        Resolver = new ReferenceAssemblyResolver();
        CompilationFactory = new CompilationFactory(Resolver);
    }

    /// <summary>
    /// Creates a compilation for testing purposes.
    /// </summary>
    protected async Task<Microsoft.CodeAnalysis.CSharp.CSharpCompilation> CreateTestCompilationAsync(
        string targetFramework,
        string sourceCode,
        string? assemblyName = null)
    {
        return await CompilationFactory.CreateCompilationAsync(targetFramework, sourceCode, assemblyName);
    }

    /// <summary>
    /// Validates that source code compiles without errors for a given framework.
    /// </summary>
    protected async Task<bool> ValidatesSuccessfullyAsync(string targetFramework, string sourceCode)
    {
        var (success, _) = await CompilationFactory.ValidateCompilationAsync(targetFramework, sourceCode);
        return success;
    }

    /// <summary>
    /// Gets the C# language version for a framework.
    /// </summary>
    protected static Microsoft.CodeAnalysis.CSharp.LanguageVersion GetLanguageVersion(string targetFramework)
    {
        return FrameworkMappings.GetLanguageVersion(targetFramework);
    }

    /// <summary>
    /// Checks if a framework supports a specific C# feature.
    /// </summary>
    protected static bool SupportsFeature(string targetFramework, FrameworkFeature feature)
    {
        return feature switch
        {
            FrameworkFeature.NullableTypes => FrameworkMappings.HasNullableTypes(targetFramework),
            FrameworkFeature.Tuples => FrameworkMappings.HasTuples(targetFramework),
            FrameworkFeature.CollectionExpressions => FrameworkMappings.HasCollectionExpressions(targetFramework),
            FrameworkFeature.PatternMatching => FrameworkMappings.HasPatternMatching(targetFramework),
            FrameworkFeature.AsyncStreams => FrameworkMappings.HasAsyncStreams(targetFramework),
            FrameworkFeature.Records => FrameworkMappings.HasRecords(targetFramework),
            FrameworkFeature.InitOnlySetters => FrameworkMappings.HasInitOnlySetters(targetFramework),
            _ => false
        };
    }

    /// <summary>
    /// Gets a friendly framework name for error messages.
    /// </summary>
    protected static string GetFriendlyFrameworkName(string targetFramework)
    {
        return FrameworkMoniker.GetFriendlyName(targetFramework);
    }

    /// <summary>
    /// Cleanup logic for test fixture.
    /// </summary>
    public virtual void Dispose()
    {
        try
        {
            // Clear caches to avoid test interference
            if (Resolver != null)
            {
                try
                {
                    Resolver.ClearAllCaches();
                }
                finally
                {
                    // Always dispose resolver even if cache clear fails
                    Resolver.Dispose();
                }
            }
        }
        catch
        {
            // Ignore disposal errors in tests
            // Consider adding ITestOutputHelper for diagnostics in derived classes
        }

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Enumeration of C# language features for easy feature detection.
/// </summary>
public enum FrameworkFeature
{
    NullableTypes,
    Tuples,
    CollectionExpressions,
    PatternMatching,
    AsyncStreams,
    Records,
    InitOnlySetters
}
