# MCP Tool Invocation Guide

## Overview

This guide explains how to invoke RefactorCsharpMCP tools from Claude Code and other MCP clients, addressing the challenges of large payload handling and tool registration.

## Architecture

### The Challenge

MCP tools running in Docker are **not directly registered** in Claude Code's primary tool namespace. Instead, they're accessible through the Docker MCP gateway with the `mcp__MCP_DOCKER__` prefix.

```
┌─────────────────┐
│  Claude Code    │
│                 │
│  Direct Tools:  │
│  - Bash         │
│  - Read         │
│  - Write        │
└────────┬────────┘
         │
         │ Proxy via gateway
         │
┌────────▼────────────────┐
│ Docker MCP Toolkit      │
│                         │
│ Bridged Tools:          │
│ - mcp__MCP_DOCKER__*    │
└────────┬────────────────┘
         │
         │ stdio transport
         │
┌────────▼────────────────┐
│ RefactorCsharpMCP       │
│                         │
│ Tools:                  │
│ - extract_method        │
│ - constructor_injection │
│ - make_field_readonly   │
│ - (8 more...)           │
└─────────────────────────┘
```

### Why Tools Aren't Directly Invocable

1. **Namespace Isolation**: Docker MCP creates a separate namespace to avoid conflicts
2. **Security Boundary**: Tools run in containers with controlled resource access
3. **Transport Abstraction**: The gateway handles stdio/SSE/HTTP transport details

## Solution Approaches

### Approach 1: Use `mcp-exec` (Recommended for Automation)

The Docker MCP toolkit provides `mcp-exec` for programmatic tool invocation:

```bash
# Via Claude Code tool
mcp__MCP_DOCKER__mcp-exec(
  name: "extract_method",
  arguments: {
    "sourceCode": "...",
    "startLine": 117,
    "endLine": 158,
    "newMethodName": "ValidateMethodParameters",
    "targetFramework": "net8.0"
  }
)
```

**Pros**:
- Works from Claude Code sessions
- Handles tool routing automatically
- Supports all argument types

**Cons**:
- Requires JSON escaping for large payloads
- Limited to ~32KB arguments on Windows command line
- Verbose for repeated operations

### Approach 2: PowerShell Orchestration (Recommended for Large Files)

Use the provided PowerShell scripts to handle large payloads and JSON escaping:

```powershell
# Production-grade orchestrator
.\scripts\Invoke-McpRefactoring.ps1 `
    -ToolName extract_method `
    -SourceFile .\src\Core\SyntaxValidator.cs `
    -ToolArguments @{
        startLine = 117
        endLine = 158
        newMethodName = 'ValidateMethodParameters'
    } `
    -TargetFramework net8.0
```

**Pros**:
- Handles arbitrarily large source files
- Automatic JSON escaping and encoding
- Built-in error handling and logging
- File backup and diff statistics
- Works offline (no Claude Code session needed)

**Cons**:
- Requires PowerShell environment
- Extra layer of abstraction

### Approach 3: Direct Docker MCP CLI

Use the Docker MCP CLI directly with stdin for large payloads:

```powershell
# Create arguments JSON
$args = @{
    sourceCode = Get-Content -Path source.cs -Raw
    startLine = 117
    endLine = 158
    newMethodName = 'ValidateMethodParameters'
    targetFramework = 'net8.0'
} | ConvertTo-Json -Compress

# Invoke via stdin (no argument length limits)
$args | docker mcp tools call extract_method
```

**Pros**:
- Direct invocation without wrappers
- Stdin avoids argument length limits
- Simple and transparent

**Cons**:
- Manual JSON construction
- No error handling or validation
- Requires Docker MCP CLI availability

## Recommended Workflow

### For Single Refactorings

Use the production orchestrator script:

```powershell
# Extract method
.\scripts\Invoke-McpRefactoring.ps1 `
    -ToolName extract_method `
    -SourceFile .\src\MyClass.cs `
    -ToolArguments @{
        startLine = 50
        endLine = 75
        newMethodName = 'ExtractedMethod'
    }

# Remove unused usings
.\scripts\Invoke-McpRefactoring.ps1 `
    -ToolName remove_unused_usings `
    -SourceFile .\src\MyClass.cs `
    -TargetFramework net8.0
```

### For Batch Refactorings

Create a batch script:

```powershell
# batch-refactor.ps1
$refactorings = @(
    @{
        Tool = 'extract_method'
        Source = '.\src\SyntaxValidator.cs'
        Args = @{ startLine = 117; endLine = 158; newMethodName = 'ValidateMethodParameters' }
    },
    @{
        Tool = 'extract_method'
        Source = '.\src\SyntaxValidator.cs.refactored.cs'
        Args = @{ startLine = 200; endLine = 245; newMethodName = 'ValidateClassDeclaration' }
    },
    @{
        Tool = 'remove_unused_usings'
        Source = '.\src\SyntaxValidator.cs.refactored.cs'
        Args = @{}
    }
)

foreach ($r in $refactorings) {
    Write-Host "Applying $($r.Tool) to $($r.Source)..." -ForegroundColor Cyan

    .\scripts\Invoke-McpRefactoring.ps1 `
        -ToolName $r.Tool `
        -SourceFile $r.Source `
        -ToolArguments $r.Args `
        -TargetFramework net8.0

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed: $($r.Tool) on $($r.Source)"
        exit 1
    }
}

Write-Host "All refactorings completed successfully!" -ForegroundColor Green
```

### From Claude Code Sessions

When working interactively in Claude Code:

1. **Small files** (<10KB): Use `mcp__MCP_DOCKER__mcp-exec` directly
2. **Large files** (>10KB): Use `Bash` tool to invoke PowerShell script
3. **Multiple refactorings**: Create batch script and execute via `Bash` tool

Example from Claude Code:

```
# Run refactoring via Bash tool
Bash(
  command: 'pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/Invoke-McpRefactoring.ps1 -ToolName extract_method -SourceFile ./src/SyntaxValidator.cs -ToolArguments @{startLine=117; endLine=158; newMethodName="ValidateMethodParameters"} -TargetFramework net8.0',
  description: 'Extract method using MCP refactoring tool'
)
```

## Troubleshooting

### Tools Not Showing Up

```bash
# Verify server is enabled
docker mcp server ls

# Verify tools are registered
docker mcp tools ls | grep extract_method

# Re-add server if needed
docker mcp server disable refactor-csharp-mcp
docker mcp server enable refactor-csharp-mcp

# Verify client connection
docker mcp client ls
```

### Argument Length Errors

If you see "command line too long" errors:

1. Switch to stdin-based invocation (Approach 3)
2. Use PowerShell orchestrator (Approach 2)
3. Break source code into chunks (not recommended)

### JSON Escaping Issues

Common issues:
- **Newlines**: Use `-Compress` flag with `ConvertTo-Json`
- **Quotes**: PowerShell handles escaping automatically
- **Unicode**: Ensure UTF-8 encoding without BOM

```powershell
# Correct JSON creation
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
$json = $args | ConvertTo-Json -Depth 10 -Compress
[System.IO.File]::WriteAllText($tempFile, $json, $utf8NoBom)
```

### Server Not Responding

```bash
# Check server logs
docker logs $(docker ps -q -f ancestor=refactor-csharp-mcp:latest)

# Restart server
docker mcp server restart refactor-csharp-mcp

# Verify gateway
docker mcp gateway status
```

## Performance Considerations

### Large File Handling

| File Size | Approach | Typical Time |
|-----------|----------|--------------|
| <10KB | mcp-exec | 1-2 seconds |
| 10-50KB | PowerShell script | 2-5 seconds |
| 50-200KB | PowerShell script | 5-10 seconds |
| >200KB | Consider chunking | 10+ seconds |

### Optimization Tips

1. **Use caching**: The server caches compilations for repeated operations
2. **Batch operations**: Group related refactorings to reuse compilations
3. **Parallel execution**: Run independent refactorings in parallel
4. **Framework targeting**: Specify exact framework to avoid detection overhead

## Security Considerations

### Source Code Handling

- Scripts create temporary files for large payloads
- Automatic cleanup on success or failure
- No source code logging to stdout (only size metrics)

### Container Isolation

- MCP server runs in isolated Docker container
- No network access by default
- Read-only filesystem for security
- Resource limits enforced by Docker

### Credential Management

- No credentials required for local refactorings
- Container uses dedicated user account
- No filesystem mounting beyond MCP communication

## Advanced Usage

### Custom Tool Wrapper

Create project-specific wrappers:

```powershell
# MyProject-Refactor.ps1
function Refactor-MyClass {
    param([string]$MethodName, [int]$StartLine, [int]$EndLine)

    .\scripts\Invoke-McpRefactoring.ps1 `
        -ToolName extract_method `
        -SourceFile ".\src\MyProject\MyClass.cs" `
        -ToolArguments @{
            startLine = $StartLine
            endLine = $EndLine
            newMethodName = $MethodName
        } `
        -TargetFramework net8.0 `
        -Verbose
}

# Usage
Refactor-MyClass -MethodName "ValidateInput" -StartLine 50 -EndLine 75
```

### Integration with CI/CD

```yaml
# Azure DevOps pipeline example
- task: PowerShell@2
  displayName: 'Apply Refactorings'
  inputs:
    targetType: 'filePath'
    filePath: './scripts/batch-refactor.ps1'
    pwsh: true
    workingDirectory: '$(Build.SourcesDirectory)'
```

### Monitoring and Telemetry

The orchestrator script provides metrics:

```powershell
# Enable verbose logging
.\scripts\Invoke-McpRefactoring.ps1 `
    -ToolName extract_method `
    -SourceFile source.cs `
    -ToolArguments @{...} `
    -Verbose

# Output includes:
# - JSON payload size
# - Invocation time
# - Line count changes
# - Backup locations
```

## Script Reference

### Invoke-McpRefactoring.ps1

**Purpose**: Production-grade orchestrator for MCP refactoring tools

**Parameters**:
- `ToolName`: MCP tool to invoke (extract_method, constructor_injection, etc.)
- `SourceFile`: Path to C# source file
- `OutputFile`: Output path (optional, defaults to *.refactored.cs)
- `ToolArguments`: Hashtable of tool-specific parameters
- `TargetFramework`: .NET framework version (default: net8.0)
- `DryRun`: Preview without executing
- `NoBackup`: Skip backup creation

**Returns**: Exit code 0 on success, 1 on failure

**Features**:
- Automatic backup creation
- UTF-8 encoding without BOM
- Comprehensive error handling
- Progress logging
- Diff statistics

### test-mcp-direct.ps1

**Purpose**: Test MCP tool invocation methods

**Usage**: Demonstrates both stdin and command-line argument approaches

## Additional Resources

- [MCP Specification](https://modelcontextprotocol.io/)
- [Docker MCP Toolkit Docs](https://docs.docker.com/desktop/mcp/)
- [RefactorCsharpMCP README](../README.md)
- [E2E Testing Guide](../E2E-TESTING.md)