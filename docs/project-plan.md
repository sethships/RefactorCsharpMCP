# RefactorCsharpMCP - C# Refactoring MCP Server

## Project Overview

RefactorCsharpMCP is a Model Context Protocol (MCP) server that provides Roslyn-based refactoring capabilities for C# code. This tool will be integrated into the DevTools repository to assist with code analysis and automated refactoring across all DevTools projects.

### Goals

1. **Enable AI-assisted refactoring** - Provide Claude Code with powerful C# refactoring capabilities
2. **Support legacy and modern .NET** - Work with both .NET Framework 4.5.2 (BackupTool, LineCounter, Logging) and .NET 8 (passgen)
3. **Docker-based deployment** - Package as a containerized MCP server for easy distribution and consistent runtime
4. **Docker Desktop MCP Toolkit compatibility** - Full integration with Docker Desktop's MCP Toolkit for one-click deployment and connection to AI clients
5. **Integration with DevTools** - Test and validate refactorings against existing DevTools projects

### Inspiration

This project is inspired by [dave-hillier/refactor-csharp-mcp](https://github.com/dave-hillier/refactor-csharp-mcp), which demonstrates Roslyn-based refactoring exposed through the Model Context Protocol.

## Architecture

### Technology Stack

- **Framework**: .NET 8 (for the MCP server itself)
- **Target Analysis**: Support for .NET Framework 4.5.2+ and .NET 8
- **Core Libraries**:
  - `ModelContextProtocol` - MCP SDK for C#
  - `Microsoft.CodeAnalysis.CSharp` (Roslyn) - C# syntax analysis and transformation
  - `Microsoft.CodeAnalysis.CSharp.Workspaces` - Workspace API for multi-file refactorings
  - `Microsoft.Extensions.Hosting` - ASP.NET Core hosting for the server
- **Communication**: stdio transport (standard input/output for Claude Code integration)
- **Containerization**: Docker for deployment

### Core Components

```
RefactorCsharpMCP/
├── docs/
│   └── project-plan.md              # This document
├── src/
│   ├── RefactorCsharpMCP.Server/          # MCP server console application
│   │   ├── Program.cs               # Entry point and MCP server setup
│   │   ├── Tools/                   # MCP tool implementations
│   │   │   ├── ExtractMethodTool.cs
│   │   │   ├── ConstructorInjectionTool.cs
│   │   │   └── ...
│   │   └── RefactorCsharpMCP.Server.csproj
│   │
│   ├── RefactorCsharpMCP.Core/            # Roslyn refactoring logic
│   │   ├── Refactorings/            # Individual refactoring implementations
│   │   │   ├── ExtractMethod.cs
│   │   │   ├── ConstructorInjection.cs
│   │   │   └── ...
│   │   ├── Analysis/                # Code analysis utilities
│   │   │   ├── SyntaxAnalyzer.cs
│   │   │   ├── DependencyAnalyzer.cs
│   │   │   └── ...
│   │   └── RefactorCsharpMCP.Core.csproj
│   │
│   └── RefactorCsharpMCP.Tests/           # Unit and integration tests
│       ├── Refactorings/            # Tests for each refactoring
│       ├── Integration/             # End-to-end MCP tests
│       └── RefactorCsharpMCP.Tests.csproj
│
├── examples/                         # Example code for testing refactorings
├── Dockerfile                        # Multi-stage Docker build
├── .dockerignore
├── RefactorCsharpMCP.sln                  # Solution file
└── README.md                        # Usage and setup instructions
```

### MCP Server Design

The MCP server will:

1. **Expose Refactoring Tools** - Each refactoring operation is exposed as an MCP tool
2. **Accept Code Input** - Tools receive C# source code as strings or file paths
3. **Return Refactored Code** - Output includes the transformed code and a description of changes
4. **Provide Diagnostics** - Report analysis results, potential issues, and suggestions

#### Example Tool Definition

```csharp
[McpServerToolType]
public class RefactoringTools
{
    [McpServerTool]
    [Description("Extracts selected code into a new method")]
    public async Task<RefactoringResult> ExtractMethod(
        string sourceCode,
        int startLine,
        int endLine,
        string newMethodName)
    {
        // Implementation using RefactorCsharpMCP.Core
    }
}
```

## Proposed Features

### Phase 1: Core Refactorings (Initial Implementation)

1. **Extract Method**
   - Select code block and extract to a new method
   - Automatically detect and pass required parameters
   - Handle return values and variable scoping

2. **Constructor Injection**
   - Convert method parameters to constructor-injected fields
   - Support both field and property injection
   - Add constructor if none exists

3. **Make Field Readonly**
   - Move field initialization to constructors
   - Mark fields as readonly where possible

### Phase 2: Advanced Refactorings

4. **Move Static Method**
   - Relocate static methods between classes
   - Optionally create wrapper methods

5. **Move Instance Method**
   - Move instance methods to another class
   - Handle delegation and dependency injection

6. **Extract Class**
   - Create new class from selected members
   - Generate composition relationships

7. **Use Interface**
   - Replace concrete types with interface types
   - Analyze which interface best fits the usage

8. **Safe Delete**
   - Remove unused fields, properties, methods
   - Perform dependency analysis before deletion

### Phase 3: Analysis and Metrics

9. **Code Metrics**
   - Cyclomatic complexity
   - Lines of code per method/class
   - Coupling and cohesion metrics

10. **Refactoring Suggestions**
    - Analyze code and suggest applicable refactorings
    - Identify code smells (long methods, god classes, etc.)

### Phase 4: Advanced Features

11. **Batch Refactoring**
    - Apply same refactoring to multiple locations
    - Pattern-based transformations

12. **Undo/Preview**
    - Show diff before applying changes
    - Support rollback operations

## Development Phases

### Phase 1: Foundation (Weeks 1-2)

**Goals:**
- Set up project structure
- Implement basic MCP server
- Create 2-3 simple refactorings

**Tasks:**
1. Create solution and project files
2. Add NuGet dependencies (ModelContextProtocol, Roslyn)
3. Implement MCP server with stdio transport
4. Create ExtractMethod refactoring
5. Create ConstructorInjection refactoring
6. Write unit tests for refactorings
7. Create README with usage examples

**Success Criteria:**
- MCP server runs and responds to tool calls
- Extract Method works on simple code samples
- Constructor Injection creates fields and constructor
- Tests pass with >80% coverage

### Phase 2: Enhanced Refactorings (Weeks 3-4)

**Goals:**
- Add more complex refactorings
- Improve analysis capabilities
- Test against DevTools projects

**Tasks:**
1. Implement MakeFieldReadonly
2. Implement SafeDelete with dependency analysis
3. Implement ExtractClass
4. Add code analysis utilities (dependency tracking, scope analysis)
5. Create integration tests using DevTools code samples
6. Test refactorings on BackupTool and passgen

**Success Criteria:**
- 5-6 refactorings working reliably
- Can analyze and refactor real DevTools code
- Integration tests cover common scenarios

### Phase 3: Docker Deployment (Week 5)

**Goals:**
- Containerize the MCP server
- Enable easy deployment and distribution
- Ensure Docker Desktop MCP Toolkit compatibility

**Tasks:**
1. Create multi-stage Dockerfile with stdio transport support
2. Optimize container size
3. Set up Docker build and test workflow
4. Test with Docker Desktop MCP Toolkit (Docker Desktop 4.42.0+)
5. Verify stdio transport works correctly through Docker gateway
6. Document Docker usage and MCP Toolkit setup in README
7. Test with multiple AI clients (Claude Desktop, VS Code, Cursor)

**Success Criteria:**
- Docker image builds successfully
- Container runs MCP server with stdio transport correctly
- Successfully connects via Docker Desktop MCP Toolkit
- Works with Claude Code and other MCP clients through the toolkit
- Documentation explains both standalone Docker and MCP Toolkit setup
- Ready for Docker MCP Catalog submission

### Phase 4: Production Readiness (Week 6)

**Goals:**
- Polish and optimize
- Comprehensive documentation
- Production deployment

**Tasks:**
1. Performance optimization
2. Error handling and logging improvements
3. Create comprehensive examples
4. Write detailed documentation
5. Set up CI/CD pipeline (optional)

**Success Criteria:**
- Production-ready error handling
- Complete documentation
- Examples demonstrate all features
- Ready for real-world usage

## Integration with DevTools

### Test Projects

RefactorCsharpMCP will be validated against all existing DevTools projects:

1. **BackupTool** (.NET Framework 4.5.2)
   - Test refactorings on Entity Framework models
   - Extract methods from complex LINQ queries
   - Constructor injection for service dependencies

2. **passgen** (.NET 8)
   - Refactor command-line parsing logic
   - Extract validation methods
   - Apply modern C# patterns

3. **LineCounter** (.NET Framework 4.5.2)
   - Simplify file processing logic
   - Extract file analysis methods

4. **Logging** (.NET Framework 4.5.2)
   - Refactor logging infrastructure
   - Apply interface-based design patterns

### Compatibility Strategy

Since DevTools contains both .NET Framework 4.5.2 and .NET 8 projects:

- **Server Runtime**: Use .NET 8 for the MCP server itself
- **Analysis Support**: Roslyn can analyze any C# version
- **Output Compatibility**: Preserve target framework syntax (e.g., don't suggest C# 12 features for .NET Framework 4.5.2 code)
- **Framework Detection**: Analyze .csproj files to determine target framework and adjust suggestions accordingly

## MCP Protocol Integration

### Tool Registration

Each refactoring is exposed as an MCP tool with:
- **Name**: Kebab-case identifier (e.g., `extract-method`)
- **Description**: Clear explanation of what the refactoring does
- **Parameters**: JSON schema for input parameters
- **Return Type**: Structured result with refactored code and metadata

### Example Tool Call

```json
{
  "method": "tools/call",
  "params": {
    "name": "extract-method",
    "arguments": {
      "sourceCode": "public class Example { void Foo() { var x = 1; var y = 2; var z = x + y; } }",
      "startLine": 3,
      "endLine": 3,
      "newMethodName": "CalculateSum"
    }
  }
}
```

### Example Response

```json
{
  "content": [
    {
      "type": "text",
      "text": "Successfully extracted method 'CalculateSum'"
    },
    {
      "type": "resource",
      "resource": {
        "uri": "refactored://Example.cs",
        "mimeType": "text/x-csharp",
        "text": "public class Example { void Foo() { var z = CalculateSum(); } int CalculateSum() { var x = 1; var y = 2; return x + y; } }"
      }
    }
  ]
}
```

### Resources

The server will expose:
- `metrics://` - Code quality metrics for files
- `summary://` - Refactoring suggestions and code smell detection
- `refactored://` - Transformed code after refactoring

## Docker Deployment

### Docker Desktop MCP Toolkit Integration

RefactorCsharpMCP is designed for **full compatibility with Docker Desktop's MCP Toolkit**, enabling:

- **One-click installation** from Docker Desktop's MCP Catalog
- **Secure container isolation** for the MCP server
- **Easy configuration** of environment variables and settings
- **Cross-client support** for Claude Desktop, Cursor, VS Code, Continue.dev, and other AI clients
- **stdio transport** as required by the MCP Toolkit specification

#### Requirements

Users need Docker Desktop 4.42.0+ (4.46.0+ recommended) to use the MCP Toolkit.

#### MCP Toolkit Configuration

Once published, users can configure RefactorCsharpMCP in their AI clients (e.g., VS Code, Claude Desktop) like this:

```json
{
  "mcp": {
    "servers": {
      "refactor-csharp-mcp": {
        "command": "docker",
        "args": [
          "mcp",
          "gateway",
          "run",
          "sethb75/refactor-csharp-mcp:latest"
        ],
        "type": "stdio"
      }
    }
  }
}
```

The Docker MCP Toolkit acts as a gateway, running the containerized MCP server and connecting it to AI clients via stdio transport.

### Dockerfile Strategy

Use multi-stage build optimized for MCP Toolkit compatibility:

```dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app

# Stage 2: Runtime
# IMPORTANT: Use runtime image, not aspnet, since this is a console app with stdio transport
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .

# stdio transport requires proper signal handling
ENTRYPOINT ["dotnet", "RefactorCsharpMCP.Server.dll"]
```

### Production Deployment Considerations

**Recommended: Use minimal Linux container for production**

Based on performance testing, .NET runs ~40% faster on Linux compared to Windows:
- **Windows**: ~45 seconds per test iteration
- **Linux**: ~27 seconds per test iteration
- **Performance gain**: 40% faster execution

**Alpine Linux Base Image (Recommended for Production)**:

```dockerfile
# Production-optimized Dockerfile with Alpine
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app

# Minimal runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "RefactorCsharpMCP.Server.dll"]
```

**Benefits**:
- **Smaller image size**: ~100MB vs ~200MB (standard Debian-based)
- **Faster startup**: Reduced layers and dependencies
- **Better performance**: 40% faster execution on Linux runtime
- **Security**: Smaller attack surface with minimal dependencies
- **Cost**: Reduced storage and bandwidth costs

**Alternative**: Standard Debian-based `mcr.microsoft.com/dotnet/runtime:8.0` for broader compatibility if Alpine compatibility issues arise.

### Docker Compose (Optional)

For local development and testing:

```yaml
version: '3.8'
services:
  refactor-csharp-mcp:
    build: .
    container_name: refactor-csharp-mcp-server
    stdin_open: true    # Required for stdio transport
    tty: true          # Required for stdio transport
```

### Docker Hub Distribution

Publishing strategy:
- `sethb75/refactor-csharp-mcp:latest` - Latest stable release
- `sethb75/refactor-csharp-mcp:v1.0.0` - Semantic versioned releases
- **Docker MCP Catalog submission** - Submit to Docker's official MCP Catalog for discoverability alongside 200+ other MCP servers

## Testing Strategy

### Unit Tests

- Test each refactoring in isolation
- Use Roslyn's SyntaxTree comparison for validation
- Cover edge cases (empty code, invalid syntax, etc.)

### Integration Tests

- Test MCP server tool calls end-to-end
- Validate JSON serialization/deserialization
- Test with real DevTools code samples

### Manual Testing

- Run against each DevTools project
- Test with Claude Code integration
- Validate Docker container behavior

### Test Coverage Goals

- Core refactoring logic: >90% coverage
- MCP tools: >80% coverage
- Overall project: >85% coverage

## Documentation

### README.md

- Quick start guide
- Installation instructions (dotnet run, Docker)
- List of available refactorings
- Usage examples
- Claude Code integration setup

### API Documentation

- XML documentation comments for all public APIs
- Generate API docs using DocFX or similar

### Examples

Create `EXAMPLES.md` with:
- Example code before/after for each refactoring
- Common usage patterns
- Integration examples with Claude Code

## Success Metrics

### Functionality
- ✅ All Phase 1 refactorings working correctly
- ✅ Tests passing with >80% coverage
- ✅ Can refactor real DevTools code without errors

### Performance
- ✅ Refactoring operations complete in <2 seconds for typical files
- ✅ Docker container starts in <5 seconds
- ✅ Low memory footprint (<100MB baseline)

### Usability
- ✅ Clear documentation
- ✅ Easy Docker setup
- ✅ Claude Code integration works seamlessly
- ✅ Helpful error messages

## Future Enhancements

### Beyond Initial Release

1. **Docker MCP Catalog Listing** - Publish to Docker's official MCP Catalog for broader discoverability
2. **VS Code Extension** - Direct integration into VS Code
3. **Web UI** - Browser-based refactoring interface
4. **AI-Powered Suggestions** - Use LLM to suggest refactorings
5. **Cross-Language Support** - Extend to F#, VB.NET
6. **Team Collaboration** - Share refactoring patterns and rules
7. **Performance Optimization** - Incremental analysis for large codebases
8. **Custom Refactorings** - Allow users to define refactoring patterns

### Community Contributions

- Open source the project on GitHub
- Submit to Docker MCP Catalog for public distribution
- Accept community-contributed refactorings
- Build ecosystem of refactoring patterns

## Risk Mitigation

### Technical Risks

1. **Roslyn Complexity**
   - *Risk*: Roslyn API is complex and poorly documented
   - *Mitigation*: Start with simple refactorings, reference existing tools, use Roslyn source browser

2. **Framework Compatibility**
   - *Risk*: Different .NET versions may have incompatible syntax
   - *Mitigation*: Detect target framework, test against multiple versions

3. **Performance**
   - *Risk*: Large files may cause slow refactorings
   - *Mitigation*: Implement timeouts, optimize analysis, consider async operations

### Process Risks

1. **Scope Creep**
   - *Risk*: Too many features delay initial release
   - *Mitigation*: Strict phase planning, MVP focus for Phase 1

2. **Testing Overhead**
   - *Risk*: Testing all edge cases takes excessive time
   - *Mitigation*: Prioritize common scenarios, add tests incrementally

## Conclusion

RefactorCsharpMCP will provide powerful, AI-assisted C# refactoring capabilities to the DevTools repository and beyond. By leveraging Roslyn, MCP, and Docker, we'll create a flexible, extensible tool that integrates seamlessly with Claude Code and can evolve to support the broader .NET community.

### Next Steps

1. Review and approve this project plan
2. Create initial project structure (solution, projects)
3. Begin Phase 1 implementation
4. Iterate based on learnings from DevTools integration

---

**Document Version**: 1.0
**Created**: 2025-10-04
**Author**: Seth (with Claude Code assistance)
**Status**: Draft - Pending Review
