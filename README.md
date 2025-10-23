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

- .NET 8 SDK or later
- Docker Desktop 4.42.0+ (for Docker deployment)

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

💡 **Performance**: WSL typically runs **~40% faster** than native Windows PowerShell (27s vs 45s per iteration) due to Linux's optimized .NET runtime and lower I/O overhead. Recommended for performance-critical testing.

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

RefactorCsharpMCP is compatible with Docker Desktop 4.42.0+ MCP Toolkit for one-click deployment.

**Configuration for Docker Desktop:**

Add to your MCP client configuration (Claude Desktop, VS Code, etc.):

```json
{
  "mcpServers": {
    "refactor-csharp-mcp": {
      "command": "docker",
      "args": ["run", "--rm", "-i", "refactor-csharp-mcp:latest"],
      "type": "stdio"
    }
  }
}
```

**Note:** The Docker container uses stdio transport - no port mapping is required.

#### Production Deployment Considerations

For production use, consider these additional configurations for enhanced security and reliability:

**Security Hardening:**
```bash
# Run container with read-only filesystem
docker run --rm -i --read-only refactor-csharp-mcp:latest

# Limit container resources to prevent DoS
docker run --rm -i --memory=512m --cpus=1 refactor-csharp-mcp:latest

# Run as non-root user (add USER directive to Dockerfile)
docker run --rm -i --user 1000:1000 refactor-csharp-mcp:latest
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
- Examples (coming soon)
- API Documentation (coming soon)

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
