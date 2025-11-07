# RefactorCsharpMCP Refactoring Opportunities

**Date**: 2025-11-06
**Purpose**: Dogfooding - Using our own refactoring tools to improve our codebase

## Overview

This document identifies opportunities to apply our own refactoring tools to improve the RefactorCsharpMCP codebase. This serves as both practical improvement and demonstration of the tools' capabilities.

**Update 2025-11-07**: Following comprehensive architectural analysis, we now have two levels of refactoring:
1. **SOLID-Level Refactorings**: Break down large files (>500 lines) into multiple classes following Single Responsibility Principle
2. **Method-Level Refactorings**: Extract complex methods within those classes for improved clarity

This document addresses both levels, with SOLID refactorings taking priority as they provide the foundation for sustainable growth.

---

## File Size Problem

**Critical Issue**: Our largest production files exceed 29KB, which causes practical limitations:
- **MCP Tool Invocation**: JSON payloads for 29KB files exceed Docker MCP Toolkit's practical limits
- **Maintainability**: Files over 500 lines violate Single Responsibility Principle
- **Testability**: Large files with mixed concerns are harder to unit test effectively
- **Cognitive Load**: Developers struggle to understand files with multiple responsibilities

### Recommended File Size Guidelines

Based on industry best practices and MCP compatibility:

| Metric | Soft Limit | Hard Limit | Rationale |
|--------|-----------|------------|-----------|
| **Lines of Code** | 300-400 | 500 | Maintains focus on single responsibility |
| **File Size** | 15KB | 20KB | Ensures MCP tool compatibility for dogfooding |
| **Methods per Class** | 10-15 | 20 | Prevents god classes |
| **Method Length** | 20-30 lines | 50 | Encourages extraction and composition |

**Action Required**: Any file exceeding soft limits should be reviewed for SOLID violations and decomposed into multiple classes.

---

## Large Files Requiring SOLID Refactoring

### Files Exceeding Guidelines

| File | Lines | Size | Status | Priority |
|------|-------|------|--------|----------|
| InlineMethod.cs | 978 | ~41KB | **Critical** | High |
| SyntaxValidator.cs | 659 | ~29KB | **Critical** | High |
| SymbolResolutionHelper.cs | 643 | ~27KB | **Critical** | High |
| ExtractMethod.cs | 581 | ~24KB | **Needs Review** | Medium |
| ExtractClass.cs | 573 | ~24KB | **Needs Review** | Medium |

**Test Files** (ExtractMethodTests.cs: 1683 lines, InlineMethodTests.cs: 1531 lines) are acceptable as they contain many independent test cases. However, consider grouping related tests into separate test classes if organization improves.

---

## Part A: SOLID-Level File Decomposition

These refactorings address architectural concerns by breaking down large files that violate Single Responsibility Principle.

### A1. SyntaxValidator.cs (659 lines → ~900 lines across 9 files)

**Current File**: `src/RefactorCsharpMCP.Core/Validation/SyntaxValidator.cs`
**Problem**: Violates Single Responsibility Principle - handles 7 distinct concerns
**Target Framework**: net8.0

#### Current Responsibilities (SRP Violations)

1. Parse diagnostic handling (lines 117-158)
2. Semantic diagnostic handling (lines 182-230)
3. Framework version detection (lines 293-363)
4. Error classification heuristics (lines 428-474)
5. BCL namespace identification (lines 537-577)
6. Typo detection
7. Compilation orchestration

#### Proposed Class Decomposition

Create new folder structure: `src/RefactorCsharpMCP.Core/Validation/`

**Recommended Folder Structure** (flatter, purpose-based):
```
Validation/
├── Handlers/          (diagnostic processing logic)
├── Framework/         (framework version detection)
└── Analysis/          (error analysis utilities)
```

**Strategy Pattern for Diagnostic Handlers**:
```csharp
// Base interface for common diagnostic handling pattern
public interface IDiagnosticHandler
{
    Task<ValidationResult> HandleAsync(
        IEnumerable<Diagnostic> diagnostics,
        FrameworkVersion targetFramework,
        CancellationToken cancellationToken = default);
}

// Specific interface for parse-time diagnostic handling
public interface IParseDiagnosticHandler : IDiagnosticHandler
{
    // Marker interface for type safety - enables specific DI registration
}

// Specific interface for semantic-time diagnostic handling
public interface ISemanticDiagnosticHandler : IDiagnosticHandler
{
    // Marker interface for type safety - enables specific DI registration
}
```

**Design Decision**: Use specific interfaces (`IParseDiagnosticHandler`, `ISemanticDiagnosticHandler`) to:
- Adhere to Interface Segregation Principle (ISP)
- Enable type-safe dependency injection without keyed services
- Allow independent evolution of parse and semantic concerns
- Improve code clarity and discoverability

**New Classes**:

1. **Handlers/ParseDiagnosticHandler.cs** (~200 lines)
   - Extract lines 117-158 from ValidateCompilationAsync
   - Responsibility: Handle syntax errors, language version mismatches
   - Dependencies: IFrameworkVersionDetector

2. **Handlers/SemanticDiagnosticHandler.cs** (~200 lines)
   - Extract lines 182-230 from ValidateCompilationAsync
   - Responsibility: Handle semantic errors, type errors
   - Dependencies: IApiClassifier, IBclValidator

3. **Handlers/DiagnosticClassifier.cs** (~150 lines)
   - Extract ExtractFeatureFromError (lines 268-286)
   - Responsibility: Classify diagnostic types
   - Pure function class

4. **Framework/FrameworkVersionDetector.cs** (~150 lines)
   - Extract DetectRequiredVersion (lines 293-363)
   - Responsibility: Map diagnostic IDs to required framework versions
   - Contains comprehensive C# version mapping

5. **Framework/FrameworkFeatureMapper.cs** (~100 lines)
   - Extract feature-to-version mapping logic
   - Responsibility: Map C# language features to framework versions
   - Static data class

6. **Analysis/BclNamespaceValidator.cs** (~100 lines)
   - Extract KnownBclPrefixes (lines 537-577) and IsBclNamespace
   - Responsibility: Validate BCL namespace usage
   - Static utility class

7. **Analysis/TypoDetector.cs** (~100 lines)
   - Extract typo detection heuristics
   - Responsibility: Differentiate typos from missing APIs
   - Levenshtein distance algorithms

8. **Analysis/ApiAvailabilityChecker.cs** (~150 lines)
   - Extract ClassifyApiErrors (lines 428-474)
   - Responsibility: Check API availability across frameworks
   - Dependencies: IBclValidator

9. **SyntaxValidator.cs** (refactored, ~150 lines)
   - Orchestrates diagnostic handlers (Facade Pattern)
   - Maintains backward compatibility
   - Clean entry point for validation

#### Implementation Approach

**Use Facade Pattern** for backward compatibility:
```csharp
public class SyntaxValidator
{
    private readonly IParseDiagnosticHandler _parseHandler;
    private readonly ISemanticDiagnosticHandler _semanticHandler;
    private readonly IFrameworkVersionDetector _versionDetector;

    public async Task<ValidationResult> ValidateCompilationAsync(
        string sourceCode,
        string targetFramework,
        CancellationToken cancellationToken = default)
    {
        // Orchestrate handlers
        var parseResult = await _parseHandler.HandleAsync(...);
        if (!parseResult.IsSuccess) return parseResult;

        var semanticResult = await _semanticHandler.HandleAsync(...);
        return semanticResult;
    }
}
```

**Benefits**:
- Each class has single, clear responsibility
- Easier to test individual handlers
- Can add new handlers without modifying existing code (Open/Closed)
- Framework detection logic isolated for reuse
- ~150-line entry point vs 659-line monolith

#### Dependency Injection Strategy

**Service Lifetime Decisions**

All validation services use **Singleton** lifetime for optimal performance:

| Service | Lifetime | Rationale |
|---------|----------|-----------|
| **IDiagnosticHandler implementations** | **Singleton** | Stateless handlers with no per-request state |
| **IFrameworkVersionDetector** | **Singleton** | Pure mapping logic with static data |
| **IApiClassifier** | **Singleton** | Classification logic with static heuristics |
| **IBclValidator** | **Singleton** | Namespace validation with readonly data structures |
| **SyntaxValidator (facade)** | **Singleton** | Orchestrator with immutable dependencies |
| **ReferenceAssemblyResolver** | **Singleton** | **Must be shared** - caches reference assemblies for reuse |

**Registration Code**

Add to `src/RefactorCsharpMCP.Server/Program.cs` after `.WithToolsFromAssembly()`:

```csharp
// Register shared infrastructure
builder.Services.AddSingleton<ReferenceAssemblyResolver>();

// Register validation services (all stateless, safe as singletons)
builder.Services.AddSingleton<IFrameworkVersionDetector, FrameworkVersionDetector>();
builder.Services.AddSingleton<IApiClassifier, ApiClassifier>();
builder.Services.AddSingleton<IBclValidator, BclNamespaceValidator>();
builder.Services.AddSingleton<IParseDiagnosticHandler, ParseDiagnosticHandler>();
builder.Services.AddSingleton<ISemanticDiagnosticHandler, SemanticDiagnosticHandler>();
builder.Services.AddSingleton<SyntaxValidator>();
```

**MCP Tool Integration**

Update all 11 MCP tool classes to inject `SyntaxValidator` via constructor:

```csharp
public class ExtractMethodTool : McpTool
{
    private readonly SyntaxValidator _validator;

    public ExtractMethodTool(SyntaxValidator validator)
    {
        _validator = validator;
    }

    public async Task<object> ExtractMethod(string sourceCode, ...)
    {
        var result = await _validator.ValidateInputAsync(sourceCode, targetFramework);
        // ...
    }
}
```

**Performance Benefits**

Expected improvements from singleton services with shared caching:

| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| **Validator creation** | ~50ms | ~0.01ms | **5000x faster** |
| **Memory per request** | ~2MB | ~0KB | **Minimal allocation** |
| **Throughput** | ~20/sec | ~200/sec | **10x improvement** |

---

### A2. InlineMethod.cs (978 lines → ~1200 lines across 6 files)

**Current File**: `src/RefactorCsharpMCP.Core/Refactorings/InlineMethod.cs`
**Problem**: Complex refactoring logic mixing multiple concerns
**Target Framework**: net8.0

#### Current Responsibilities (SRP Violations)

1. Method resolution and validation (lines 86-108)
2. Reference finding and analysis (lines 111-128)
3. Identifier conflict detection (lines 392-474)
4. Parameter mapping (lines 476-585)
5. Body transformation (lines 272-390)
6. Syntax node tracking (lines 148-165)

#### Proposed Class Decomposition

Create folder: `src/RefactorCsharpMCP.Core/Refactorings/InlineMethod/`

**New Classes**:

1. **InlineMethod.cs** (refactored, ~150 lines)
   - Orchestrates refactoring process
   - Main Execute() method
   - Delegates to specialized services

2. **MethodResolver.cs** (~200 lines)
   - Extract method resolution (lines 86-100)
   - Find method declaration at position
   - Validate method can be inlined

3. **ReferenceAnalyzer.cs** (~200 lines)
   - Extract reference finding (lines 111-128)
   - Analyze call sites
   - Build reference graph

4. **ConflictResolver.cs** (~250 lines)
   - Extract ResolveIdentifierConflicts (lines 392-474)
   - Scope name gathering
   - Generate unique names with _1 suffix

5. **ParameterMapper.cs** (~300 lines)
   - Extract parameter mapping logic (lines 476-585)
   - MapParametersToArguments
   - Handle complex argument expressions

6. **BodyTransformer.cs** (~200 lines)
   - Extract body transformation (lines 272-390)
   - PrepareMethodBody
   - Apply parameter substitutions
   - Preserve trivia

**Benefits**:
- Clear separation of concerns
- Each component independently testable
- Parameter mapping logic reusable for other refactorings
- Conflict resolution strategies isolated

---

### A3. SymbolResolutionHelper.cs (643 lines → ~800 lines across 5 files)

**Current File**: `src/RefactorCsharpMCP.Core/Utilities/SymbolResolutionHelper.cs`
**Problem**: Mixing symbol operations with conflict detection and scope analysis
**Target Framework**: net8.0

#### Current Responsibilities (SRP Violations)

1. Position-based symbol resolution
2. Symbol conflict detection
3. Scope analysis
4. Reference finding across compilation
5. Symbol usage pattern analysis

#### Proposed Class Decomposition

Create folder: `src/RefactorCsharpMCP.Core/Utilities/Symbols/`

**New Classes**:

1. **PositionBasedResolver.cs** (~150 lines)
   - Extract GetSymbolAtPosition
   - Position-to-line/column conversion
   - SyntaxToken retrieval

2. **ConflictDetector.cs** (~200 lines)
   - Extract FindSymbolConflicts
   - HashSet optimizations
   - Conflict categorization

3. **ScopeAnalyzer.cs** (~150 lines)
   - Extract AnalyzeSymbolScope
   - Scope boundary detection
   - Variable lifetime analysis

4. **ReferenceLocator.cs** (~200 lines)
   - Extract GetAllReferences
   - Cross-compilation reference finding
   - Workspace integration

5. **SymbolResolutionHelper.cs** (refactored, ~100 lines)
   - Facade for common symbol operations
   - Maintains backward compatibility

**Benefits**:
- Clear separation between position resolution and semantic analysis
- Conflict detection optimizations in dedicated class
- Scope analysis reusable for other refactorings
- Easier to optimize reference finding independently

---

### A4. ExtractMethod.cs (581 lines → ~700 lines across 4 files)

**Current File**: `src/RefactorCsharpMCP.Core/Refactorings/ExtractMethod.cs`
**Problem**: Mixed concerns around extraction, analysis, and code generation
**Target Framework**: net8.0

#### Proposed Decomposition (Summary)

1. **ExtractMethod.cs** (~150 lines) - Orchestrator
2. **CodeSelectionAnalyzer.cs** (~200 lines) - Analyzes selected code region
3. **ParameterExtractor.cs** (~200 lines) - Determines required parameters/returns
4. **MethodGenerator.cs** (~150 lines) - Generates new method signature and body

---

### A5. ExtractClass.cs (573 lines → ~700 lines across 4 files)

**Current File**: `src/RefactorCsharpMCP.Core/Refactorings/ExtractClass.cs**
**Problem**: Mixed concerns around class creation, member extraction, and composition
**Target Framework**: net8.0

#### Proposed Decomposition (Summary)

1. **ExtractClass.cs** (~150 lines) - Orchestrator
2. **MemberSelector.cs** (~150 lines) - Selects fields/methods to extract
3. **ClassGenerator.cs** (~200 lines) - Generates new class definition
4. **CompositionBuilder.cs** (~150 lines) - Creates composition field and updates references

---

## Part B: Method-Level Refactorings

These refactorings improve clarity within individual classes by extracting complex methods.

## High-Priority Opportunities

### 1. SyntaxValidator.ValidateCompilationAsync

**File**: `src\RefactorCsharpMCP.Core\Validation\SyntaxValidator.cs`
**Method**: `ValidateCompilationAsync`
**Lines**: 69-264 (195 lines)
**Complexity**: High - Multiple concerns mixed together

#### Current Structure

The method handles 6 distinct concerns:
1. Input validation (lines 76-82)
2. Framework normalization and validation (lines 86-96)
3. Parse tree creation and parse error handling (lines 102-158)
4. Semantic compilation creation (lines 160-175)
5. Semantic error handling with API classification (lines 177-230)
6. Exception handling with error categorization (lines 235-263)

#### Recommended Refactorings

**Extract Method #1**: Parse error handling logic
```csharp
// Lines 112-158: Extract parse diagnostic handling
private ValidationResult HandleParseDiagnostics(
    List<Diagnostic> parseDiagnostics,
    LanguageVersion languageVersion,
    string targetFramework,
    ErrorCode mismatchErrorCode)
{
    // Language version error detection
    // Genuine syntax error handling
}
```

**Extract Method #2**: Semantic error handling logic
```csharp
// Lines 182-230: Extract semantic diagnostic handling
private ValidationResult HandleSemanticDiagnostics(
    List<Diagnostic> semanticDiagnostics,
    SyntaxTree syntaxTree,
    string targetFramework)
{
    // API error classification
    // Framework error vs typo detection
}
```

**Extract Method #3**: Compilation creation
```csharp
// Lines 160-175: Extract compilation setup
private CSharpCompilation CreateValidationCompilation(
    SyntaxTree syntaxTree,
    IEnumerable<MetadataReference> references,
    string targetFramework)
{
    // Compilation options
    // Nullable context
    // Assembly creation
}
```

#### Expected Outcome

- **Before**: 195-line method handling 6 concerns
- **After**: ~50-line orchestration method + 3-4 focused helper methods
- **Benefit**: Improved testability, clearer separation of concerns, easier maintenance

#### Similar Pattern

This is identical to the `OptionsValidator.Validate()` refactoring from passgen where validation concerns were extracted into separate methods.

---

### 2. InlineMethod.Execute

**File**: `src\RefactorCsharpMCP.Core\Refactorings\InlineMethod.cs`
**Method**: `Execute`
**Lines**: 54-201 (147 lines)
**Complexity**: Medium-High - Sequential phases with tracking logic

#### Current Structure

The method performs refactoring in 7 phases:
1. Input validation (lines 56-68)
2. Syntax parsing and compilation (lines 72-83)
3. Method resolution (lines 86-100)
4. Validation (lines 103-108)
5. Reference finding (lines 111-128)
6. Identifier conflict resolution (lines 134-145)
7. Inlining and cleanup (lines 148-195)

#### Recommended Refactorings

While the method is already well-structured with clear phases, the identifier conflict resolution and node tracking logic could be extracted:

**Extract Method #1**: Node tracking setup
```csharp
// Lines 148-165: Extract node tracking logic
private (CompilationUnitSyntax trackedRoot, List<InvocationExpressionSyntax> trackedReferences)
    SetupNodeTracking(
        CompilationUnitSyntax root,
        MethodDeclarationSyntax originalDeclaration,
        List<InvocationExpressionSyntax> references)
{
    // Track nodes
    // Validate tracking
}
```

**Extract Method #2**: Post-inline cleanup
```csharp
// Lines 175-186: Extract method removal logic
private CompilationUnitSyntax RemoveInlinedMethod(
    CompilationUnitSyntax newRoot,
    SyntaxNode originalDeclaration)
{
    // Find tracked declaration
    // Remove from tree
}
```

#### Expected Outcome

- **Before**: 147-line method
- **After**: ~100-line method + 2 helper methods
- **Benefit**: Reduced complexity, reusable tracking logic

---

### 3. InlineMethod.ResolveIdentifierConflicts

**File**: `src\RefactorCsharpMCP.Core\Refactorings\InlineMethod.cs`
**Method**: `ResolveIdentifierConflicts`
**Lines**: 392-474 (82 lines)
**Complexity**: Medium - Multiple sub-concerns

#### Current Structure

The method handles:
1. Call site scope gathering (lines 411-425)
2. Rename suffix generation (lines 427-441)
3. Body renaming (lines 443-462)
4. MethodInfo reconstruction (lines 465-473)

#### Recommended Refactorings

**Extract Method #1**: Scope name gathering
```csharp
// Lines 411-425: Extract scope gathering
private HashSet<string> GatherAllScopeNames(
    List<InvocationExpressionSyntax> callSites,
    Compilation compilation)
{
    // Iterate call sites
    // Collect scope symbols
}
```

**Extract Method #2**: Generate unique names
```csharp
// Lines 427-441: Extract rename generation
private Dictionary<string, string> GenerateUniqueNames(
    HashSet<string> conflicts,
    HashSet<string> existingNames)
{
    // Generate suffixed names
    // Ensure uniqueness
}
```

#### Expected Outcome

- **Before**: 82-line method with multiple concerns
- **After**: ~40-line method + 2 helper methods
- **Benefit**: Reusable logic for scope analysis and name generation

---

## Medium-Priority Opportunities

### 4. Tool Input Validation Patterns

**Files**: `src\RefactorCsharpMCP.Server\Tools\*.cs`
**Pattern**: Repetitive input validation code across 11 tool files

#### Current Pattern

Each tool has similar validation code:
```csharp
if (string.IsNullOrWhiteSpace(sourceCode))
{
    return Task.FromResult<object>(new
    {
        success = false,
        error = "Source code cannot be empty",
        message = "Refactoring failed: Source code cannot be empty"
    });
}

if (sourceCode.Length > 1_000_000) // 1MB limit
{
    return Task.FromResult<object>(new
    {
        success = false,
        error = "Source code exceeds 1MB limit",
        message = "Refactoring failed: Source code exceeds 1MB limit"
    });
}
```

This pattern is repeated in:
- ExtractMethodTool.cs
- ConstructorInjectionTool.cs
- MakeFieldReadonlyTool.cs
- SafeDeleteTool.cs
- ExtractClassTool.cs
- RemoveUnusedUsingsTool.cs
- InlineMethodTool.cs
- RenameSymbolTool.cs
- FixDiagnosticTool.cs
- InlineVariableTool.cs
- AnalyzeCodeTool.cs

#### Recommended Refactoring

**Extract to Shared Helper**:
```csharp
// Create: src/RefactorCsharpMCP.Server/Tools/ToolInputValidator.cs
public static class ToolInputValidator
{
    public static object? ValidateSourceCode(string sourceCode, int maxSize = 1_000_000)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return new
            {
                success = false,
                error = "Source code cannot be empty",
                message = "Refactoring failed: Source code cannot be empty"
            };
        }

        if (sourceCode.Length > maxSize)
        {
            return new
            {
                success = false,
                error = $"Source code exceeds {maxSize / 1_000_000}MB limit",
                message = $"Refactoring failed: Source code exceeds {maxSize / 1_000_000}MB limit"
            };
        }

        return null; // Validation passed
    }
}
```

**Usage**:
```csharp
public Task<object> ExtractMethod(...)
{
    var validationError = ToolInputValidator.ValidateSourceCode(sourceCode);
    if (validationError != null)
        return Task.FromResult(validationError);

    // ... rest of method
}
```

#### Expected Outcome

- **Before**: ~15 lines per tool × 11 tools = 165 lines of duplicated code
- **After**: ~25 lines in shared class + ~3 lines per tool = 58 total lines
- **Benefit**: 65% reduction in validation code, single source of truth

---

### 5. DiagnosticAnalyzer Error Message Formatting

**File**: `src\RefactorCsharpMCP.Core\Diagnostics\DiagnosticAnalyzer.cs`
**Pattern**: Similar error message formatting repeated

This may have opportunities for extracting helper methods for error message formatting, though needs closer examination.

---

## Lower-Priority Opportunities

### 6. Test Setup Code

**Pattern**: Repeated test setup patterns across test files

While test code can tolerate some repetition, there may be opportunities to extract common setup patterns into helper methods or fixtures.

---

## Implementation Plan

**Updated 2025-11-07**: SOLID refactorings now take priority as they enable sustainable growth and resolve the MCP tool payload size limitations that prevented dogfooding.

### Sprint 1: Foundation - SyntaxValidator Decomposition (High Priority)

**Goal**: Break down the 659-line SyntaxValidator into 9 focused classes

**Tasks**:
1. Create folder structure: `Validation/Handlers/`, `Validation/Framework/`, `Validation/Analysis/`
2. Extract diagnostic handlers (ParseDiagnosticHandler, SemanticDiagnosticHandler, DiagnosticClassifier)
3. Extract framework detection (FrameworkVersionDetector, FrameworkFeatureMapper)
4. Extract error analysis (BclNamespaceValidator, TypoDetector, ApiAvailabilityChecker)
5. Refactor main SyntaxValidator to orchestrator (~150 lines)
6. Register services in Program.cs with DI container
7. Update all 11 MCP tools to inject SyntaxValidator
8. Update tests to use new class structure
9. Verify all tests pass

**Expected Time**: 8-12 hours
**Test Impact**: Moderate (may need test helper updates)
**Benefit**: Resolves largest file size issue, enables MCP dogfooding

#### Test Migration Strategy

**Philosophy**: Integration-first approach - maintain existing integration tests while adding focused unit tests for new classes.

**Test File Structure**:
```
tests/RefactorCsharpMCP.Tests/Validation/
├── SyntaxValidatorTests.cs                     (KEEP - ~400 lines, integration only)
├── Handlers/
│   ├── ParseDiagnosticHandlerTests.cs          (NEW - ~150 lines)
│   ├── SemanticDiagnosticHandlerTests.cs       (NEW - ~150 lines)
│   └── DiagnosticClassifierTests.cs            (NEW - ~100 lines)
├── Framework/
│   ├── FrameworkVersionDetectorTests.cs        (NEW - ~200 lines)
│   └── FrameworkFeatureMapperTests.cs          (NEW - ~100 lines)
└── Analysis/
    ├── BclNamespaceValidatorTests.cs           (NEW - ~100 lines)
    ├── TypoDetectorTests.cs                    (NEW - ~150 lines)
    └── ApiAvailabilityCheckerTests.cs          (NEW - ~150 lines)
```

**Migration Steps**:

1. **Phase 1**: Create test helpers (`ValidationTestHelpers.cs`) with shared utilities
2. **Phase 2**: Extract handler class and create corresponding unit test file
3. **Phase 3**: Migrate specific tests from SyntaxValidatorTests to new files
4. **Phase 4**: Trim SyntaxValidatorTests.cs to ~400 lines (integration only)
5. **Phase 5**: Verify all tests pass, coverage maintained at 87%+

**Test Coverage Tracking**:

| Phase | Total Tests | Coverage |
|-------|-------------|----------|
| **Baseline** | 491 | 87% |
| **After SyntaxValidator** | ~550 | 88% |

**Naming Conventions**:
- Test classes: `{ClassUnderTest}Tests.cs`
- Test methods: `{MethodUnderTest}_{Scenario}_{ExpectedOutcome}`
- Always use Arrange-Act-Assert pattern

### Sprint 2: High Complexity - InlineMethod Decomposition

**Goal**: Break down the 978-line InlineMethod into 6 focused classes

**Tasks**:
1. Create folder: `Refactorings/InlineMethod/`
2. Extract specialized services (MethodResolver, ReferenceAnalyzer, ConflictResolver, ParameterMapper, BodyTransformer)
3. Refactor main InlineMethod to orchestrator (~150 lines)
4. Update InlineMethodTests
5. Verify all tests pass

**Expected Time**: 20-30 hours (revised upward - most complex file, extensive test migration)
**Test Impact**: High (complex refactoring with many edge cases, 39 InlineMethod tests to update)
**Benefit**: Most complex file resolved, reusable components for other refactorings
**Recommendation**: Consider splitting into two sub-sprints (core decomposition + test migration)

### Sprint 3: Utility Decomposition - SymbolResolutionHelper

**Goal**: Break down 643-line SymbolResolutionHelper into 5 focused classes

**Tasks**:
1. Create folder: `Utilities/Symbols/`
2. Extract symbol operations (PositionBasedResolver, ConflictDetector, ScopeAnalyzer, ReferenceLocator)
3. Refactor SymbolResolutionHelper to facade (~100 lines)
4. Update all refactorings using SymbolResolutionHelper
5. Verify tests pass

**Expected Time**: 6-10 hours
**Test Impact**: Moderate (many consumers to update)
**Benefit**: Widely-used utility becomes more maintainable

### Sprint 4: Refactoring Decomposition - ExtractMethod & ExtractClass

**Goal**: Break down ExtractMethod (581 lines) and ExtractClass (573 lines)

**Tasks**:
1. Create folders for both refactorings
2. Extract specialized analyzers and generators
3. Refactor main classes to orchestrators
4. Update tests
5. Verify tests pass

**Expected Time**: 12-16 hours
**Test Impact**: High (core refactoring logic)
**Benefit**: All large files now under 400 lines

### Sprint 5: Polish & Method Extractions

**Goal**: Apply method-level refactorings and cleanup

**Tasks**:
1. **Tool Input Validation** - Extract to ToolInputValidator helper
   - Create shared validation helper
   - Update all 11 tool files
   - Expected time: 1-2 hours
   - Test impact: None

2. **Method extractions within refactored classes**
   - Apply extract_method refactorings as needed
   - Focus on methods >50 lines
   - Expected time: 4-6 hours

3. **Documentation updates**
   - Update CLAUDE.md with new architecture
   - Create architecture decision records (ADRs)
   - Update README with new folder structure

**Expected Time**: 8-10 hours
**Test Impact**: Low
**Benefit**: Code polish, improved documentation

---

### Total Estimated Time: 70-90 hours (~9-12 working days)

**Note**: Estimates revised upward based on architectural code review to account for:
- Test migration complexity (1196-line test file decomposition)
- DI registration and integration across 11 MCP tools
- Performance baseline establishment and validation
- More realistic assessment of InlineMethod complexity (978 lines)

### Parallel Work Opportunities

- Sprints 4 and 5 can be parallelized if multiple developers
- Tool input validation can be done alongside any sprint
- Documentation updates can happen throughout

---

## Testing Strategy

For each refactoring:

1. **Before refactoring**: Run full test suite and record results
   ```bash
   dotnet test
   ```

2. **Apply refactoring**: Use RefactorCsharpMCP tools to perform extraction

3. **After refactoring**:
   - Run full test suite
   - Verify all tests still pass
   - Compare coverage metrics

4. **Manual verification**:
   - Build project: `dotnet build`
   - Check for warnings
   - Review extracted methods for clarity

---

## Success Metrics

### Quantitative

- **Lines of Code Reduced**: Target 200+ lines (similar to passgen)
- **Methods Created**: Expect 10-15 new focused methods
- **Cyclomatic Complexity**: Reduce average complexity by 20-30%
- **Test Coverage**: Maintain or improve current 87% coverage

### Qualitative

- Improved readability and maintainability
- Better separation of concerns
- Easier to add new features
- Demonstrates RefactorCsharpMCP capabilities

---

## Dogfooding Benefits

1. **Validation**: Proves tools work on real, complex code
2. **Quality Improvement**: Makes codebase more maintainable
3. **Documentation**: Creates real-world before/after examples
4. **Confidence**: Shows we use what we build
5. **Discovery**: May reveal tool limitations or bugs

---

## Related Documents

- **Real-World Examples**: `.mcp-catalog/REAL_WORLD_EXAMPLES.md` - Examples from passgen project
- **Project Plan**: `docs/project-plan.md` - Original project roadmap
- **CLAUDE.md**: Project-specific development guidelines

---

## Next Steps

1. Review this document for prioritization
2. Create branch: `refactor/dogfood-improvements`
3. Start with Phase 1: SyntaxValidator.ValidateCompilationAsync
4. Document each refactoring as it's applied
5. Update REAL_WORLD_EXAMPLES.md with our own examples

---

*This document demonstrates RefactorCsharpMCP's ability to analyze and identify refactoring opportunities in production code.*
