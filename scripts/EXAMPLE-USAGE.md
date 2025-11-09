# Example Usage: Dogfooding RefactorCsharpMCP

This document demonstrates the complete workflow for using RefactorCsharpMCP to refactor its own codebase.

## Scenario: Refactoring SyntaxValidator.cs

**File**: `src\RefactorCsharpMCP.Core\SyntaxValidator.cs`
**Size**: ~660 lines, 29KB
**Goal**: Extract long methods to improve readability and maintainability

## Step 1: Analyze the File

First, let's use the `analyze_code` tool to identify refactoring opportunities:

```powershell
.\scripts\Invoke-McpRefactoring.ps1 `
    -ToolName analyze_code `
    -SourceFile .\src\RefactorCsharpMCP.Core\SyntaxValidator.cs `
    -TargetFramework net8.0 `
    -OutputFile .\analysis-results.json `
    -Verbose
```

**Expected Output**:
```
[INFO] RefactorCsharpMCP Orchestrator - Starting
[INFO] Tool: analyze_code | Source: SyntaxValidator.cs
[INFO] Source file size: 29847 characters
[INFO] Invoking MCP tool...
[✓] Refactoring succeeded!
[INFO] Line count: 660 → 660 (+0)
[✓] Refactored code written to: analysis-results.json
[✓] Refactoring orchestration complete
```

## Step 2: Extract First Method

Based on analysis, extract method validation logic (lines 117-158):

```powershell
.\scripts\Invoke-McpRefactoring.ps1 `
    -ToolName extract_method `
    -SourceFile .\src\RefactorCsharpMCP.Core\SyntaxValidator.cs `
    -OutputFile .\src\RefactorCsharpMCP.Core\SyntaxValidator.step1.cs `
    -ToolArguments @{
        startLine = 117
        endLine = 158
        newMethodName = 'ValidateMethodParameters'
    } `
    -TargetFramework net8.0 `
    -Verbose
```

**What happens**:
1. Script reads 29KB source file
2. Creates JSON payload (~45KB with escaping)
3. Invokes `extract_method` via Docker MCP
4. Validates result from Roslyn
5. Creates backup: `SyntaxValidator.cs.bak_20250107_143022`
6. Writes refactored code to `SyntaxValidator.step1.cs`

**Expected Output**:
```
[INFO] RefactorCsharpMCP Orchestrator - Starting
[INFO] Tool: extract_method | Source: SyntaxValidator.cs
[INFO] Source file size: 29847 characters
[INFO] Created backup: SyntaxValidator.cs.bak_20250107_143022
[INFO] Invoking MCP tool...
[✓] Refactoring succeeded!
[INFO] Message: Extracted method 'ValidateMethodParameters' (42 lines)
[INFO] Line count: 660 → 665 (+5)
[✓] Refactored code written to: SyntaxValidator.step1.cs
[✓] Refactoring orchestration complete
```

## Step 3: Extract Second Method

Continue with the step1 output:

```powershell
.\scripts\Invoke-McpRefactoring.ps1 `
    -ToolName extract_method `
    -SourceFile .\src\RefactorCsharpMCP.Core\SyntaxValidator.step1.cs `
    -OutputFile .\src\RefactorCsharpMCP.Core\SyntaxValidator.step2.cs `
    -ToolArguments @{
        startLine = 200
        endLine = 245
        newMethodName = 'ValidateClassDeclaration'
    } `
    -TargetFramework net8.0
```

## Step 4: Clean Up Unused Usings

After extracting methods, clean up:

```powershell
.\scripts\Invoke-McpRefactoring.ps1 `
    -ToolName remove_unused_usings `
    -SourceFile .\src\RefactorCsharpMCP.Core\SyntaxValidator.step2.cs `
    -OutputFile .\src\RefactorCsharpMCP.Core\SyntaxValidator.refactored.cs `
    -TargetFramework net8.0
```

## Step 5: Verify and Apply

```powershell
# Run tests to verify refactored code
dotnet test .\tests\RefactorCsharpMCP.Tests\

# If tests pass, replace original
if ($LASTEXITCODE -eq 0) {
    Copy-Item `
        .\src\RefactorCsharpMCP.Core\SyntaxValidator.refactored.cs `
        .\src\RefactorCsharpMCP.Core\SyntaxValidator.cs `
        -Force

    Write-Host "✓ Refactoring applied successfully!" -ForegroundColor Green
} else {
    Write-Error "Tests failed! Review refactored code before applying."
}
```

## Batch Refactoring Script

For multiple refactorings, create a batch script:

```powershell
# batch-refactor-syntaxvalidator.ps1

$ErrorActionPreference = 'Stop'

$refactorings = @(
    # Step 1: Extract method parameters validation
    @{
        Tool = 'extract_method'
        Source = '.\src\RefactorCsharpMCP.Core\SyntaxValidator.cs'
        Output = '.\temp\SyntaxValidator.step1.cs'
        Args = @{
            startLine = 117
            endLine = 158
            newMethodName = 'ValidateMethodParameters'
        }
    },
    # Step 2: Extract class declaration validation
    @{
        Tool = 'extract_method'
        Source = '.\temp\SyntaxValidator.step1.cs'
        Output = '.\temp\SyntaxValidator.step2.cs'
        Args = @{
            startLine = 200
            endLine = 245
            newMethodName = 'ValidateClassDeclaration'
        }
    },
    # Step 3: Extract variable scope analysis
    @{
        Tool = 'extract_method'
        Source = '.\temp\SyntaxValidator.step2.cs'
        Output = '.\temp\SyntaxValidator.step3.cs'
        Args = @{
            startLine = 300
            endLine = 342
            newMethodName = 'AnalyzeVariableScope'
        }
    },
    # Step 4: Clean up unused usings
    @{
        Tool = 'remove_unused_usings'
        Source = '.\temp\SyntaxValidator.step3.cs'
        Output = '.\src\RefactorCsharpMCP.Core\SyntaxValidator.refactored.cs'
        Args = @{}
    }
)

# Create temp directory
New-Item -Path .\temp -ItemType Directory -Force | Out-Null

# Execute refactorings
$stepNumber = 1
foreach ($r in $refactorings) {
    Write-Host "`n=== Step $stepNumber: $($r.Tool) ===" -ForegroundColor Cyan

    $params = @{
        ToolName = $r.Tool
        SourceFile = $r.Source
        OutputFile = $r.Output
        ToolArguments = $r.Args
        TargetFramework = 'net8.0'
    }

    .\scripts\Invoke-McpRefactoring.ps1 @params

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed at step $stepNumber!"
        exit 1
    }

    $stepNumber++
}

Write-Host "`n=== Running Tests ===" -ForegroundColor Cyan
dotnet test .\tests\RefactorCsharpMCP.Tests\

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n=== Applying Refactored Code ===" -ForegroundColor Cyan

    Copy-Item `
        .\src\RefactorCsharpMCP.Core\SyntaxValidator.refactored.cs `
        .\src\RefactorCsharpMCP.Core\SyntaxValidator.cs `
        -Force

    Write-Host "✓ All refactorings completed successfully!" -ForegroundColor Green
    Write-Host "✓ Tests passed!" -ForegroundColor Green
    Write-Host "✓ Changes applied to source file!" -ForegroundColor Green

    # Cleanup temp files
    Remove-Item -Path .\temp -Recurse -Force
} else {
    Write-Error "Tests failed! Refactored code is in: SyntaxValidator.refactored.cs"
    Write-Host "Review the changes before applying manually." -ForegroundColor Yellow
}
```

**Usage**:
```powershell
.\batch-refactor-syntaxvalidator.ps1
```

## From Claude Code Session

When working in a Claude Code session, you can orchestrate the refactoring:

```
Please refactor SyntaxValidator.cs by extracting long methods:

1. Extract lines 117-158 into ValidateMethodParameters
2. Extract lines 200-245 into ValidateClassDeclaration
3. Extract lines 300-342 into AnalyzeVariableScope
4. Clean up unused usings

Use the Invoke-McpRefactoring.ps1 script with the extract_method and remove_unused_usings tools.
```

Claude will then execute:

```powershell
# Step 1
Bash(
  command: 'pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/Invoke-McpRefactoring.ps1 -ToolName extract_method -SourceFile ./src/RefactorCsharpMCP.Core/SyntaxValidator.cs -OutputFile ./temp/step1.cs -ToolArguments @{startLine=117; endLine=158; newMethodName="ValidateMethodParameters"} -TargetFramework net8.0',
  description: 'Extract ValidateMethodParameters method'
)

# Step 2
Bash(
  command: 'pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/Invoke-McpRefactoring.ps1 -ToolName extract_method -SourceFile ./temp/step1.cs -OutputFile ./temp/step2.cs -ToolArguments @{startLine=200; endLine=245; newMethodName="ValidateClassDeclaration"} -TargetFramework net8.0',
  description: 'Extract ValidateClassDeclaration method'
)

# ... and so on
```

## Troubleshooting

### Issue: "Command line too long"

**Solution**: The PowerShell script handles this automatically by using stdin

### Issue: "Tool not found: extract_method"

**Solution**:
```powershell
# Verify server is enabled
docker mcp server ls

# If not enabled:
docker mcp server enable refactor-csharp-mcp

# Restart gateway
docker mcp gateway restart
```

### Issue: "JSON parsing error"

**Solution**: The script uses UTF-8 without BOM encoding, which resolves most parsing issues. If problems persist:

```powershell
# Add -Verbose flag for detailed logging
.\scripts\Invoke-McpRefactoring.ps1 -ToolName extract_method -SourceFile source.cs -Verbose
```

### Issue: Tests fail after refactoring

**Solution**: Review the intermediate files in `.\temp\` directory to identify which step introduced issues

```powershell
# Compare original vs step1
code --diff .\src\SyntaxValidator.cs .\temp\step1.cs

# Run specific test
dotnet test --filter FullyQualifiedName~SyntaxValidatorTests
```

## Performance Metrics

Based on testing with SyntaxValidator.cs:

| Operation | File Size | Execution Time |
|-----------|-----------|----------------|
| Read file | 29KB | <1s |
| JSON serialization | 29KB → 45KB | <1s |
| extract_method | 45KB payload | 3-5s |
| remove_unused_usings | 45KB payload | 2-3s |
| Write output | 30KB | <1s |
| **Total per refactoring** | | **6-10s** |

For batch operations (4 refactorings):
- **Sequential**: ~30-40 seconds
- **Parallel** (future): ~10-15 seconds (if independent)

## Best Practices

1. **Always backup**: Script creates automatic backups, but consider version control
2. **Test incrementally**: Run tests after each refactoring step
3. **Review diffs**: Use `git diff` or `code --diff` to review changes
4. **Incremental commits**: Commit after each successful refactoring
5. **Line number updates**: After each extraction, line numbers shift - review before next step
6. **Framework targeting**: Always specify `-TargetFramework` for consistency

## Next Steps

After successful refactoring:

1. Run full test suite: `dotnet test`
2. Review code coverage: `dotnet test --collect:"XPlat Code Coverage"`
3. Update documentation if APIs changed
4. Create PR with descriptive commit messages
5. Run integration tests: `dotnet test --filter Category=Integration`

## Summary

This workflow demonstrates:
- **Large file handling**: 29KB source code processed successfully
- **Sequential refactoring**: Multiple operations on same file
- **Automation**: Batch scripting for reproducible refactorings
- **Safety**: Automatic backups and test validation
- **Integration**: Seamless use from Claude Code or command line

The key insight: **Don't fight the architecture**. Use the PowerShell orchestrator to handle the complexity of JSON escaping, argument length limits, and error handling. Let Docker MCP and RefactorCsharpMCP focus on what they do best: refactoring C# code with Roslyn.