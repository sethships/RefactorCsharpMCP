# Code Coverage Gap Analysis - V1 Release

**Generated**: 2025-11-03
**Test Suite**: 761 tests (749 passing, 12 skipped)

## Executive Summary

**Current Coverage**:
- **Lines**: 77.5% (4,629 / 5,974 covered)
- **Branches**: 58.9% (1,535 / 2,604 covered)

**V1 Targets**:
- **Lines**: 90.0% (**+12.5% gap** - need ~750 more lines covered)
- **Branches**: 85.0% (**+26.1% gap** - need ~680 more branches covered)

## Package-Level Coverage

| Package | Line Coverage | Branch Coverage | Status |
|---------|--------------|----------------|--------|
| **RefactorCsharpMCP.Core** | 82.0% | 60.1% | ⚠️ Need +8% lines, +24.9% branches |
| **RefactorCsharpMCP.Server** | 46.7% | 45.5% | ❌ Need +43.3% lines, +39.5% branches |

## Critical Coverage Gaps

### High Priority: Zero Coverage (0% lines)

These classes/methods are completely untested:

#### RefactorCsharpMCP.Core
1. **SyntaxConversionPipeline** (0% lines, 0% branches)
   - File: `SyntaxConversion/SyntaxConversionPipeline.cs`
   - Impact: HIGH - Framework version conversion orchestration
   - Recommendation: Add integration tests for cross-framework syntax conversions

#### RefactorCsharpMCP.Server (MCP Tools)
2. **InlineMethodTool** (0% lines, 0% branches)
   - File: `Tools/InlineMethodTool.cs`
   - Impact: MEDIUM - MCP entry point
   - Recommendation: Add MCP tool integration tests

3. **InlineVariableTool** (0% lines, 0% branches)
   - File: `Tools/InlineVariableTool.cs`
   - Impact: MEDIUM - MCP entry point
   - Recommendation: Add MCP tool integration tests

4. **RemoveUnusedUsingsTool** (0% lines, 0% branches)
   - File: `Tools/RemoveUnusedUsingsTool.cs`
   - Impact: MEDIUM - MCP entry point
   - Recommendation: Add MCP tool integration tests

5. **RenameSymbolTool** (0% lines, 0% branches)
   - File: `Tools/RenameSymbolTool.cs`
   - Impact: MEDIUM - MCP entry point
   - Recommendation: Add MCP tool integration tests

### Medium Priority: Low Coverage (< 50% lines)

6. **FileSystemRetryHelper** (42.3% lines, 20.0% branches)
   - File: `Infrastructure/FileSystemRetryHelper.cs`
   - Missing: Retry logic, failure scenarios, transient errors
   - Recommendation: Add tests for retry scenarios, timeout handling, max retries

7. **SyntaxValidator** (48.1% lines, 16.0% branches)
   - File: `Validation/SyntaxValidator.cs`
   - Missing: Feature extraction, version detection for various C# features
   - Recommendation: Add tests for C# feature detection across language versions

8. **ConstructorInjectionTool** (47.8% lines, 60% branches)
   - File: `Tools/ConstructorInjectionTool.cs`
   - Missing: Error scenarios, validation paths
   - Recommendation: Add error handling and validation tests

### Low Priority: Moderate Coverage (50-80% lines)

9. **ReferenceAssemblyResolver.ResolveFromNuGetAsync** (65.2% lines, 50% branches)
   - File: `Infrastructure/FrameworkSupport/ReferenceAssemblyResolver.cs`
   - Missing: NuGet download failures, cache scenarios
   - Recommendation: Add NuGet package resolution tests

10. **NuGetPackageDownloader** (70.2% lines, 53.4% branches)
    - File: `Infrastructure/FrameworkSupport/NuGetPackageDownloader.cs`
    - Missing: Download failures, partial downloads, corrupted packages
    - Recommendation: Add error scenario tests for NuGet operations

11. **DiagnosticAnalyzer** (90.3% lines, **30.6% branches**)
    - File: `Diagnostics/DiagnosticAnalyzer.cs`
    - Missing: Branch coverage for various diagnostic types
    - Recommendation: Add tests for different diagnostic categories and edge cases

## Recommended Test Additions (Priority Order)

### Phase 1: Server/MCP Tool Coverage (+43.3% lines, +39.5% branches)
**Target**: Bring Server package to 90% line coverage

1. **Create MCP Tool Integration Tests** (Est. +35% Server coverage)
   - File: `RefactorCsharpMCP.Tests/Tools/McpToolIntegrationTests.cs`
   - Test all MCP tools end-to-end with realistic inputs
   - Cover success paths, validation failures, error scenarios
   - Estimated: 15-20 new tests

2. **Expand Existing Tool Tests** (Est. +8% Server coverage)
   - Add error scenario tests to partially-covered tools
   - Test parameter validation exhaustively
   - Test framework-specific behaviors
   - Estimated: 10-15 new tests

### Phase 2: Core Infrastructure Coverage (+5% lines, +15% branches)
**Target**: Bring critical infrastructure to 80%+ coverage

3. **SyntaxValidator Comprehensive Tests** (Est. +3% overall coverage)
   - File: `RefactorCsharpMCP.Tests/Validation/SyntaxValidatorTests.cs`
   - Test feature extraction for C# 7-12 features
   - Test version detection logic
   - Test compilation validation with various error types
   - Estimated: 20-25 new tests

4. **FileSystemRetryHelper Tests** (Est. +1% overall coverage)
   - File: `RefactorCsharpMCP.Tests/Infrastructure/FileSystemRetryHelperTests.cs`
   - Test retry scenarios with transient failures
   - Test timeout handling
   - Test max retry limits
   - Estimated: 8-10 new tests

5. **NuGet Infrastructure Tests** (Est. +1% overall coverage)
   - Expand `ReferenceAssemblyResolverTests.cs`
   - Add `NuGetPackageDownloaderTests.cs`
   - Test download failures, cache behavior, package extraction
   - Estimated: 10-12 new tests

### Phase 3: Diagnostic & Refactoring Branch Coverage (+5 branches)
**Target**: Increase branch coverage in well-tested classes

6. **DiagnosticAnalyzer Branch Coverage** (Est. +2% branch coverage)
   - Add tests for each diagnostic category mapping
   - Test edge cases in custom IDE diagnostics
   - Test framework-specific compilation options
   - Estimated: 8-10 new tests

7. **Refactoring Branch Coverage** (Est. +3% branch coverage)
   - Add edge case tests to refactorings
   - Test error path branches
   - Test validation failure branches
   - Estimated: 15-20 new tests across all refactorings

### Phase 4: Framework Matrix Tests (+2% lines, +3% branches)
**Target**: Ensure cross-framework compatibility

8. **Cross-Framework Test Matrix**
   - Create parameterized tests running refactorings across all supported frameworks
   - Test framework-specific syntax conversions
   - Test language version mappings
   - Estimated: 10-15 new theory tests

## Estimated Test Count to Reach Targets

| Phase | Tests to Add | Line Coverage Gain | Branch Coverage Gain | Total Line % | Total Branch % |
|-------|-------------|-------------------|---------------------|--------------|----------------|
| Current | - | - | - | 77.5% | 58.9% |
| Phase 1 | 25-35 | +8% | +10% | 85.5% | 68.9% |
| Phase 2 | 40-50 | +5% | +15% | 90.5% | 83.9% |
| Phase 3 | 25-30 | +0% | +5% | 90.5% | 88.9% |
| **Final** | **90-115** | **+13%** | **+30%** | **90.5%** | **88.9%** |

## Quick Wins (Highest ROI)

### Top 5 Highest-Impact Test Files to Create/Expand:

1. **McpToolIntegrationTests.cs** (NEW)
   - Coverage gain: ~35% of Server package
   - Effort: Medium (3-4 hours)
   - Priority: **CRITICAL**

2. **SyntaxValidatorTests.cs** (EXPAND)
   - Coverage gain: ~3% overall
   - Effort: Medium (2-3 hours)
   - Priority: **HIGH**

3. **InlineMethodToolTests.cs** (NEW)
   - Coverage gain: ~10% of Server package
   - Effort: Low (1-2 hours)
   - Priority: **HIGH**

4. **FileSystemRetryHelperTests.cs** (NEW)
   - Coverage gain: ~1% overall
   - Effort: Low (1 hour)
   - Priority: **MEDIUM**

5. **DiagnosticAnalyzerBranchTests.cs** (NEW)
   - Coverage gain: ~2% branch coverage
   - Effort: Low (1-2 hours)
   - Priority: **MEDIUM**

## Files Not Requiring Additional Tests

These files already meet or exceed targets:

### RefactorCsharpMCP.Core (Well Covered)
- ✅ `ExtractMethod.cs` - 90.2% lines, 74.6% branches
- ✅ `MakeFieldReadonly.cs` - 95.4% lines, 86.3% branches
- ✅ `ExtractClass.cs` - 89.0% lines, 82.1% branches
- ✅ `ConstructorInjection.cs` - 87.9% lines, 77.1% branches
- ✅ `TupleReturnConverter.cs` - 92.2% lines, 91.3% branches
- ✅ `RemoveUnusedUsings.cs` - Likely well-covered (check specific numbers)

## Action Plan for V1

### Sprint 1 (Next 3 days)
- [ ] Create `McpToolIntegrationTests.cs` (25-35 tests)
- [ ] Expand `SyntaxValidatorTests.cs` (20-25 tests)
- [ ] Measure coverage after Sprint 1
- [ ] **Goal**: Reach 85% lines, 75% branches

### Sprint 2 (Following 2 days)
- [ ] Create `FileSystemRetryHelperTests.cs` (8-10 tests)
- [ ] Create `NuGetPackageDownloaderTests.cs` (10-12 tests)
- [ ] Add branch coverage tests to refactorings (15-20 tests)
- [ ] Measure coverage after Sprint 2
- [ ] **Goal**: Reach 90% lines, 85% branches

### Sprint 3 (Final 1 day)
- [ ] Add framework matrix tests (10-15 tests)
- [ ] Fill remaining gaps identified in Sprint 2 measurements
- [ ] Final coverage measurement
- [ ] **Goal**: Confirm 90%+ lines, 85%+ branches

## Notes

- **Compiler-generated async state machines** (e.g., `<ExecuteAsync>d__0`) showing 0% coverage are expected and don't require explicit testing
- **Display class closures** (e.g., `<>c__DisplayClass0_0`) are tested implicitly through parent method tests
- Focus on **meaningful coverage** of business logic, not just hitting line numbers
- **SyntaxConversionPipeline** at 0% may indicate unused code that can be removed or needs integration

## Tools & Commands

```bash
# Generate coverage report
dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults

# View detailed coverage (requires reportgenerator)
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"CoverageReport" -reporttypes:Html

# Open coverage report
start CoverageReport/index.html  # Windows
open CoverageReport/index.html   # macOS
xdg-open CoverageReport/index.html  # Linux
```

## Related Documents

- [Test Coverage Validation (Issue #29)](https://github.com/sethb75/RefactorCsharpMCP/issues/29)
- [E2E Testing Guide](../E2E-TESTING.md)
- [Integration Testing Strategy](../integration-testing.md)
