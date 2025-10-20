# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

RefactorCsharpMCP is a Model Context Protocol (MCP) server providing Roslyn-based C# refactoring capabilities for AI clients. Built with .NET 8 and leveraging Microsoft's Roslyn compiler platform, it enables AI-assisted code refactoring through stdio transport integration with Claude Code and other MCP clients.

**Previous Location**: This project was originally part of the DevTools monorepo at https://github.com/sethb75/DevTools and was migrated to its own dedicated repository to allow independent development, releases, and documentation.

## Architecture

The project is organized into three main components:

- **RefactorCsharpMCP.Server**: MCP server with stdio transport, implements MCP tools for refactoring operations
- **RefactorCsharpMCP.Core**: Core refactoring logic using Roslyn, analysis utilities, and refactoring algorithms
- **RefactorCsharpMCP.Tests**: Comprehensive test suite with 165 tests covering unit, component, and integration testing

### Shared Refactoring Infrastructure

The Core project includes a shared infrastructure layer that eliminates boilerplate across refactorings:

- **RefactoringBase**: Abstract base class providing common functionality for all refactorings
  - Input validation (`ValidateNonEmpty`)
  - Syntax parsing and validation (`ParseAndValidateSyntax`)
  - **Compilation caching** with weak references for improved performance (`CreateCompilation`)
  - Common helper methods (`FindClass`, `FindMethod`)
  - **Structured error logging** with sanitized exception handling (`HandleException`, `RefactoringErrorContext`)
  - Framework-aware validation wrapper (`ExecuteWithValidationAsync`)
  - **Formatting preservation options** for whitespace normalization (`NormalizeWhitespace`, `RefactoringOptions`)
  - **Optional metrics tracking** for performance monitoring (`RefactoringMetrics`, `MetricsTracker`)
  - **Optional ILogger integration** for telemetry and diagnostics

- **SymbolResolutionHelper**: Utility class for Roslyn symbol operations
  - Position-based symbol resolution (`GetSymbolAtPosition`)
  - Symbol conflict detection with HashSet optimization (`FindSymbolConflicts`)
  - Symbol scope analysis (`AnalyzeSymbolScope`)
  - Reference finding across compilation (`GetAllReferences`)

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

## Build and Development

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

# Current test coverage: ~87% lines, ~83% branches (estimated after CR fixes)
# Total: 452 tests (424 unit + 20 component + 8 integration)
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
5. **Extract Class**: Extract fields and methods into a new class with composition pattern
6. **Remove Unused Usings**: Remove unused using directives detected via Roslyn diagnostics (IDE0005, CS8019), with framework-aware handling of global usings (C# 10+)

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
- Use `SymbolResolutionHelper` for position-based symbol operations and conflict detection

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
