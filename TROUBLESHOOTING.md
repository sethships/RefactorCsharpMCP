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
   # Using Python YAML parser
   python -c "import yaml; yaml.safe_load(open('.github/workflows/cache-stability.yml', encoding='utf-8')); print('Valid YAML')"
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
