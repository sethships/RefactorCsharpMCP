using FluentAssertions;
using RefactorCsharpMCP.Core.Diagnostics;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace RefactorCsharpMCP.Tests.Diagnostics;

public class AnalyzerDiscoveryTests
{
    private readonly ITestOutputHelper _output;

    public AnalyzerDiscoveryTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void GetCodeStyleAnalyzers_ShouldDiscoverAnalyzers()
    {
        // Act
        var analyzers = AnalyzerDiscovery.GetCodeStyleAnalyzers();

        // Debug output
        _output.WriteLine($"Discovered {analyzers.Length} analyzers");

        if (analyzers.Length == 0)
        {
            _output.WriteLine("No analyzers discovered. Checking why...");

            // Check if Features assembly can be loaded directly
            try
            {
                var assembly = Assembly.Load("Microsoft.CodeAnalysis.CSharp.Features");
                _output.WriteLine($"SUCCESS: Loaded Features assembly: {assembly.FullName}");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"FAILED to load Features assembly via Assembly.Load: {ex.Message}");
            }

            // Check if it's in the output directory
            var currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _output.WriteLine($"Current assembly location: {currentPath}");

            var featuresPath = Path.Combine(currentPath!, "Microsoft.CodeAnalysis.CSharp.Features.dll");
            _output.WriteLine($"Features DLL path: {featuresPath}");
            _output.WriteLine($"Features DLL exists: {File.Exists(featuresPath)}");

            if (File.Exists(featuresPath))
            {
                try
                {
                    var assembly = Assembly.LoadFrom(featuresPath);
                    _output.WriteLine($"SUCCESS: Loaded via LoadFrom: {assembly.FullName}");

                    var types = assembly.GetTypes()
                        .Where(t => !t.IsAbstract && t.Name.Contains("Analyzer"))
                        .Take(5)
                        .ToList();

                    _output.WriteLine($"Found {types.Count} analyzer-like types:");
                    foreach (var type in types)
                    {
                        _output.WriteLine($"  - {type.FullName}");
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"FAILED to load Features assembly via LoadFrom: {ex.Message}");
                    _output.WriteLine($"Exception type: {ex.GetType().Name}");
                    if (ex.InnerException != null)
                    {
                        _output.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    }
                }
            }
        }
        else
        {
            _output.WriteLine("Successfully discovered analyzers:");
            foreach (var analyzer in analyzers.Take(5))
            {
                var diagnostics = analyzer.SupportedDiagnostics;
                var ids = string.Join(", ", diagnostics.Select(d => d.Id).Take(3));
                _output.WriteLine($"  - {analyzer.GetType().Name}: {ids}");
            }

            // Check for specific unused using analyzers
            _output.WriteLine("\nSearching for CS8019 and IDE0005 support...");
            var cs8019Analyzer = analyzers.FirstOrDefault(a =>
                a.SupportedDiagnostics.Any(d => d.Id == "CS8019"));
            var ide0005Analyzer = analyzers.FirstOrDefault(a =>
                a.SupportedDiagnostics.Any(d => d.Id == "IDE0005"));

            _output.WriteLine($"CS8019 analyzer found: {cs8019Analyzer != null}");
            if (cs8019Analyzer != null)
            {
                _output.WriteLine($"  Type: {cs8019Analyzer.GetType().FullName}");
            }

            _output.WriteLine($"IDE0005 analyzer found: {ide0005Analyzer != null}");
            if (ide0005Analyzer != null)
            {
                _output.WriteLine($"  Type: {ide0005Analyzer.GetType().FullName}");
            }
        }

        // Assert
        analyzers.Should().NotBeEmpty("IDE analyzers should be discovered from Features assembly");
    }
}
