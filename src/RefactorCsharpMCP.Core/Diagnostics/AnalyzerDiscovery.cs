using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.Logging;

namespace RefactorCsharpMCP.Core.Diagnostics;

/// <summary>
/// Discovers and loads IDE code style analyzers from Microsoft.CodeAnalysis.CSharp.Features assembly.
/// Provides fallback to known analyzers if dynamic discovery fails.
/// </summary>
public static class AnalyzerDiscovery
{
    private static ImmutableArray<Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer>? _cachedAnalyzers;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets IDE code style analyzers (IDE0001-IDE9999) for diagnostic analysis.
    /// Uses caching to avoid repeated discovery overhead.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    /// <returns>Immutable array of IDE diagnostic analyzers.</returns>
    public static ImmutableArray<Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer> GetCodeStyleAnalyzers(ILogger? logger = null)
    {
        lock (_lock)
        {
            if (_cachedAnalyzers.HasValue)
            {
                logger?.LogDebug("Using cached IDE analyzers ({Count} analyzers)", _cachedAnalyzers.Value.Length);
                return _cachedAnalyzers.Value;
            }

            logger?.LogDebug("Discovering IDE analyzers from Features assembly");

            try
            {
                var analyzers = DiscoverAnalyzers(logger);
                _cachedAnalyzers = analyzers;
                logger?.LogInformation("Successfully discovered {Count} IDE analyzers", analyzers.Length);
                return analyzers;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to discover IDE analyzers: {Message}. Using fallback list.", ex.Message);
                var fallbackAnalyzers = GetFallbackAnalyzers(logger);
                _cachedAnalyzers = fallbackAnalyzers;
                return fallbackAnalyzers;
            }
        }
    }

    /// <summary>
    /// Discovers analyzers from Microsoft.CodeAnalysis.CSharp.Features assembly using reflection.
    /// </summary>
    private static ImmutableArray<Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer> DiscoverAnalyzers(ILogger? logger)
    {
        var analyzers = new List<Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer>();

        // Try to load Features assembly
        var featuresAssembly = TryLoadFeaturesAssembly();
        if (featuresAssembly == null)
        {
            logger?.LogWarning("Could not load Microsoft.CodeAnalysis.CSharp.Features assembly");
            return GetFallbackAnalyzers(logger);
        }

        logger?.LogDebug("Loaded Features assembly: {Assembly}", featuresAssembly.FullName);

        // Find all DiagnosticAnalyzer types
        var analyzerTypes = featuresAssembly.GetTypes()
            .Where(t => !t.IsAbstract &&
                       !t.IsInterface &&
                       typeof(Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer).IsAssignableFrom(t) &&
                       t.GetCustomAttribute<DiagnosticAnalyzerAttribute>() != null)
            .ToList();

        logger?.LogDebug("Found {Count} analyzer types in Features assembly", analyzerTypes.Count);

        foreach (var type in analyzerTypes)
        {
            try
            {
                // Instantiate analyzer
                var analyzer = (Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer?)Activator.CreateInstance(type);
                if (analyzer == null)
                {
                    logger?.LogDebug("Skipping analyzer {Type} - instantiation returned null", type.Name);
                    continue;
                }

                // Check if analyzer supports IDE diagnostics
                var supportedDiagnostics = analyzer.SupportedDiagnostics;
                var hasIdeDiagnostics = supportedDiagnostics.Any(d => d.Id.StartsWith("IDE", StringComparison.Ordinal));

                if (hasIdeDiagnostics)
                {
                    analyzers.Add(analyzer);
                    logger?.LogDebug("Added analyzer {Type} supporting diagnostics: {Diagnostics}",
                        type.Name,
                        string.Join(", ", supportedDiagnostics.Select(d => d.Id).Take(5)));
                }
                else
                {
                    logger?.LogDebug("Skipping analyzer {Type} - no IDE diagnostics supported", type.Name);
                }
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "Failed to instantiate analyzer {Type}: {Message}", type.Name, ex.Message);
                // Continue with other analyzers
            }
        }

        if (analyzers.Count == 0)
        {
            logger?.LogWarning("No IDE analyzers discovered via reflection. Using fallback list.");
            return GetFallbackAnalyzers(logger);
        }

        return analyzers.ToImmutableArray();
    }

    /// <summary>
    /// Attempts to load the Microsoft.CodeAnalysis.CSharp.Features assembly.
    /// </summary>
    private static Assembly? TryLoadFeaturesAssembly()
    {
        try
        {
            // Method 1: Try loading via Assembly.Load (works if already loaded)
            try
            {
                return Assembly.Load("Microsoft.CodeAnalysis.CSharp.Features");
            }
            catch
            {
                // Method 2: Try LoadFrom with relative path (works if DLL is in output directory)
                try
                {
                    var currentAssembly = Assembly.GetExecutingAssembly();
                    var currentPath = Path.GetDirectoryName(currentAssembly.Location);
                    if (currentPath != null)
                    {
                        var featuresPath = Path.Combine(currentPath, "Microsoft.CodeAnalysis.CSharp.Features.dll");
                        if (File.Exists(featuresPath))
                        {
                            return Assembly.LoadFrom(featuresPath);
                        }
                    }
                }
                catch
                {
                    // Method 3: Try loading via assembly name pattern
                    try
                    {
                        var workspacesAssembly = Assembly.Load("Microsoft.CodeAnalysis.CSharp.Workspaces");
                        var featuresName = workspacesAssembly.FullName?.Replace("Workspaces", "Features");

                        if (featuresName != null)
                        {
                            return Assembly.Load(featuresName);
                        }
                    }
                    catch
                    {
                        // Ignore - will return null below
                    }
                }
            }
        }
        catch
        {
            // All methods failed
        }

        return null;
    }

    /// <summary>
    /// Provides a fallback list of known IDE analyzers if dynamic discovery fails.
    /// This is a safety mechanism to ensure basic functionality even if reflection fails.
    /// </summary>
    private static ImmutableArray<Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer> GetFallbackAnalyzers(ILogger? logger)
    {
        logger?.LogInformation("Using fallback analyzer list (known limitation - some IDE diagnostics may not be detected)");

        // Note: For fallback, we return an empty array and rely on the legacy DiagnosticAnalyzer
        // which has custom implementations for IDE0044. Full IDE analyzer support requires
        // successful discovery from the Features assembly.
        //
        // Alternative: We could try to instantiate specific known analyzer types by name,
        // but this is fragile and may break with Roslyn version updates.

        return ImmutableArray<Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer>.Empty;
    }

    /// <summary>
    /// Clears the analyzer cache. Useful for testing or if analyzers need to be reloaded.
    /// </summary>
    public static void ClearCache()
    {
        lock (_lock)
        {
            _cachedAnalyzers = null;
        }
    }
}
