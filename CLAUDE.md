# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

RefactorCsharpMCP is a Model Context Protocol (MCP) server providing Roslyn-based C# refactoring capabilities for AI clients. Built with .NET 8 and leveraging Microsoft's Roslyn compiler platform, it enables AI-assisted code refactoring through stdio transport integration with Claude Code and other MCP clients.

**Previous Location**: This project was originally part of the DevTools monorepo at https://github.com/sethb75/DevTools and was migrated to its own dedicated repository to allow independent development, releases, and documentation.

## Architecture

The project is organized into three main components:

- **RefactorCsharpMCP.Server**: MCP server with stdio transport, implements MCP tools for refactoring operations
  - **Utilities**: Shared validation helpers (`ToolInputValidator`) consolidating input validation across all 11 MCP tools (Sprint 5, Issue #92)
- **RefactorCsharpMCP.Core**: Core refactoring logic using Roslyn, analysis utilities, and refactoring algorithms
- **RefactorCsharpMCP.Tests**: Comprehensive test suite with 1063 tests covering unit, component, and integration testing (1045 passing, 98.3%)

### Shared Refactoring Infrastructure

The Core project includes a shared infrastructure layer that eliminates boilerplate across refactorings:

- **RefactoringBase**: Abstract base class providing common functionality for all refactorings
  - Input validation (`ValidateNonEmpty`)
  - Syntax parsing and validation (`ParseAndValidateSyntax`)
  - **Compilation caching** with weak references for improved performance (`CreateCompilation`)
  - Common helper methods (`FindClass`, `FindMethod`)
  - **Structured error logging** with sanitized exception handling (`HandleException`, `RefactoringErrorContext`)
  - Framework-aware validation wrapper (`ExecuteWithValidationAsync`)
  - **Compilation validation** with framework-specific BCL references (`ValidateCompilationWithFrameworkAsync`, Issue #115)
  - **Formatting preservation options** for whitespace normalization (`NormalizeWhitespace`, `RefactoringOptions`)
  - **Optional metrics tracking** for performance monitoring (`RefactoringMetrics`, `MetricsTracker`)
  - **Optional ILogger integration** for telemetry and diagnostics

- **Symbol Resolution Utilities** (decomposed in Sprint 3, Issue #90): Specialized classes for Roslyn symbol operations
  - **SymbolResolutionHelper** (facade, ~192 lines): Simplified API delegating to specialized classes
  - **PositionBasedResolver** (~285 lines): Position-to-symbol resolution with SyntaxTree identity preservation
    - `GetSymbolAtPosition(sourceCode, line, column)`: Standalone resolution
    - `GetSymbolAtPosition(semanticModel, syntaxTree, line, column)`: Identity-preserving overload
  - **ConflictDetector** (~283 lines): Comprehensive conflict detection using dual-strategy scanning
    - `FindSymbolConflicts()`: HashSet-optimized detection of local variables, parameters, lambdas, methods, fields
  - **ScopeAnalyzer** (~70 lines): Symbol scope and accessibility analysis
    - `AnalyzeSymbolScope()`: Determines symbol kind, containing type, and accessibility modifiers
  - **ReferenceLocator** (~68 lines): Reference finding across compilation
    - `GetAllReferences()`: Finds all usages of a symbol within a compilation
  - **Structure**: Located in `src/RefactorCsharpMCP.Core/Utilities/Symbols/`
  - **Benefits**: 70% reduction in main file size, independent optimization, backward compatibility via facade

- **RefactoringResult**: Standardized result type for all refactoring operations
  - Success/failure status with refactored code
  - Framework validation result integration
  - User-friendly error messages

- **RefactoringErrorContext**: Structured error context for debugging and telemetry
  - Error categorization (InvalidInput, InvalidState, ParseError, etc.)
  - Phase tracking (Validation, Parsing, Analysis, Transformation)
  - Source location capture
  - Sanitized user messages vs detailed log messages

- **RefactoringOptions**: Configurable refactoring behavior
  - Formatting preservation (`PreserveFormatting`) - fully implemented
  - Comment preservation - always enabled via Roslyn trivia (explicit options may be added in future versions)

- **RefactoringMetrics**: Performance and operational metrics
  - Execution timing with stopwatch
  - Success/failure counts and categorization
  - Complexity metrics (lines changed, nodes affected)
  - Framework-specific tracking

This infrastructure has reduced refactoring implementation boilerplate by an average of 29% (275+ lines eliminated across the five refactorings), making it faster to implement new refactorings while maintaining consistency, code quality, and observability.

### ExtractClass Facade Pattern Architecture

The **ExtractClass** refactoring implements a clean facade pattern (Issue #91), decomposing a 661-line monolithic orchestrator into specialized components:

- **ExtractClass.cs** (176 lines, facade): Thin orchestration layer providing input validation and delegation
  - Input validation for source code, class names, and member lists
  - Exception handling with structured error contexts
  - Delegation to ExtractClassOrchestrator for core logic
  - Optional async wrapper with framework-aware compilation validation

- **ExtractClassOrchestrator** (200 lines): Core orchestration workflow
  - Coordinates member validation, symbol resolution, and reference updates
  - Manages semantic analysis and tree transformation sequence
  - Preserves SyntaxTree identity for accurate symbol resolution
  - Located in `src/RefactorCsharpMCP.Core/Refactorings/ExtractClassComponents/`

- **MemberSelector** (163 lines): Member name parsing and validation
  - Parses comma/semicolon-separated member names
  - Validates field, method, and nested type existence
  - Supports delegate type detection and error reporting
  - Re-finds members after tree mutations for fresh syntax nodes

- **ReferenceUpdater** (246 lines): Reference finding and transformation coordinator
  - Resolves symbols for extracted members using semantic model
  - Categorizes references (same-class vs external) for targeted updates
  - **Syntax-based fallback** (Issue #118): When semantic analysis fails due to unresolved dependencies, falls back to identifier matching
  - Builds warning messages for external references requiring manual updates

- **ReferenceTransformer** (CSharpSyntaxRewriter): Syntax tree rewriter
  - Transforms member access expressions to route through composition field
  - Handles method invocations, identifier references, and qualified type names
  - **Location-based matching** (Issue #118): Uses TextSpan for robust reference updates when semantic model is unreliable

**Benefits of Facade Pattern:**
- 73% reduction in main file size (661 → 176 lines, 485 lines eliminated)
- Clear separation of concerns: validation, orchestration, member selection, reference updates
- Independent testing and optimization of each component
- Simplified maintenance with focused responsibilities
- Robust handling of code with unresolved dependencies (Issue #118)

**Key Design Decision (Issue #118):**
The syntax-based fallback in ReferenceUpdater addresses scenarios where semantic analysis fails (missing type references, unresolved dependencies). This dual-strategy approach ensures ExtractClass works reliably on incomplete codebases during incremental refactoring.

**Transform Planning Pattern (Issues #120, #124):**
Solves the Roslyn Identity Paradox for nested type qualification by separating semantic analysis from tree transformation:

- **TransformationPlan**: Data structure capturing type qualification metadata (field types, local variables, properties) using TextSpan-based location tracking
- **AnalyzeTypeQualifications**: Analysis phase that uses SemanticModel on the ORIGINAL unmodified syntax tree to identify all type references needing qualification
- **TypeQualificationTransformer**: Location-based transformer applying qualifications without requiring SemanticModel, allowing it to work on modified trees

**Three-Phase Pipeline:**
1. **PHASE 1 (Analysis)**: Analyze original tree with SemanticModel to collect transformation metadata keyed by TextSpan
2. **PHASE 2 (Transform References)**: Update member references to route through composition field
3. **PHASE 3 (Transform Types)**: Apply type qualifications using location-based matching (no SemanticModel conflicts)

**Current Support:**
- ✅ Field declarations: `Config _config` → `Configuration.Config _config`
- ✅ Local variable declarations: `Config local` → `Configuration.Config local`
- ✅ Property declarations: `Config Settings` → `Configuration.Config Settings`
- ✅ All nested type kinds: classes, records, structs, enums, interfaces

**Known Limitations:**
- **Return types and parameter types**: Not yet qualified when they remain in the extracted method (by design - no qualification needed in same class)
- **Generic type arguments**: Planned for future enhancement
- **Local Variable Name Collisions**: Syntax-based fallback includes filtering to skip local variable declarations, preventing false positives when local variables have the same name as extracted members.

**Performance Characteristics:**
- **Semantic-Based (Primary)**: O(1) symbol lookup + O(n) references via Roslyn's indexed search. Highly efficient for well-formed code with resolved dependencies.
- **Syntax-Based (Fallback)**: O(n) tree traversal per extracted symbol where n = number of syntax nodes in source class. Only triggers when semantic search returns zero results (typically due to unresolved dependencies).
- **Trade-off**: Syntax fallback sacrifices precision (name-based matching) for robustness (works without complete type information). Performance impact is minimal as it only activates when semantic analysis is unavailable.

## Build and Development

### Prerequisites

**Required Tools:**
- .NET 8.0 SDK
- GitHub CLI (`gh`) - Required for issue management and PR workflows (when using MCP Docker GitHub tools as fallback)

**Automated Setup (Claude Code Remote Environments):**

This project uses Claude Code SessionStart hooks to automatically install dependencies in remote environments. When you open this project in Claude Code web, the following happens automatically:

1. ✅ `.claude/settings.json` triggers `scripts/install_tools.sh`
2. ✅ Script detects remote environment (`CLAUDE_CODE_REMOTE=true`)
3. ✅ Installs .NET SDK 8.0 from packages.microsoft.com
4. ✅ Installs GitHub CLI from cli.github.com
5. ✅ Configures environment variables (DOTNET_ROOT, PATH)
6. ✅ Reports installation success with version info

**Manual Setup (Local Environments):**

For local development, install dependencies manually:

```bash
# .NET SDK 8.0
# Windows: https://dotnet.microsoft.com/download/dotnet/8.0
# macOS: brew install dotnet@8
# Linux: https://learn.microsoft.com/en-us/dotnet/core/install/linux

# GitHub CLI
# Windows: winget install GitHub.cli
# macOS: brew install gh
# Linux: https://cli.github.com/manual/installation
```

The installation script will skip automatically in local environments:
```bash
$ ./scripts/install_tools.sh
ℹ️  Local environment detected - skipping automated tool installation
```

**GitHub CLI Usage (Remote Environments):**

**IMPORTANT:** In Claude Code remote environments, the git remote uses a local proxy URL that gh CLI doesn't recognize. Therefore:

1. **Always use the full path** when calling gh:
   ```bash
   # Using dynamic path (recommended for portability)
   $(pwd)/.tools/gh/bin/gh [command]

   # Or with absolute path
   /path/to/RefactorCsharpMCP/.tools/gh/bin/gh [command]
   ```

2. **Always specify the repository** with `-R` flag:
   ```bash
   # List issues
   $(pwd)/.tools/gh/bin/gh issue list -R sethb75/RefactorCsharpMCP

   # View a specific issue
   $(pwd)/.tools/gh/bin/gh issue view 123 -R sethb75/RefactorCsharpMCP

   # List pull requests
   $(pwd)/.tools/gh/bin/gh pr list -R sethb75/RefactorCsharpMCP
   ```

3. **Why this is needed:**
   - Git remote: `http://local_proxy@127.0.0.1:20402/git/...` (proxy URL)
   - gh expects: `https://github.com/sethb75/RefactorCsharpMCP.git` (GitHub URL)
   - Solution: Explicit `-R sethb75/RefactorCsharpMCP` flag tells gh which repo to use

4. **Authentication:** Already configured via GH_TOKEN environment variable (no action needed)

### Building the Project
```bash
# Build all projects
dotnet build RefactorCsharpMCP.sln

# Build in Release mode
dotnet build RefactorCsharpMCP.sln -c Release
```

### Running the MCP Server
```bash
# Run from the Server project directory
cd src/RefactorCsharpMCP.Server
dotnet run

# Or run from published executable
dotnet publish -c Release
cd bin/Release/net8.0/publish
./RefactorCsharpMCP.Server
```

### Running Tests
```bash
# Run all tests
dotnet test

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"

# Current test coverage: ~87% lines, ~83% branches (estimated)
# Total: 1063 tests (1045 passing, 18 skipped, 0 failures)
# ExtractClass: 83 tests (all passing, including 7 new Transform Planning Pattern tests)
# Includes 39 InlineMethod tests (38 passing, 1 skipped for net48)
```

## Technology Stack

### Framework & Runtime
- **.NET 8**: Modern .NET SDK-style project format
- **C# 12**: Latest language features

### Key Dependencies
- **ModelContextProtocol 0.4.0-preview.1**: MCP SDK for stdio transport
- **Microsoft.CodeAnalysis.CSharp 4.14.0**: Roslyn compiler platform for C# analysis and transformation
- **Microsoft.Extensions.Hosting 9.0.9**: Dependency injection and hosting abstractions
- **xUnit 2.9.2**: Testing framework
- **NSubstitute 5.3.0**: Mocking framework for tests

## Available Refactorings

### Phase 1 (Implemented)
1. **Extract Method**: Extract selected code into a new method
2. **Constructor Injection**: Convert method parameters to constructor-injected fields
3. **Make Field Readonly**: Make fields readonly if only assigned in constructors
4. **Safe Delete**: Delete methods/classes after verifying no references exist
5. **Extract Class**: Extract fields and methods into a new class with composition pattern. **Includes optional compilation validation with framework-specific BCL references (enabled by default, Issue #115).**
6. **Remove Unused Usings**: Remove unused using directives detected via Roslyn diagnostics (IDE0005, CS8019), with framework-aware handling of global usings (C# 10+)
7. **Inline Method (Part 1)**: Inline a method by replacing its single invocation with the method's body. Supports void methods with simple parameters (primitives, string). Single caller only. Framework-aware with automatic syntax validation.

### Phase 2 (Planned - See docs/SDD-Framework-Version-Awareness.md)
- Framework Version Awareness: Support for targeting different .NET framework versions (net8.0, net48, netstandard2.0, etc.)
- Language Version Mapping: Automatic C# language version selection based on target framework
- Reference Assembly Loading: Cross-framework refactoring support

## Documentation

- **README.md**: User-facing documentation, installation, and usage guide
- **EXAMPLES.md**: Example refactorings with input/output code samples
- **E2E-TESTING.md**: End-to-end testing guide and integration test scenarios
- **TROUBLESHOOTING.md**: Common issues and solutions
- **docs/project-plan.md**: Original project plan and phased implementation
- **docs/PRD-Framework-Version-Awareness.md**: Product requirements for framework version awareness feature
- **docs/SDD-Framework-Version-Awareness.md**: Software design document for framework version awareness (v2.1.0)
- **docs/integration-testing.md**: Integration testing strategy and test cases

## Docker Deployment

The project includes Docker support for containerized deployment:

```bash
# Build Docker image
docker build -t refactor-csharp-mcp .

# Run with Docker Compose
docker-compose up

# Use deployment scripts
./scripts/deploy-docker.sh  # Linux/macOS
./scripts/deploy-docker.ps1 # Windows
```

## Security Scanning

Security scanning scripts are available for vulnerability detection:

```bash
# Run security scans
./scripts/security-scan.sh  # Linux/macOS
./scripts/security-scan.ps1 # Windows
```

## MCP Tool Integration

The server exposes MCP tools that can be called from Claude Code:

1. `extract_method`: Extract code into a new method
2. `constructor_injection`: Convert parameters to constructor-injected dependencies
3. `make_field_readonly`: Make fields readonly where safe
4. `safe_delete_method`: Delete methods with reference checking
5. `extract_class`: Extract fields/methods into a new class
6. `remove_unused_usings`: Remove unused using directives with framework-aware global using preservation
7. `inline_method`: Inline a method by replacing its single invocation with the method body (Part 1: void methods, simple parameters, single caller)

Each tool accepts source code and refactoring parameters, returning either:
- Success: Refactored source code
- Failure: Error message with diagnostic information

## Development Workflow

### Making Changes
1. Create feature branch from master
2. Implement changes with tests
3. Run full test suite: `dotnet test`
4. Ensure build succeeds: `dotnet build`
5. Create pull request with clear description

### Code Quality
- Maintain test coverage above 85%
- Follow C# coding conventions
- Use Roslyn APIs for all code analysis and transformation
- Add XML documentation comments for public APIs
- Include unit tests for all new refactorings
- Inherit from `RefactoringBase` when implementing new refactorings to leverage shared infrastructure
- Use `SymbolResolutionHelper` (facade) for simple symbol operations, or inject specialized classes (`PositionBasedResolver`, `ConflictDetector`, `ScopeAnalyzer`, `ReferenceLocator`) directly for fine-grained control or batch operations

### Roslyn Best Practices
- Always work with SyntaxTree and SemanticModel for accuracy
- Use SyntaxFactory for code generation
- Preserve trivia (whitespace, comments) during transformations
- Validate syntax before and after refactorings
- Use data flow analysis for variable scope detection
- Leverage `RefactoringBase` methods for common operations:
  - `ParseAndValidateSyntax()` for parsing with error handling
  - `CreateCompilation()` for standardized compilation setup
  - `FindClass()` and `FindMethod()` for locating declarations
  - `HandleException()` for security-conscious error messages
  - `NormalizeWhitespace()` for consistent code formatting

#### Canonical Pattern for Position-Based Refactorings

**CRITICAL**: Roslyn's semantic analysis relies on **object identity** for SyntaxTree instances. When implementing position-based refactorings (e.g., RenameSymbol, ExtractMethodAtPosition), you must maintain the same SyntaxTree instance throughout the entire operation to ensure reference finding and symbol resolution work correctly.

**The Pattern**:
```csharp
public RefactoringResult Execute(string sourceCode, int lineNumber, int columnNumber, ...)
{
    // STEP 1: Parse once and validate
    CurrentPhase = "Syntax Parsing";
    var parseResult = ParseAndValidateSyntax(sourceCode, out var root, out var syntaxTree);
    if (!parseResult.IsSuccess || root == null || syntaxTree == null)
        return parseResult;

    // STEP 2: Create compilation (leverages cache if available)
    CurrentPhase = "Semantic Analysis";
    var compilation = CreateCompilation(syntaxTree);
    var semanticModel = compilation.GetSemanticModel(syntaxTree);

    // STEP 3: Resolve symbol using the SAME syntaxTree
    CurrentPhase = "Symbol Resolution";
    var symbolResult = _symbolHelper.GetSymbolAtPosition(
        semanticModel,    // Pass existing semantic model
        syntaxTree,       // Pass existing syntax tree
        lineNumber,
        columnNumber);

    if (!symbolResult.Success)
        return RefactoringResult.Failure(symbolResult.ErrorMessage);

    // STEP 4: Find references using the SAME compilation
    var references = _symbolHelper.GetAllReferences(symbolResult.Symbol, compilation);

    // STEP 5: Transform the SAME root
    var newRoot = TransformSyntax(root, ...);

    return RefactoringResult.Success(newRoot.ToFullString(), message);
}
```

**Key Principles**:
1. **Single Parse**: Call `ParseAndValidateSyntax()` exactly once at the beginning
2. **Identity Preservation**: Use the returned `syntaxTree` instance everywhere - never re-parse the same source code
3. **Cache Leverage**: `CreateCompilation(syntaxTree)` uses `ConditionalWeakTable` for caching based on SyntaxTree object identity
4. **Consistent Context**: All semantic operations (symbol resolution, reference finding) use the same `Compilation` and `SemanticModel`
5. **Enhanced Helper**: Use `GetSymbolAtPosition(semanticModel, syntaxTree, ...)` overload to maintain SyntaxTree identity

**Why This Matters**: If you create a new SyntaxTree from the same source code string, Roslyn treats it as a completely different tree (even though the content is identical). Symbol locations from one tree won't match nodes in another tree, causing reference finding to return zero results.

## Project History

This project was created as part of the DevTools repository and later migrated to its own repository to allow:
- Independent release cycles
- Dedicated issue tracking
- Focused documentation
- Standalone distribution

All commit history has been preserved during the migration using git filter-branch.

## Future Enhancements

See **docs/SDD-Framework-Version-Awareness.md** for detailed plans on:
- .NET Framework version awareness (v1.0.0)
- Multi-framework refactoring support
- Language version mapping
- Reference assembly resolution
- Enhanced error handling with structured error codes

---

## Private Configuration Overlay

The following private configuration extends these project-specific guidelines with personal preferences, MCP configurations, and private instructions. This file is bootstrapped from a private repository at session start.

@.claude/private-config/bootstrap/CLAUDE.private.md
