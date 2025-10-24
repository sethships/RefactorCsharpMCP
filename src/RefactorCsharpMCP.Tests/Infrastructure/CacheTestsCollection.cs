using Xunit;

namespace RefactorCsharpMCP.Tests.Infrastructure;

/// <summary>
/// Collection definition for cache tests.
/// Tests in this collection run sequentially to avoid file locking conflicts
/// when accessing shared cache files (cache-manifest.json, DLL files).
///
/// Without this definition, xUnit would ignore the [Collection("CacheTests")] attributes
/// and run tests in parallel, causing IOException and UnauthorizedAccessException.
/// </summary>
[CollectionDefinition("CacheTests", DisableParallelization = true)]
public class CacheTestsCollection
{
    // This class is never instantiated - it's just a marker for xUnit.
    // Tests decorated with [Collection("CacheTests")] will run sequentially.
}
