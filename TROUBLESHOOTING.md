# RefactorCsharpMCP Troubleshooting Guide

This guide helps resolve common issues when using RefactorCsharpMCP with Claude Code and other AI clients.

## Table of Contents

1. [Server Configuration Issues](#server-configuration-issues)
2. [Connection Problems](#connection-problems)
3. [Refactoring Errors](#refactoring-errors)
4. [Performance Issues](#performance-issues)
5. [Cache Issues](#cache-issues)
6. [Platform-Specific Issues](#platform-specific-issues)
7. [GitHub Actions Workflow Issues](#github-actions-workflow-issues)

## Server Configuration Issues

### Server Won't Start

**Symptom**: `dotnet run` command fails or exits immediately

**Solutions**:

1. **Check .NET SDK Version**
   ```bash
   dotnet --version
   # Should be 8.0.304 or later
   ```

2. **Verify Dependencies**
   ```bash
   cd RefactorCsharpMCP
   dotnet restore
   dotnet build
   ```

3. **Check for Port Conflicts**
   - stdio transport doesn't use ports, but ensure no other MCP servers are running

4. **Review Error Logs**
   ```bash
   dotnet run --project src/RefactorCsharpMCP.Server 2>&1 | tee server.log
   ```

### MCP Server Not Discovered by Claude Code

**Symptom**: RefactorCsharpMCP tools don't appear in Claude Code

**Solutions**:

1. **Verify MCP Configuration** (if applicable to your Claude Code version)
   - Check for MCP servers configuration file
   - Ensure correct command path

2. **Test Server Manually**
   ```bash
   cd src/RefactorCsharpMCP.Server
   dotnet run
   # Server should wait for stdin input (correct behavior)
   ```

3. **Check Server Registration**
   ```bash
   # The server uses automatic tool discovery via [McpServerToolType] attributes
   # Ensure all tool classes are properly decorated
   ```

## Connection Problems

### "Server Disconnected" Errors

**Symptom**: Connection drops during refactoring operations

**Solutions**:

1. **Check Server Process**
   ```bash
   # On Windows
   tasklist | findstr dotnet

   # On Linux/Mac
   ps aux | grep dotnet
   ```

2. **Increase Timeout** (if configurable in your client)
   - Default timeout may be too short for large files

3. **Review Server Logs**
   - Look for exceptions in console output
   - Check for memory issues

### stdio Transport Issues

**Symptom**: Server starts but doesn't respond to requests

**Solutions**:

1. **Verify stdio Configuration**
   - Server must use stdio transport for Claude Code
   - Check `Program.cs` has `.WithStdioServerTransport()`

2. **Test with Manual Input** (for debugging)
   ```bash
   echo '{"jsonrpc":"2.0","method":"initialize","params":{},"id":1}' | dotnet run --project src/RefactorCsharpMCP.Server
   ```

## Refactoring Errors

### Extract Method Failures

**Error**: "Invalid line range" or "Line exceeds source code length"

**Solutions**:

1. **Verify Line Numbers**
   - Line numbers are 1-based (first line is 1, not 0)
   - Ensure endLine >= startLine
   - Check that lines exist in source code

2. **Check Source Code Format**
   ```csharp
   // Ensure code is valid C#
   // No syntax errors
   // Proper encoding (UTF-8)
   ```

3. **Test with Simple Example**
   ```csharp
   public class Test
   {
       public void Method()
       {
           var x = 1;  // Line 5
           var y = 2;  // Line 6
       }
   }
   // Extract lines 5-6
   ```

### Constructor Injection Failures

**Error**: "Class not found" or "Method not found"

**Solutions**:

1. **Verify Names Match Exactly**
   - Class names are case-sensitive
   - Method names must match exactly
   - Check for generic types: use `MyClass<T>` format

2. **Check Parameter Names**
   ```bash
   # Comma or semicolon separated
   parameterNames: "logger,config"  # Correct
   parameterNames: "logger, config" # Also works (spaces trimmed)
   ```

3. **Validate Source Code**
   - Ensure class and method exist in provided source
   - Check for nested classes (use full path: "OuterClass.InnerClass")

### "Syntax errors in source code"

**Symptom**: Refactoring fails with parse errors

**Solutions**:

1. **Validate C# Syntax**
   ```bash
   # Use Roslyn to check syntax
   dotnet build
   ```

2. **Check for Special Characters**
   - Ensure proper encoding (UTF-8)
   - Escape special characters in strings

3. **Test in Isolation**
   - Extract problematic class to separate file
   - Test refactoring on minimal example

## Performance Issues

### Slow Refactoring Operations

**Symptom**: Refactorings take >2 seconds for simple code

**Solutions**:

1. **Check File Size**
   - Large files (>10,000 lines) may be slow
   - Consider breaking up large files

2. **Review System Resources**
   ```bash
   # Check memory usage
   dotnet-counters monitor --process-id <pid>
   ```

3. **Optimize Roslyn Analysis**
   - Ensure syntax tree caching is working
   - Check for memory leaks in long-running sessions

### High Memory Usage

**Symptom**: Server uses excessive RAM

**Solutions**:

1. **Restart Server Periodically**
   - Long-running servers may accumulate memory

2. **Check for Leaks**
   ```bash
   dotnet-gcdump collect -p <pid>
   dotnet-gcdump report <dump-file>
   ```

3. **Reduce Concurrent Operations**
   - Process one refactoring at a time

## Cache Issues

### Cache Location and Management

**Cache Location**:
```
%USERPROFILE%/.refactor-csharp-mcp/reference-assemblies/
```

RefactorCsharpMCP caches reference assemblies for 11 supported .NET frameworks (~50MB each, ~550MB total).

### Cache Growing Too Large

**Symptom**: Cache directory exceeds 1GB or disk space concerns

**Solutions**:

1. **Check Cache Size**
   ```bash
   # Windows PowerShell
   Get-ChildItem "$env:USERPROFILE\.refactor-csharp-mcp\reference-assemblies" -Recurse | Measure-Object -Property Length -Sum

   # Linux/Mac
   du -sh ~/.refactor-csharp-mcp/reference-assemblies/
   ```

2. **Clear Specific Framework Cache**
   ```bash
   # Windows PowerShell
   Remove-Item -Recurse -Force "$env:USERPROFILE\.refactor-csharp-mcp\reference-assemblies\net481"

   # Linux/Mac
   rm -rf ~/.refactor-csharp-mcp/reference-assemblies/net481
   ```

3. **Clear All Caches**
   ```bash
   # Windows PowerShell
   Remove-Item -Recurse -Force "$env:USERPROFILE\.refactor-csharp-mcp"

   # Linux/Mac
   rm -rf ~/.refactor-csharp-mcp/
   ```

**Note**: Cache will be automatically rebuilt on next use (requires internet for NuGet downloads).

### Slow First Load for Framework

**Symptom**: First refactoring operation for a framework takes 2-5 seconds

**Explanation**: This is expected behavior. Reference assemblies are downloaded from NuGet on first use (~50MB per framework).

**Solutions**:

1. **Pre-warm Cache** (optional)
   ```bash
   # Test with simple code to trigger cache population
   # The server will download needed frameworks automatically
   ```

2. **Check Internet Connection**
   - NuGet download requires internet connectivity
   - Verify no proxy/firewall blocking nuget.org

3. **Verify Download Progress**
   - Server logs show "Cache miss for {framework}, resolving..."
   - Check logs for download errors

### Cache Corruption

**Symptom**: "Failed to load cached assembly" warnings in logs

**Solutions**:

1. **Clear Corrupted Framework Cache**
   ```bash
   # Windows PowerShell
   Remove-Item -Recurse -Force "$env:USERPROFILE\.refactor-csharp-mcp\reference-assemblies\net8.0"

   # Linux/Mac
   rm -rf ~/.refactor-csharp-mcp/reference-assemblies/net8.0
   ```

2. **Verify Disk Health**
   - Run disk check utility
   - Ensure adequate free space (>2GB recommended)

3. **Check File Permissions**
   ```bash
   # Linux/Mac - ensure write permissions
   chmod -R u+w ~/.refactor-csharp-mcp/
   ```

### File Locking Issues

**Symptom**: "File is being used by another process" errors

**Solutions**:

1. **Retry Automatically**
   - Built-in retry logic handles transient locks (50ms, 200ms, 500ms delays)
   - Most errors resolve automatically

2. **Check for Antivirus Interference**
   - Some antivirus software locks DLL files during scanning
   - Add cache directory to exclusions list

3. **Close Other Processes**
   ```bash
   # Windows - check processes using cache files
   handle.exe $env:USERPROFILE\.refactor-csharp-mcp

   # Linux/Mac
   lsof ~/.refactor-csharp-mcp/
   ```

### Cache Performance Issues

**Symptom**: Cache hits still slow (>1 second)

**Solutions**:

1. **Check Disk Performance**
   - SSD recommended for optimal cache performance
   - HDD may show 2-5x slower cache access

2. **Verify Memory Cache**
   - Memory cache should provide <10ms access for active frameworks
   - Restart server if memory cache seems inactive

3. **Review Cache Statistics**
   - Check logs for "Memory cache hit" vs "Disk cache hit"
   - Memory cache misses indicate server restarts or memory pressure

### Future Cache Management

**Note**: Automatic cache eviction (LRU policy) is planned for a future release. See [FUTURE-ROADMAP.md](docs/FUTURE-ROADMAP.md) V2.5.4 for details.

For now, manual cache management is recommended:
- Monitor cache size periodically
- Clear unused framework caches manually
- Keep 2-3 frequently used frameworks cached

## Platform-Specific Issues

### Windows Issues

**Issue**: Path separators in configuration

**Solution**:
```json
{
  "command": "dotnet",
  "args": ["run", "--project", "C:/src/DevTools/RefactorCsharpMCP/src/RefactorCsharpMCP.Server"]
}
```
Use forward slashes `/` or escaped backslashes `\\`

**Issue**: PowerShell execution policy

**Solution**:
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### Linux/Mac Issues

**Issue**: Permission denied

**Solution**:
```bash
chmod +x /path/to/dotnet
```

**Issue**: .NET not in PATH

**Solution**:
```bash
export PATH=$PATH:$HOME/.dotnet
```

### Docker Issues

**Issue**: Container can't find .NET SDK

**Solution**:
- Ensure Dockerfile uses correct SDK image
- Verify .NET 8 SDK is installed in container

## Diagnostic Commands

### Test Server Startup
```bash
cd src/RefactorCsharpMCP.Server
dotnet run --verbosity detailed
```

### Check Tool Registration
```bash
# Server should log discovered tools on startup
dotnet run --project src/RefactorCsharpMCP.Server 2>&1 | grep -i "tool"
```

### Validate Roslyn Installation
```bash
dotnet list package | grep Microsoft.CodeAnalysis
```

### Test Refactoring Logic
```bash
cd src/RefactorCsharpMCP.Tests
dotnet test --verbosity normal
```

## Common Error Messages

### "Source code cannot be empty"
- **Cause**: Empty or null source code provided
- **Fix**: Ensure source code parameter is populated

### "Class 'X' not found in source code"
- **Cause**: Class name mismatch or class doesn't exist
- **Fix**: Verify exact class name, check for typos

### "Method 'X' not found in class 'Y'"
- **Cause**: Method name mismatch or method doesn't exist
- **Fix**: Verify exact method name in specified class

### "Not all specified parameters found"
- **Cause**: One or more parameter names don't exist in method
- **Fix**: Check parameter names match method signature exactly

### "Invalid line range: X-Y"
- **Cause**: startLine > endLine or startLine < 1
- **Fix**: Ensure startLine <= endLine and both >= 1

## Framework-Specific Error Codes

### IDE Analyzer Limitations (Issue #72)

**Error Pattern**: "IDE analyzer functionality not available" or missing CS8019/IDE0005 diagnostics

**Diagnostic Codes Affected**:
- **CS8019**: Unnecessary using directive
- **IDE0005**: Using directive is unnecessary

**Root Cause**: RefactorCsharpMCP uses Roslyn compiler APIs which have different capabilities than full IDE workspace APIs. IDE diagnostics like CS8019 and IDE0005 require workspace context (project files, solution structure, external references) which is not available when analyzing individual source code strings.

**Symptoms**:
- `remove_unused_usings` may not detect all unused directives
- `analyze_code` may not return CS8019/IDE0005 diagnostics
- `fix_diagnostic` cannot fix IDE0005 if not detected

**Solutions**:

1. **Use Modern IDE for Unused Using Detection**:
   ```bash
   # Use Visual Studio, VS Code with C# extension, or Rider
   # These have full workspace context for accurate detection
   ```

2. **Manual Review**:
   ```csharp
   // Check each using directive manually
   // Remove directives not referenced in the code
   ```

3. **Alternative Validation**:
   ```bash
   # Use Roslyn analyzers in full build
   dotnet build /p:TreatWarningsAsErrors=true
   ```

**Test Suite Status**: 12 tests skipped due to this limitation (documented with Skip attribute)

**Workaround**: For unused using detection, use IDE-based refactoring tools that have full workspace context.

### .NET Framework Reference Assembly Errors (Issue #75)

**Error Pattern**: "Code references types or members not available in {framework}"

**Example Errors**:
```
Code references types or members not available in net48
Failed to load reference assemblies for net48
The name 'result' does not exist in the current context (net48)
```

**Root Cause**: Cross-framework refactoring requires NuGet-distributed reference assemblies. For legacy .NET Framework versions (net48, net472, net471, etc.), reference assemblies may not be available in all environments or may have incomplete type information.

**Framework Support Matrix**:

| Framework | Support Level | Status |
|-----------|--------------|--------|
| net9.0 | Full | ✅ Fully supported |
| net8.0 | Full | ✅ Fully supported |
| netstandard2.1 | Full | ✅ Fully supported |
| netstandard2.0 | Full | ✅ Fully supported |
| net48 | Limited | ⚠️ May fail on some systems |
| net481 | Limited | ⚠️ May fail on some systems |
| net472 | Limited | ⚠️ May fail on some systems |
| net471, net47, net462 | Limited | ⚠️ May fail on some systems |
| net35 | Limited | ⚠️ May fail on some systems |

**Solutions**:

1. **Prefer Modern Frameworks**:
   ```csharp
   // Use net8.0 or net9.0 for best reliability
   targetFramework: "net8.0"  // Recommended
   ```

2. **Manual Reference Assembly Installation**:
   ```bash
   # Install reference assemblies NuGet package
   dotnet add package Microsoft.NETFramework.ReferenceAssemblies
   ```

3. **Cache Pre-warming Strategy**:
   ```bash
   # Run refactorings on modern frameworks first
   # This populates the cache with working assemblies
   # Then fallback to net48 if needed
   ```

4. **Check Cache Directory**:
   ```bash
   # Windows PowerShell
   Get-ChildItem "$env:USERPROFILE\.refactor-csharp-mcp\reference-assemblies\net48"

   # Linux/Mac
   ls -la ~/.refactor-csharp-mcp/reference-assemblies/net48
   ```

5. **Clear and Retry**:
   ```bash
   # If reference assemblies are corrupted, clear and re-download
   Remove-Item -Recurse "$env:USERPROFILE\.refactor-csharp-mcp\reference-assemblies\net48"

   # Next refactoring will re-download from NuGet
   ```

**Test Suite Handling**: Framework matrix tests use conditional assertions to allow graceful net48 failures while validating modern frameworks.

**Recommendation**: For production use, target net8.0 or net9.0. Use net48 only when absolutely required by your project constraints.

### Language Version Mismatch Errors

**Error Pattern**: "C# {version} syntax should not work on {framework}"

**Example Errors**:
```
C# 12 syntax should not work on net48
Collection expression syntax not supported in C# 7.3
Primary constructor syntax requires C# 12
```

**Language Version Mapping**:
- net9.0 → C# 13 (latest features)
- net8.0 → C# 12 (collection expressions, primary constructors)
- net48, netstandard2.0 → C# 7.3 (limited features)

**Unsupported Syntax Examples**:

```csharp
// C# 12 Collection Expressions (net8.0+)
int[] numbers = [1, 2, 3];  // ❌ Fails on net48

// C# 7.3 Compatible (all frameworks)
int[] numbers = new[] { 1, 2, 3 };  // ✅ Works on all frameworks

// C# 12 Primary Constructors (net8.0+)
public class Point(int x, int y);  // ❌ Fails on net48

// C# 7.3 Compatible
public class Point {  // ✅ Works on all frameworks
    public Point(int x, int y) { X = x; Y = y; }
    public int X { get; }
    public int Y { get; }
}
```

**Solutions**:

1. **Use Framework-Compatible Syntax**:
   - Check language version for your target framework
   - Avoid modern syntax when targeting legacy frameworks

2. **Framework Validation**:
   ```csharp
   // RefactorCsharpMCP automatically validates syntax compatibility
   // Error messages will indicate language version mismatch
   ```

3. **Upgrade Target Framework**:
   ```csharp
   // If modern syntax is needed, upgrade target framework
   targetFramework: "net8.0"  // Supports C# 12
   ```

### Unsupported Framework Errors

**Error Pattern**: "Unsupported framework: {framework}"

**EOL (End-of-Life) Frameworks**:
- net6.0 (EOL November 2024)
- net5.0 (EOL May 2022)
- netcoreapp3.1 (EOL December 2022)

**Solutions**:

1. **Use Supported Frameworks**:
   ```csharp
   // Modern .NET
   "net8.0"  // Supported until November 2026
   "net9.0"  // Supported until May 2026

   // Legacy .NET Framework
   "net48"   // Still supported (limited)

   // .NET Standard
   "netstandard2.0"  // Cross-platform compatibility
   ```

2. **Upgrade Your Project**:
   ```bash
   # Upgrade project to supported framework
   # Edit .csproj file:
   <TargetFramework>net8.0</TargetFramework>
   ```

**Microsoft Support Timeline**: See [.NET Support Policy](https://dotnet.microsoft.com/platform/support/policy) for current framework support status.

## GitHub Actions Workflow Issues

### cache-stability.yml Workflow Failing Consistently

**Symptom**: All GitHub Actions workflow runs fail with "This run likely failed because of a workflow file issue"

**Root Cause**: YAML syntax errors in workflow file, typically in `github-script` actions

**Common Issues**:

1. **GitHub Actions Expressions in JavaScript Template Literals**

   **Problem**: Using `${{ }}` syntax inside JavaScript backtick strings in `github-script` blocks

   ```yaml
   # INCORRECT - causes YAML parsing error
   script: |
     github.rest.issues.createComment({
       body: `https://github.com/${{ github.repository }}/runs/${{ github.run_id }}`
     })
   ```

   **Solution**: Use JavaScript context objects instead

   ```yaml
   # CORRECT
   script: |
     github.rest.issues.createComment({
       body: 'https://github.com/' + context.repo.owner + '/' + context.repo.repo + '/actions/runs/' + context.runId
     })
   ```

2. **Emoji or Special Characters Causing Encoding Issues**

   **Problem**: UTF-8 emojis (✅, ❌) or special characters in YAML workflow files

   **Solution**: Replace emojis with ASCII equivalents (✓, ✗) or remove entirely

3. **Colons in Markdown Lists Inside YAML Strings**

   **Problem**: Colons followed by spaces are interpreted as YAML key-value separators

   ```yaml
   # INCORRECT - colon causes YAML parsing error
   body: `
   - Linux (bash): 100% pass
   `
   ```

   **Solution**: Change formatting to avoid colon-space pattern or use quotes properly

   ```yaml
   # CORRECT - avoid colon or use different formatting
   body: '- Linux (bash) - 100% pass'
   ```

**Diagnostic Steps**:

1. **Validate YAML Syntax Locally**
   ```bash
   # Using Python YAML parser (requires: pip install pyyaml)
   python3 -c "import yaml, sys; yaml.safe_load(open('.github/workflows/cache-stability.yml', encoding='utf-8')); print('✓ Valid YAML')" 2>&1 || echo "✗ Invalid YAML or PyYAML not installed"

   # Alternative: Use yamllint for more detailed validation
   yamllint .github/workflows/cache-stability.yml
   ```

2. **Check Recent Workflow Runs**
   ```bash
   gh run list --workflow=cache-stability.yml --limit 5
   gh run view <run-id>
   ```

3. **Look for "workflow file issue" Message**
   ```bash
   gh run view <run-id> 2>&1 | grep "workflow file"
   ```

**Prevention**:

1. Always use JavaScript context objects (`context.repo.owner`, `context.runId`) instead of workflow expressions (`${{ github.repository }}`) inside `script:` blocks

2. Test workflow files locally with YAML validators before committing

3. Use single-quoted strings with explicit newlines (`\n`) instead of backtick template literals in `github-script` actions

4. Avoid emojis in workflow files, use ASCII equivalents

## Getting Help

### Enable Detailed Logging

For .NET applications, set environment variable:
```bash
# Windows
set DOTNET_LOGGING__CONSOLE__LOGLEVEL=Debug

# Linux/Mac
export DOTNET_LOGGING__CONSOLE__LOGLEVEL=Debug
```

### Collect Diagnostics

1. Server logs: `dotnet run > server.log 2>&1`
2. Test results: `dotnet test --logger "console;verbosity=detailed"`
3. System info: `dotnet --info`

### Report Issues

When reporting issues, include:
- .NET SDK version (`dotnet --version`)
- OS and version
- Exact error message
- Minimal reproduction code
- Server logs (if applicable)

## Performance Benchmarks

Expected performance for typical refactorings:

| Operation | File Size | Expected Time |
|-----------|-----------|---------------|
| Extract Method | < 1,000 lines | < 500ms |
| Extract Method | 1,000-5,000 lines | < 1s |
| Constructor Injection | < 1,000 lines | < 500ms |
| Constructor Injection | 1,000-5,000 lines | < 1s |

If performance is significantly worse, check:
- System resources (CPU, RAM)
- Antivirus software interference
- Network issues (though stdio doesn't use network)

## Best Practices

1. **Keep Files Reasonably Sized**
   - < 5,000 lines per file for optimal performance

2. **Validate Code Before Refactoring**
   - Ensure code compiles
   - Run tests to establish baseline

3. **Use Version Control**
   - Commit before major refactorings
   - Easy to revert if needed

4. **Test Incrementally**
   - Apply one refactoring at a time
   - Verify results before proceeding

5. **Monitor Server Health**
   - Restart periodically for long sessions
   - Watch for memory usage trends
