using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;

namespace RefactorCsharpMCP.Benchmarks.Config;

/// <summary>
/// Standard BenchmarkDotNet configuration for RefactorCsharpMCP benchmarks.
/// Provides consistent settings across all benchmark runs.
/// </summary>
public class BenchmarkConfig : ManualConfig
{
    public BenchmarkConfig()
    {
        // Use default job with x64 platform
        AddJob(Job.Default.WithPlatform(BenchmarkDotNet.Environments.Platform.X64));

        // Add memory diagnostics
        AddDiagnoser(MemoryDiagnoser.Default);

        // Export results in multiple formats
        AddExporter(HtmlExporter.Default);
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(CsvExporter.Default);

        // Summary style
        WithSummaryStyle(BenchmarkDotNet.Reports.SummaryStyle.Default
            .WithRatioStyle(BenchmarkDotNet.Columns.RatioStyle.Trend));
    }
}
