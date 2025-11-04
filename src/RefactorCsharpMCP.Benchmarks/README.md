# RefactorCsharpMCP.Benchmarks

Performance benchmarks for RefactorCsharpMCP refactoring operations using BenchmarkDotNet.

## Running Benchmarks

```bash
# Navigate to the benchmarks directory
cd src/RefactorCsharpMCP.Benchmarks

# Run all benchmarks (IMPORTANT: use Release configuration)
dotnet run -c Release

# Run specific benchmarks
dotnet run -c Release --filter *ExtractMethod*

# List available benchmarks
dotnet run -c Release --list flat
```

## Benchmark Results

Benchmark results are saved to `BenchmarkDotNet.Artifacts/results/` directory with:
- Summary tables (HTML, CSV, Markdown)
- Detailed reports with statistical analysis
- Memory allocation diagrams

## Available Benchmarks

### Refactoring Benchmarks
- **ExtractMethodBenchmarks**: Extract method refactoring performance
- **InlineVariableBenchmarks**: Inline variable refactoring performance
- **RenameSymbolBenchmarks**: Rename symbol refactoring performance
- **ConstructorInjectionBenchmarks**: Constructor injection refactoring performance
- **MakeFieldReadonlyBenchmarks**: Make field readonly refactoring performance
- **SafeDeleteBenchmarks**: Safe delete refactoring performance
- **ExtractClassBenchmarks**: Extract class refactoring performance

### Benchmark Categories
Each benchmark tests:
- **Small code files** (~50 lines): Quick baseline performance
- **Medium code files** (~500 lines): Typical real-world scenario
- **Large code files** (~5000 lines): Stress testing and scalability

## Interpreting Results

BenchmarkDotNet provides several metrics:
- **Mean**: Average execution time
- **Error**: Half of 99.9% confidence interval
- **StdDev**: Standard deviation of all measurements
- **Allocated**: Memory allocated per operation

### Performance Targets (Baseline)
- Small files: < 100ms
- Medium files: < 500ms
- Large files: < 2000ms

## Best Practices

1. **Always run in Release mode**: Debug builds add significant overhead
2. **Close other applications**: Minimize system noise during benchmarking
3. **Run multiple times**: Statistical analysis requires multiple iterations
4. **Compare results**: Use baseline results to track performance changes over time

## Architecture

Benchmarks are organized by refactoring type, with shared test data and infrastructure:
- `Data/`: Sample code files for benchmarking
- `Benchmarks/`: Individual benchmark classes
- `Config/`: BenchmarkDotNet configuration

## Adding New Benchmarks

1. Create a new class in `Benchmarks/`
2. Add `[MemoryDiagnoser]` attribute for memory tracking
3. Use `[Benchmark]` attribute on test methods
4. Use `[GlobalSetup]` for initialization
5. Follow naming convention: `{RefactoringName}Benchmarks`
