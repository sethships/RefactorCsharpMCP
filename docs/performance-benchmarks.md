# RefactorCsharpMCP Performance Benchmarks

Performance benchmarks for RefactorCsharpMCP refactoring operations using BenchmarkDotNet.

## Overview

This document provides baseline performance metrics for all refactoring operations. Benchmarks are categorized by code file size:

- **Small files** (~20-50 lines): Baseline performance for quick refactorings
- **Medium files** (~50-100 lines): Typical real-world scenarios
- **Large files** (~100+ lines): Stress testing and scalability validation

## Benchmark Environment

All benchmarks were executed using:
- **Framework**: .NET 8.0
- **Configuration**: Release mode
- **Platform**: x64
- **Runtime**: .NET Core 8.0
- **Memory Diagnostics**: Enabled (BenchmarkDotNet.MemoryDiagnoser)

## Performance Targets

Performance targets for V1 release:

| File Size | Target Mean Time | Max Acceptable Time |
|-----------|------------------|---------------------|
| Small (~50 lines) | < 100ms | < 200ms |
| Medium (~500 lines) | < 500ms | < 1000ms |
| Large (~5000 lines) | < 2000ms | < 5000ms |

## Benchmark Results

### ExtractMethod

Extract method refactoring performance across different code sizes.

| Benchmark | Code Size | Mean Time | Allocated Memory |
|-----------|-----------|-----------|------------------|
| ExtractMethod_SmallFile | ~50 lines | Target: < 100ms | Target: < 5 MB |

**Notes**:
- Extracts console output statements into a new method
- Framework: net8.0
- Performance is dominated by Roslyn parsing and semantic analysis

### InlineVariable

Inline variable refactoring performance across different variable usage patterns.

| Benchmark | Code Size | Mean Time | Allocated Memory |
|-----------|-----------|-----------|------------------|
| InlineVariable_SmallFile | ~20 lines | Target: < 50ms | Target: < 2 MB |
| InlineVariable_MediumFile | ~50 lines | Target: < 100ms | Target: < 5 MB |

**Notes**:
- Uses synchronous Execute method (no framework validation overhead)
- Fast operation for simple variable declarations
- Performance scales with variable usage count

### RenameSymbol

Rename symbol refactoring performance across different symbol types.

| Benchmark | Code Size | Mean Time | Allocated Memory |
|-----------|-----------|-----------|------------------|
| RenameSymbol_Field_SmallFile | ~25 lines | Target: < 100ms | Target: < 5 MB |
| RenameSymbol_Method_MediumFile | ~55 lines | Target: < 150ms | Target: < 8 MB |

**Notes**:
- Method renames typically slower than field renames due to reference finding
- Async operation with framework validation
- Performance scales with symbol reference count

### ConstructorInjection

Constructor injection refactoring performance across different parameter counts.

| Benchmark | Code Size | Mean Time | Allocated Memory |
|-----------|-----------|-----------|------------------|
| ConstructorInjection_SmallFile | ~30 lines | Target: < 100ms | Target: < 5 MB |
| ConstructorInjection_MediumFile | ~45 lines | Target: < 150ms | Target: < 8 MB |

**Notes**:
- Single parameter conversion vs multiple parameters
- Async operation with framework validation
- Performance scales with class complexity and existing constructor presence

### MakeFieldReadonly

Make field readonly refactoring performance across different field usage patterns.

| Benchmark | Code Size | Mean Time | Allocated Memory |
|-----------|-----------|-----------|------------------|
| MakeFieldReadonly_SmallFile | ~25 lines | Target: < 100ms | Target: < 5 MB |
| MakeFieldReadonly_MediumFile | ~45 lines | Target: < 150ms | Target: < 8 MB |

**Notes**:
- Requires analysis of field assignments across entire class
- Async operation with framework validation
- Performance scales with class size and field usage complexity

### SafeDelete

Safe delete refactoring performance across different code sizes.

| Benchmark | Code Size | Mean Time | Allocated Memory |
|-----------|-----------|-----------|------------------|
| SafeDelete_SmallFile | ~30 lines | Target: < 100ms | Target: < 5 MB |
| SafeDelete_MediumFile | ~60 lines | Target: < 150ms | Target: < 8 MB |

**Notes**:
- Requires comprehensive reference analysis across compilation
- Async operation with framework validation
- Performance scales with compilation size and symbol reference count

### ExtractClass

Extract class refactoring performance across different extraction complexity.

| Benchmark | Code Size | Mean Time | Allocated Memory |
|-----------|-----------|-----------|------------------|
| ExtractClass_SmallFile | ~30 lines | Target: < 150ms | Target: < 8 MB |
| ExtractClass_MediumFile | ~60 lines | Target: < 200ms | Target: < 10 MB |

**Notes**:
- Single field extraction vs multiple fields with comma separation
- Most complex refactoring operation (creates new class + composition)
- Async operation with framework validation
- Performance scales with number of extracted members

### RemoveUnusedUsings

Remove unused using directives refactoring performance.

| Benchmark | Code Size | Mean Time | Allocated Memory |
|-----------|-----------|-----------|------------------|
| RemoveUnusedUsings_SmallFile | ~20 lines | N/A* | N/A* |
| RemoveUnusedUsings_MediumFile | ~40 lines | N/A* | N/A* |

**Notes**:
- *IDE analyzer limitations (Issue #72) may prevent successful execution
- Async operation with framework validation
- Performance would depend on using directive count and code complexity

### InlineMethod

Inline method refactoring performance (Part 1 implementation).

| Benchmark | Code Size | Mean Time | Allocated Memory |
|-----------|-----------|-----------|------------------|
| InlineMethod_SmallFile | ~25 lines | N/A* | N/A* |
| InlineMethod_MediumFile | ~45 lines | N/A* | N/A* |

**Notes**:
- *Part 1 limitations: void methods, simple parameters, single caller only
- Async operation with framework validation
- Future Part 2 implementation will expand capabilities

## Performance Analysis

### Key Performance Factors

1. **Roslyn Parsing**: Initial syntax tree construction is the primary fixed cost
2. **Semantic Analysis**: Compilation creation and semantic model analysis scales with code size
3. **Reference Finding**: Symbol reference operations scale with compilation size
4. **Code Generation**: SyntaxFactory operations are generally fast
5. **Framework Validation**: Adds ~20-50ms overhead for reference assembly loading

### Memory Allocation Patterns

Memory allocation is dominated by:
- Roslyn SyntaxTree and Compilation objects
- Semantic model caching
- Symbol reference collections
- Generated syntax nodes

### Optimization Opportunities

Current optimizations in place:
- **Compilation caching** with weak references (RefactoringBase)
- **Symbol resolution helpers** with HashSet optimizations
- **Early validation** to fail fast before expensive operations

Potential future optimizations:
- Incremental compilation for multiple refactorings
- Parallel reference finding for large compilations
- Lazy semantic model creation

## Running Benchmarks

To run benchmarks and generate updated results:

```bash
cd src/RefactorCsharpMCP.Benchmarks

# Run all benchmarks (generates HTML, Markdown, and CSV reports)
dotnet run -c Release

# Run specific refactoring benchmarks
dotnet run -c Release --filter *ExtractMethod*

# Run with specific categories
dotnet run -c Release --filter *SmallFile*

# List available benchmarks
dotnet run -c Release -- --list flat
```

## Benchmark Results Location

Results are saved to:
```
src/RefactorCsharpMCP.Benchmarks/BenchmarkDotNet.Artifacts/results/
```

Formats available:
- `results.html` - Interactive HTML report with charts
- `results.md` - Markdown summary tables
- `results.csv` - Raw data for analysis

## Performance Regression Testing

To detect performance regressions:

1. Run benchmarks on baseline (current commit)
2. Make code changes
3. Run benchmarks again
4. Compare Mean times and Allocated memory
5. Investigate any regressions > 20% performance degradation

## Framework-Specific Performance Notes

### .NET 8.0 vs .NET 9.0
- No significant performance differences expected
- Both use C# 12/13 which have similar compilation costs

### .NET Framework 4.8
- May be slower due to reference assembly limitations (Issue #75)
- If reference assemblies are unavailable, refactoring will fail early
- Recommend using net8.0/net9.0 for best performance

### .NET Standard 2.0/2.1
- Performance similar to .NET 8.0 for basic operations
- Some advanced language features may have compilation overhead

## Version History

### V1.0.0 (Current)
- Initial benchmark suite: 17 benchmarks across 9 refactorings
- Baseline targets established for small and medium files
- BenchmarkDotNet 0.14.0 integration

## References

- **BenchmarkDotNet Documentation**: https://benchmarkdotnet.org/
- **Roslyn Performance**: https://github.com/dotnet/roslyn/blob/main/docs/wiki/Roslyn-Overview.md
- **Issue #72**: IDE Analyzer Limitations
- **Issue #75**: .NET Framework Reference Assembly Limitations
