# RefactorCsharpMCP

A Model Context Protocol (MCP) server that provides Roslyn-based refactoring capabilities for C# code, designed for seamless integration with AI clients like Claude Code, Cursor, VS Code, and others.

## Overview

RefactorCsharpMCP enables AI-assisted refactoring of C# code through the Model Context Protocol. It uses Microsoft's Roslyn compiler platform to perform sophisticated code transformations while maintaining code correctness.

### Key Features

- **Roslyn-Based Refactoring**: Leverages Microsoft's Roslyn for accurate C# code analysis and transformation
- **MCP Protocol Support**: Standard stdio transport for AI client integration
- **Docker Desktop MCP Toolkit Compatible**: One-click deployment from Docker Desktop
- **Multi-Framework Support**: Works with .NET Framework 4.5.2+ and .NET 8+
- **Comprehensive Testing**: Extensive test coverage ensures reliability

## Technology Stack

- **.NET 8**: Modern runtime for the MCP server
- **Roslyn**: Microsoft.CodeAnalysis for C# syntax analysis
- **MCP SDK**: ModelContextProtocol NuGet package
- **xUnit**: Testing framework

## Project Structure

```
RefactorCsharpMCP/
├── docs/                           # Documentation
│   └── project-plan.md            # Comprehensive development plan
├── src/
│   ├── RefactorCsharpMCP.Server/        # MCP server console application
│   │   └── Tools/                 # MCP tool implementations
│   ├── RefactorCsharpMCP.Core/          # Roslyn refactoring logic
│   │   ├── Refactorings/          # Individual refactoring implementations
│   │   └── Analysis/              # Code analysis utilities
│   └── RefactorCsharpMCP.Tests/         # Unit and integration tests
├── examples/                       # Example code for testing
├── RefactorCsharpMCP.sln                # Solution file
├── .gitignore                     # Git ignore rules
└── .dockerignore                  # Docker build exclusions
```

## Quick Start

### Prerequisites

**REQUIRED for all environments (native, WSL, Docker, CI/CD):**
- **.NET 8 SDK or later** - Must be installed in each environment where tests or the server will run

**Optional:**
- Docker Desktop 4.42.0+ (for Docker deployment)
- **Pester 5.x** (for PowerShell script validation tests)
  ```powershell
  Install-Module -Name Pester -Force -SkipPublisherCheck -Scope CurrentUser
  ```
  **Note**: Windows includes Pester 3.4.0 by default. The SBOM validation test suite requires Pester 5.x for `BeforeAll`/`AfterAll` test lifecycle support.

**Important for WSL users**: .NET SDK must be installed **inside** the WSL distribution, not just on the Windows host. See [E2E-TESTING.md](E2E-TESTING.md#wsl-specific-requirements) for WSL installation instructions.

### Building the Project

```bash
# Clone the repository
cd RefactorCsharpMCP

# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run tests
dotnet test
```

### Running Cache Stability Tests

To verify that cache concurrency fixes remain stable over time, specialized stability test scripts are available:

**Linux/macOS:**
```bash
./scripts/test-cache-stability.sh [--iterations N]
```

**Windows (PowerShell):**
```powershell
.\scripts\test-cache-stability.ps1 [-Iterations N]
```

**Windows (WSL):**
```powershell
.\scripts\test-cache-stability-wsl.ps1 [-Iterations N]
```
⚠️ **Note**: WSL script requires .NET SDK installed in WSL.

To install .NET SDK in WSL (Ubuntu 24.04):
```bash
# In your WSL terminal
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
```

💡 **Performance**: WSL typically runs **~40% faster** than native Windows PowerShell (27s vs 45s per iteration in cache stability tests) due to Linux filesystem characteristics and lower I/O overhead. Your results may vary depending on workload. Recommended for performance-critical testing.

Default: 10 iterations

These scripts run cache-related tests multiple times and report:
- Pass/fail rate
- Average execution time
- Statistical metrics (min/max/std deviation)
- Exit with error if any run fails

**Automated CI/CD**: The GitHub Actions workflow `.github/workflows/cache-stability.yml` runs these tests automatically on every PR and weekly schedule.

### Running the MCP Server

```bash
# Run the server directly
cd src/RefactorCsharpMCP.Server
dotnet run

# Or from the solution root
dotnet run --project src/RefactorCsharpMCP.Server
```

The server will start and listen for MCP requests via stdio transport.

### Docker Deployment

RefactorCsharpMCP includes Docker support for easy deployment and distribution.

#### Building the Docker Image

```bash
# Build the Docker image
docker build -t refactor-csharp-mcp:latest .

# Or use Docker Compose
docker compose build
```

#### Running with Docker

```bash
# Run directly with Docker (for testing)
docker run --rm -i refactor-csharp-mcp:latest

# Or use Docker Compose
docker compose run --rm refactor-csharp-mcp
```

#### Docker Desktop MCP Toolkit Integration

RefactorCsharpMCP is fully integrated with Docker Desktop 4.25+ MCP Toolkit for centralized server management and discovery.

**Two Integration Options:**

##### Option 1: Docker MCP Gateway (Recommended)

The Docker MCP Gateway provides centralized management of all your MCP servers through Docker Desktop.

**Quick Setup:**

```bash
# 1. Build the image (if not already built)
docker build -t refactor-csharp-mcp:latest .

# 2. Register with Docker MCP Gateway
# Windows
pwsh ./scripts/register-mcp-gateway.ps1

# Linux/macOS
./scripts/register-mcp-gateway.sh

# 3. Verify registration
docker mcp catalog show local-dev
docker mcp server ls
```

**Configure your AI client** (Claude Desktop, VS Code, etc.):

```json
{
  "mcpServers": {
    "refactor-csharp-mcp": {
      "command": "docker",
      "args": ["mcp", "gateway", "run"]
    }
  }
}
```

**Gateway Management Commands:**

```bash
# Enable/disable the server
docker mcp server enable refactor-csharp-mcp
docker mcp server disable refactor-csharp-mcp

# View server details
docker mcp server inspect refactor-csharp-mcp

# List all enabled servers
docker mcp server ls

# View catalog
docker mcp catalog show local-dev
```

**Benefits:**
- ✅ Centralized management in Docker Desktop
- ✅ Discoverable in Docker Desktop UI
- ✅ Automatic resource limits (1 CPU, 2GB RAM)
- ✅ Version management via environment variables
- ✅ Standardized configuration across MCP servers

##### Option 2: Direct Docker Integration

For direct control without the gateway, use the traditional Docker approach:

```json
{
  "mcpServers": {
    "refactor-csharp-mcp": {
      "command": "docker",
      "args": ["run", "--rm", "-i", "refactor-csharp-mcp:latest"]
    }
  }
}
```

**Benefits:**
- ✅ No gateway dependency
- ✅ Direct container control
- ✅ Simpler configuration
- ✅ Lower overhead

**Note:** Both options use stdio transport - no port mapping is required.

For detailed Docker MCP Toolkit integration guide, see [docs/DOCKER-MCP-TOOLKIT.md](docs/DOCKER-MCP-TOOLKIT.md).

#### Production Deployment Considerations

For production use, consider these additional configurations for enhanced security and reliability:

**Security Hardening:**

The Docker image includes built-in security features:
- ✅ **Non-root user**: Container runs as `mcpuser` (UID 1000)
- ✅ **Security labels**: OCI image metadata and compliance labels
- ✅ **SHA256 pinning**: Base images pinned for reproducible builds
- ✅ **Health checks**: Automatic container health monitoring

Additional runtime security options:
```bash
# Run container with read-only filesystem
docker run --rm -i --read-only refactor-csharp-mcp:latest

# Limit container resources (Gateway enforces these by default)
docker run --rm -i --memory=512m --cpus=1 refactor-csharp-mcp:latest
```

**Monitoring and Health:**
```bash
# Check container health status
docker inspect --format='{{.State.Health.Status}}' <container-id>

# View container logs
docker logs <container-id>

# Monitor resource usage
docker stats <container-id>
```

**Image Security Scanning:**
```bash
# Scan for vulnerabilities with Docker Scout
docker scout cves refactor-csharp-mcp:latest

# Or use Trivy for security scanning
trivy image refactor-csharp-mcp:latest
```

#### Software Bill of Materials (SBOM)

RefactorCsharpMCP automatically generates Software Bill of Materials (SBOM) during Docker builds for supply chain security and compliance.

**SBOM Features:**
- **Automatic Generation**: SBOM created during every Docker build using BuildKit
- **Multi-Stage Scanning**: Captures both build-time (NuGet packages) and runtime dependencies
- **Dual Format Support**: SPDX (native) and CycloneDX (optional with Syft/Trivy)
- **Validation**: Automated checks for expected NuGet packages and license coverage

**Requirements:**
- Docker Desktop >= 4.24 or BuildKit >= 0.12
- Docker Buildx with `docker-container` driver

**Build with SBOM:**
```bash
# Using deployment script (recommended)
.\scripts\deploy-docker.ps1

# Manual build (exports SBOM to filesystem)
docker buildx build --sbom=true --output type=local,dest=./sbom-output .

# The deploy script performs dual-build:
# 1. Export SBOM to sbom.spdx.json
# 2. Load image to local Docker daemon
```

**SBOM Files Generated:**
- `sbom.spdx.json` - SPDX format SBOM (always generated)
- `sbom.cyclonedx.json` - CycloneDX format (if Syft installed)
- `sbom-packages-{version}-{timestamp}.csv` - Package summary

**Validate SBOM:**
```powershell
# Basic validation
.\scripts\validate-sbom.ps1

# Validate specific file
.\scripts\validate-sbom.ps1 -SbomPath "sbom.cyclonedx.json" -Format cyclonedx

# Verbose output with package details
.\scripts\validate-sbom.ps1 -Verbose
```

**Validation Checks:**
- ✅ Valid JSON structure
- ✅ Format compliance (SPDX 2.3+ or CycloneDX 1.4+)
- ✅ Minimum package count (default: 80 packages)
- ✅ Expected NuGet packages present (Microsoft.CodeAnalysis.CSharp, ModelContextProtocol, etc.)
- ✅ License coverage percentage
- ✅ Base image dependencies included

**Install Optional Tools (for CycloneDX):**
```bash
# Syft (Anchore SBOM generator)
choco install syft

# Trivy (Security scanner with SBOM generation)
choco install trivy
```

**Performance Impact:**
- First-time builds: 5-10 minutes (buildkit-syft-scanner download ~14 MB)
- Subsequent builds: +30-50% build time overhead
- Cached builds: Minimal impact due to BuildKit layer caching

**NTIA Compliance:**
The generated SBOM includes all components required for NTIA minimum elements:
- Component names and versions
- Dependency relationships
- Author/supplier information (where available)
- License information
- Timestamps

**Image Reproducibility:**
- Base images are pinned to SHA256 digests for reproducible builds
- Update digests quarterly or when security patches are released
- Use `docker buildx imagetools inspect mcr.microsoft.com/dotnet/runtime:8.0` to get latest digest

### Integration with AI Clients

RefactorCsharpMCP uses stdio transport for communication. Configure your AI client's MCP settings to connect to the server.

#### Claude Code Configuration (Direct .NET)

Add to your Claude Code configuration (typically in `.claude/mcp_servers.json`):

```json
{
  "mcpServers": {
    "refactor-csharp-mcp": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/RefactorCsharpMCP/src/RefactorCsharpMCP.Server"],
      "type": "stdio"
    }
  }
}
```

**Note:** Replace `/path/to/RefactorCsharpMCP` with your actual installation path.

#### Claude Code Configuration (Docker)

For Docker deployment:

```json
{
  "mcpServers": {
    "refactor-csharp-mcp-docker": {
      "command": "docker",
      "args": ["run", "--rm", "-i", "refactor-csharp-mcp:latest"],
      "type": "stdio"
    }
  }
}
```

#### VS Code / Cursor Configuration

Similar stdio-based configuration in your editor's MCP settings. Use either the dotnet or docker command depending on your deployment preference.

### Cache Management

RefactorCsharpMCP uses a three-tier caching strategy to optimize reference assembly resolution for cross-framework refactoring:

**Cache Location:**
```
%USERPROFILE%/.refactor-csharp-mcp/reference-assemblies/
```

**Cache Characteristics:**
- **Three-tier caching**: Memory cache → Disk cache → NuGet download
- **Thread-safe**: Concurrent access supported with proper locking
- **Automatic retry**: Transient file system errors handled with exponential backoff
- **Framework isolation**: Each framework has a dedicated cache directory
- **Size**: Approximately 50MB per framework (~550MB for all 11 supported frameworks)

**Supported Frameworks (11 total):**
- Modern .NET: net9.0, net8.0
- .NET Framework: net481, net48, net472, net471, net47, net462, net35
- .NET Standard: netstandard2.1, netstandard2.0

**Cache Operations:**

View cache statistics:
```bash
# Cache stats are logged during normal operation
# Check logs for cache hit/miss information
```

Clear cache manually:
```bash
# Delete the cache directory
rm -rf ~/.refactor-csharp-mcp/reference-assemblies/

# Or on Windows PowerShell:
Remove-Item -Recurse -Force "$env:USERPROFILE\.refactor-csharp-mcp\reference-assemblies"
```

**Cache Behavior:**
- First access per framework downloads from NuGet (~50MB, one-time)
- Subsequent access uses disk cache (~100-500ms)
- Memory cache provides sub-millisecond access for active frameworks
- Cache persists across server restarts
- No automatic eviction (see [TROUBLESHOOTING.md](TROUBLESHOOTING.md) for manual management)

**Performance Characteristics:**
- First load: ~2000ms (includes NuGet download and extraction)
- Disk cache hit: ~100-500ms (load from disk)
- Memory cache hit: <10ms (already in memory)

See [FUTURE-ROADMAP.md](docs/FUTURE-ROADMAP.md) for planned enhancements including automatic cache eviction policies.

### Available Refactorings

#### Extract Method
Extracts a block of code into a new private method.

```bash
# Parameters:
- sourceCode: Complete C# source code
- startLine: Starting line number (1-based)
- endLine: Ending line number (1-based)
- newMethodName: Name for the extracted method
```

#### Constructor Injection
Converts method parameters to constructor-injected fields or properties.

```bash
# Parameters:
- sourceCode: Complete C# source code
- className: Name of the class containing the method
- methodName: Name of the method with parameters to inject
- parameterNames: Comma or semicolon-separated parameter names
- useProperties: Use properties instead of fields (default: false)
```

#### Inline Variable
Replaces all uses of a local variable with its initialization expression, then removes the variable declaration. Helps simplify code by eliminating unnecessary intermediate variables. Maps to Roslyn diagnostics IDE0059 (unnecessary value assignment) and IDE0058 (expression value never used).

```bash
# Parameters:
- sourceCode: Complete C# source code
- lineNumber: Line number where variable is declared (1-based)
- columnNumber: Column number within the line (1-based)
- targetFramework: Target .NET framework (default: "net8.0")

# Features:
- Automatic operator precedence handling with parentheses
- Safety checks: rejects uninitialized variables, multiple assignments, increment/decrement operators
- Lambda capture detection (not supported in V1)
- Framework-aware validation
```

#### Extract Class
Extracts fields and methods into a new class with automatic reference updating within the same class.

```bash
# Parameters:
- sourceCode: Complete C# source code
- className: Name of the source class
- newClassName: Name for the new extracted class
- fieldNames: Comma or semicolon-separated field names to extract
- methodNames: Comma or semicolon-separated method names to extract (optional)

# Features:
- Automatic reference updating for same-class references (field accesses, method calls)
- Semantic analysis prevents false positives (local variables, parameters remain unchanged)
- Partial class support (references in all parts are updated)
- Composition pattern with readonly field and instantiation
- External reference warnings for manual updates
- Handles qualified member access (this._field)
```

**Automatic transformations:**
- `_city` → `_address._city`
- `this._city` → `_address._city`
- `GetAddress()` → `_address.GetAddress()`

**Preserved correctly:**
- Local variables with same name
- Method parameters with same name
- Fields in unrelated classes

#### Inline Method (Part 1)
Inlines a method by replacing its single invocation with the method's body, then removes the method declaration. Part 1 supports void methods with simple parameters (primitives, string) and single caller only.

```bash
# MCP Tool: inline_method
# Parameters:
- sourceCode: Complete C# source code
- lineNumber: Line number where method is declared (1-based)
- columnNumber: Column number within the line (1-based)
- targetFramework: Target .NET framework (default: "net8.0")

# Features:
- Void method inlining with single caller
- Parameter substitution for primitives and strings
- Comment preservation (invocation site and method body)
- Framework-aware validation
- Safety checks: rejects virtual/abstract/recursive methods, multiple callers, ref/out parameters

# Part 1 Limitations:
- Void methods only (no return values)
- Single caller required
- Simple parameters only (primitives, string)
- No virtual/abstract/override methods
- No recursive methods
```

### Diagnostic Integration (V1.5)

RefactorCsharpMCP provides powerful diagnostic capabilities that enable AI agents to detect code issues and automatically fix them using the **analyze → suggest → fix** workflow.

#### Analyze Code
Analyzes C# code for compiler warnings, style violations, and code quality issues using Roslyn's built-in analyzers (500+ diagnostic rules).

```bash
# MCP Tool: analyze_code
# Parameters:
- sourceCode: Complete C# source code
- targetFramework: Target .NET framework (e.g., "net8.0", "net48")
- minSeverity: Minimum severity level - "Error", "Warning", "Info", "Hidden" (optional, default: "Warning")

# Returns:
- diagnostics: List of issues with locations, severity, and applicable refactorings
- summary: Total count by severity level (errors, warnings, info)
```

**Example Workflow:**
```json
{
  "tool": "analyze_code",
  "parameters": {
    "sourceCode": "using System.Linq;\npublic class Test { }",
    "targetFramework": "net8.0"
  },
  "result": {
    "success": true,
    "diagnostics": [
      {
        "id": "IDE0005",
        "severity": "Info",
        "message": "Using directive is unnecessary",
        "location": { "line": 1, "column": 1 },
        "category": "Style",
        "applicableRefactorings": ["remove_unused_usings"]
      }
    ],
    "summary": {
      "totalDiagnostics": 1,
      "errorCount": 0,
      "warningCount": 0,
      "infoCount": 1
    }
  }
}
```

#### Fix Diagnostic
Automatically fixes a specific Roslyn diagnostic by dispatching to the appropriate refactoring tool.

```bash
# MCP Tool: fix_diagnostic
# Parameters:
- sourceCode: Complete C# source code containing the diagnostic
- diagnosticId: Roslyn diagnostic ID (e.g., "IDE0005", "IDE0044", "CS8019")
- line: Line number where diagnostic occurs (1-based)
- column: Column number where diagnostic occurs (1-based)
- targetFramework: Target .NET framework

# Supported Diagnostics:
- IDE0005, CS8019: Unused using directives → remove_unused_usings
- IDE0044: Field can be readonly → make_field_readonly
- IDE0059, IDE0058: Unnecessary value assignment → inline_variable
```

**Example Workflow:**
```json
{
  "tool": "fix_diagnostic",
  "parameters": {
    "sourceCode": "using System.Linq;\npublic class Test { }",
    "diagnosticId": "IDE0005",
    "line": 1,
    "column": 1,
    "targetFramework": "net8.0"
  },
  "result": {
    "success": true,
    "message": "Successfully removed 1 unused using directive(s)",
    "refactoredCode": "public class Test { }",
    "diagnosticId": "IDE0005",
    "appliedRefactoring": "remove_unused_usings"
  }
}
```

**Benefits:**
- **Framework-Aware Analysis**: Respects target framework capabilities and language versions
- **Automatic Tool Routing**: Maps diagnostic IDs to appropriate refactorings
- **Comprehensive Coverage**: Leverages Roslyn's 500+ built-in diagnostic rules
- **AI-Friendly**: Enables proactive code quality assistance for AI agents

## Development Roadmap

RefactorCsharpMCP is being developed in 4 phases:

### Phase 1: Foundation (Weeks 1-2) - ✅ Complete
- ✅ Basic MCP server with stdio transport
- ✅ Extract Method refactoring with Roslyn semantic analysis
- ✅ Constructor Injection refactoring with proper merging
- ✅ Comprehensive testing (26 tests, zero warnings)
- ✅ Full documentation (README, EXAMPLES, TROUBLESHOOTING, E2E-TESTING)

### Phase 2: Enhanced Refactorings (Weeks 3-4) - ✅ Complete
- ✅ Make Field Readonly refactoring
- ✅ Safe Delete with dependency analysis
- ✅ Extract Class refactoring
- ✅ Code analysis utilities (dependency tracking, scope analysis)
- ✅ DevTools integration testing (BackupTool, passgen)

### Phase 3: Docker Deployment (Week 5) - ✅ Complete
- ✅ Multi-stage Dockerfile with stdio transport
- ✅ Docker Desktop MCP Toolkit integration
- ✅ Container optimization (.dockerignore, multi-stage build)
- ✅ Docker Compose configuration
- ✅ Comprehensive Docker documentation

### Phase 4: Production Readiness (Week 6) - 🚧 In Progress
- ✅ Performance optimization (compiled regex in McpToolConstants, ReDoS protection)
- ✅ Enhanced error handling (safe error categorization)
- ✅ Comprehensive examples (all 5 refactorings documented)
- 🚧 Final documentation and API reference
- 🚧 Docker MCP Catalog preparation
- ⏳ Final testing and release

## Planned Refactorings

- Extract Method
- Constructor Injection
- Make Field Readonly
- Safe Delete
- Extract Class
- Move Static/Instance Methods
- Use Interface
- And more...

## Documentation

- [Project Plan](docs/project-plan.md) - Comprehensive development and architecture plan
- [Performance Benchmarks](docs/performance-benchmarks.md) - Baseline performance metrics and optimization guide
- Examples (coming soon)
- API Documentation (coming soon)

## Performance

RefactorCsharpMCP includes comprehensive performance benchmarks using BenchmarkDotNet to track and optimize refactoring operation speed.

### Performance Targets

| File Size | Target Mean Time | Typical Use Case |
|-----------|------------------|------------------|
| Small (~50 lines) | < 100ms | Quick refactorings in small classes |
| Medium (~500 lines) | < 500ms | Real-world production code |
| Large (~5000 lines) | < 2000ms | Legacy codebases and large files |

### Running Benchmarks

```bash
cd src/RefactorCsharpMCP.Benchmarks

# Run all benchmarks
dotnet run -c Release

# Run specific refactoring benchmarks
dotnet run -c Release --filter *ExtractMethod*
```

Benchmark results are saved to `BenchmarkDotNet.Artifacts/results/` with HTML, Markdown, and CSV reports.

For detailed performance analysis, baseline metrics, and optimization notes, see [Performance Benchmarks](docs/performance-benchmarks.md).

## Known Limitations

### IDE Analyzer Limitations (Issue #72)

RefactorCsharpMCP uses Roslyn's compiler APIs for code analysis, which have different capabilities compared to full IDE workspace APIs:

**Unused Using Detection:**
- **Issue**: CS8019 and IDE0005 (unused using directives) require full IDE analyzer infrastructure
- **Impact**: `remove_unused_usings` and `analyze_code` may not detect all unused usings in all scenarios
- **Workaround**: Modern IDEs (Visual Studio, VS Code with C# extension) provide complete detection
- **Status**: 12 tests skipped due to this limitation (documented in test suite)

**Why This Occurs:**
- IDE analyzers require workspace context (project files, references, solution structure)
- RefactorCsharpMCP operates on individual source code strings without workspace context
- This is an architectural limitation of the Roslyn compiler APIs vs. workspace APIs

**Affected Refactorings:**
- `remove_unused_usings` - May miss some unused directives
- `analyze_code` - May not report IDE0005/CS8019 diagnostics
- `fix_diagnostic` - Cannot fix IDE0005/CS8019 if not detected

### .NET Framework Reference Assembly Limitations (Issue #75)

Cross-framework refactoring relies on NuGet-distributed reference assemblies, which may not be available in all environments:

**Framework Support Status:**
- **Fully Supported**: net8.0, net9.0, netstandard2.0, netstandard2.1
- **Limited Support**: net48, net481, net472, net471, net47, net462, net35

**net48 Specific Issues:**
- Reference assemblies may not be available on all systems
- Refactorings may fail with "Code references types or members not available" errors
- Modern frameworks (net8.0, net9.0) are recommended for best experience

**Workarounds:**
1. **Prefer Modern Frameworks**: Use net8.0 or net9.0 when possible
2. **Manual Installation**: Install Microsoft.NETFramework.ReferenceAssemblies NuGet package
3. **Cache Pre-warming**: Run refactorings on modern frameworks first to warm the cache

**Test Suite Handling:**
- Framework matrix tests use conditional assertions for net48
- 42 framework compatibility tests verify behavior across all frameworks
- Tests document expected failures for net48 environments

### InlineMethod Part 1 Limitations

The current `inline_method` implementation (Part 1) has several intentional limitations:

**Supported:**
- Void methods only
- Single caller required
- Simple parameters (primitives, string)
- Comment preservation

**Not Supported (Part 2 Planned):**
- Methods with return values
- Multiple call sites
- Complex parameters (ref, out, params)
- Virtual/abstract/override methods
- Recursive methods
- Lambda captures

**Status**: Part 2 enhancements planned for future release

### Framework-Specific Language Version Restrictions

Refactorings respect target framework language version constraints:

**Language Version Mappings:**
- net9.0 → C# 13
- net8.0 → C# 12
- net48, netstandard2.0 → C# 7.3

**Impact:**
- Modern C# syntax (e.g., collection expressions `[1, 2, 3]`) will fail on net48
- Framework validation prevents incompatible syntax from being used
- Error messages indicate language version mismatch

**Example:**
```csharp
// This works on net8.0
int[] numbers = [1, 2, 3];

// But fails on net48 with:
// "C# 12 syntax should not work on net48"
```

### General Limitations

**Cross-File Refactoring:**
- RefactorCsharpMCP operates on single source code strings
- Cannot refactor across multiple files or projects
- Use IDE refactoring tools for multi-file scenarios

**Workspace Context:**
- No access to project files, solution structure, or external references
- Limited to analysis within provided source code
- Cannot resolve external type references

**Performance:**
- First framework load requires NuGet download (~2000ms, ~50MB per framework)
- Subsequent loads use disk cache (~100-500ms)
- Memory cache provides sub-millisecond access for active frameworks

For detailed troubleshooting guidance, see [TROUBLESHOOTING.md](TROUBLESHOOTING.md).

## Contributing

RefactorCsharpMCP is part of the DevTools repository. See the project plan for development guidelines and architecture details.

## License

Part of the DevTools repository by Seth.

## Related Projects

- [dave-hillier/refactor-csharp-mcp](https://github.com/dave-hillier/refactor-csharp-mcp) - Inspiration for this project
- [Model Context Protocol](https://modelcontextprotocol.io/) - MCP specification

---

**Status**: Phase 4 - Production Readiness (🚧 In Progress)
**Version**: 0.4.0-rc
**Tests**: 107 passing (94 unit + 11 integration + 2 lambda tests), 0 warnings
**Docker**: Multi-stage build, SHA256 pinned, HEALTHCHECK enabled
**Performance**: Compiled regex validation, ReDoS protection
**Examples**: [Comprehensive examples](examples/README.md) for all 5 refactorings
