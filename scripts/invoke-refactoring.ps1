#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Wrapper script to invoke RefactorCsharp MCP tools with large source files
.DESCRIPTION
    Handles JSON escaping and large payload transport for MCP tool invocation
.PARAMETER Tool
    The refactoring tool name (e.g., extract_method, constructor_injection)
.PARAMETER SourceFile
    Path to the C# source file to refactor
.PARAMETER OutputFile
    Path where refactored code will be written (optional, defaults to stdout)
.PARAMETER Arguments
    Additional arguments as hashtable (converted to JSON)
.EXAMPLE
    .\invoke-refactoring.ps1 -Tool extract_method -SourceFile SyntaxValidator.cs -Arguments @{startLine=117; endLine=158; newMethodName="ValidateMethodParameters"}
#>
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('extract_method', 'constructor_injection', 'make_field_readonly',
                 'safe_delete_method', 'extract_class', 'remove_unused_usings',
                 'inline_method', 'rename_symbol', 'inline_variable',
                 'analyze_code', 'fix_diagnostic')]
    [string]$Tool,

    [Parameter(Mandatory=$true)]
    [string]$SourceFile,

    [string]$OutputFile,

    [Parameter(Mandatory=$true)]
    [hashtable]$Arguments
)

$ErrorActionPreference = 'Stop'

try {
    # Read source file
    if (-not (Test-Path $SourceFile)) {
        throw "Source file not found: $SourceFile"
    }

    $sourceCode = Get-Content -Path $SourceFile -Raw
    Write-Host "Read source file: $($sourceCode.Length) characters" -ForegroundColor Green

    # Add sourceCode to arguments
    $Arguments['sourceCode'] = $sourceCode

    # Handle framework-specific requirements
    if ($Tool -in @('analyze_code', 'fix_diagnostic', 'remove_unused_usings')) {
        if (-not $Arguments.ContainsKey('targetFramework')) {
            $Arguments['targetFramework'] = 'net8.0'
            Write-Host "Added default targetFramework: net8.0" -ForegroundColor Yellow
        }
    }

    # Convert arguments to JSON with proper escaping
    $jsonArgs = $Arguments | ConvertTo-Json -Depth 10 -Compress

    # Create temporary file for arguments (avoids command-line length limits)
    $tempArgFile = [System.IO.Path]::GetTempFileName()
    $jsonArgs | Out-File -FilePath $tempArgFile -Encoding UTF8 -NoNewline

    Write-Host "Invoking tool: $Tool" -ForegroundColor Cyan
    Write-Host "Arguments file: $tempArgFile ($($jsonArgs.Length) bytes)" -ForegroundColor Gray

    # Build the docker command to use mcp-exec
    # We'll pass the JSON through stdin to avoid shell escaping issues
    $dockerCmd = @"
docker run --rm -i `
    -v "${tempArgFile}:/tmp/args.json:ro" `
    -e MCP_TOOL=$Tool `
    --entrypoint sh `
    refactor-csharp-mcp:latest `
    -c 'cat /tmp/args.json | docker mcp tools exec refactor-csharp-mcp $Tool -'
"@

    # Alternative: Use docker mcp directly if available
    $result = & docker mcp tools exec refactor-csharp-mcp $Tool --json-file $tempArgFile 2>&1

    # Parse result
    if ($LASTEXITCODE -ne 0) {
        throw "Tool execution failed: $result"
    }

    # Extract refactored code from result
    $resultObj = $result | ConvertFrom-Json

    if ($resultObj.success -eq $false) {
        throw "Refactoring failed: $($resultObj.error)"
    }

    $refactoredCode = $resultObj.refactoredCode

    # Output result
    if ($OutputFile) {
        $refactoredCode | Out-File -FilePath $OutputFile -Encoding UTF8 -NoNewline
        Write-Host "Refactored code written to: $OutputFile" -ForegroundColor Green
    } else {
        Write-Output $refactoredCode
    }

    # Cleanup
    Remove-Item -Path $tempArgFile -Force -ErrorAction SilentlyContinue

    Write-Host "Refactoring completed successfully" -ForegroundColor Green

} catch {
    Write-Error "Refactoring failed: $_"

    # Cleanup on error
    if ($tempArgFile -and (Test-Path $tempArgFile)) {
        Remove-Item -Path $tempArgFile -Force -ErrorAction SilentlyContinue
    }

    exit 1
}