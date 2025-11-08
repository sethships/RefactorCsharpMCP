#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Production-grade MCP refactoring orchestrator for large C# files
.DESCRIPTION
    Handles large payloads, proper JSON escaping, error handling, and result validation
    Works with Docker MCP Toolkit and RefactorCsharpMCP server
.PARAMETER ToolName
    MCP tool name (extract_method, constructor_injection, etc.)
.PARAMETER SourceFile
    Path to C# source file to refactor
.PARAMETER OutputFile
    Path for refactored output (optional, defaults to source file with .refactored.cs)
.PARAMETER ToolArguments
    Hashtable of tool-specific arguments (startLine, endLine, newMethodName, etc.)
.PARAMETER TargetFramework
    Target .NET framework version (default: net8.0)
.PARAMETER DryRun
    Show what would be done without executing
.PARAMETER Verbose
    Enable verbose logging
.EXAMPLE
    .\Invoke-McpRefactoring.ps1 -ToolName extract_method -SourceFile .\src\SyntaxValidator.cs -ToolArguments @{startLine=117; endLine=158; newMethodName='ValidateMethodParameters'}
.EXAMPLE
    .\Invoke-McpRefactoring.ps1 -ToolName remove_unused_usings -SourceFile .\src\Program.cs -TargetFramework net8.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet(
        'extract_method',
        'constructor_injection',
        'make_field_readonly',
        'safe_delete_method',
        'extract_class',
        'remove_unused_usings',
        'inline_method',
        'rename_symbol',
        'inline_variable',
        'analyze_code',
        'fix_diagnostic'
    )]
    [string]$ToolName,

    [Parameter(Mandatory=$true)]
    [ValidateScript({ Test-Path $_ })]
    [string]$SourceFile,

    [string]$OutputFile,

    [hashtable]$ToolArguments = @{},

    [string]$TargetFramework = 'net8.0',

    [switch]$DryRun,

    [switch]$NoBackup
)

$ErrorActionPreference = 'Stop'

# --- Helper Functions ---

function Write-Log {
    param(
        [string]$Message,
        [ValidateSet('Info', 'Success', 'Warning', 'Error')]
        [string]$Level = 'Info'
    )

    $color = switch ($Level) {
        'Info'    { 'Cyan' }
        'Success' { 'Green' }
        'Warning' { 'Yellow' }
        'Error'   { 'Red' }
    }

    $prefix = switch ($Level) {
        'Info'    { '[INFO]' }
        'Success' { '[✓]' }
        'Warning' { '[WARN]' }
        'Error'   { '[✗]' }
    }

    Write-Host "$prefix $Message" -ForegroundColor $color
}

function Test-DockerMcpAvailable {
    try {
        $null = docker mcp --version 2>&1
        return $true
    } catch {
        return $false
    }
}

function Test-McpServerEnabled {
    param([string]$ServerName)

    try {
        $servers = docker mcp server ls 2>&1 | Out-String
        return $servers -match $ServerName
    } catch {
        return $false
    }
}

function Invoke-McpToolDirect {
    param(
        [string]$Tool,
        [hashtable]$Arguments
    )

    Write-Log "Preparing MCP tool invocation: $Tool" -Level Info

    # Convert arguments to JSON
    $jsonPayload = $Arguments | ConvertTo-Json -Depth 10 -Compress

    Write-Verbose "JSON payload size: $($jsonPayload.Length) bytes"

    # Create temporary file for large payloads (safer than stdin)
    $tempJsonFile = [System.IO.Path]::GetTempFileName()

    try {
        # Write JSON to temp file with UTF-8 no BOM
        $utf8NoBom = New-Object System.Text.UTF8Encoding $false
        [System.IO.File]::WriteAllText($tempJsonFile, $jsonPayload, $utf8NoBom)

        Write-Verbose "Temp file: $tempJsonFile"

        # Invoke MCP tool via Docker MCP Toolkit
        # Using file input to avoid shell escaping and argument length limits
        $result = Get-Content -Path $tempJsonFile -Raw | docker mcp tools call $Tool 2>&1

        if ($LASTEXITCODE -ne 0) {
            throw "Docker MCP command failed with exit code $LASTEXITCODE`n$result"
        }

        return $result

    } finally {
        # Cleanup temp file
        if (Test-Path $tempJsonFile) {
            Remove-Item -Path $tempJsonFile -Force -ErrorAction SilentlyContinue
        }
    }
}

function Parse-McpResult {
    param([string]$RawResult)

    try {
        # Try to parse as JSON
        $resultObj = $RawResult | ConvertFrom-Json

        # Check for success indicators (different tools may use different property names)
        $success = $resultObj.success -eq $true -or `
                   $resultObj.isSuccess -eq $true -or `
                   $resultObj.Success -eq $true -or `
                   ($null -ne $resultObj.refactoredCode -and $resultObj.refactoredCode.Length -gt 0)

        if ($success) {
            return @{
                Success = $true
                RefactoredCode = $resultObj.refactoredCode
                Message = $resultObj.message
            }
        } else {
            return @{
                Success = $false
                Error = $resultObj.error -or $resultObj.errorMessage -or "Unknown error"
            }
        }

    } catch {
        # If parsing fails, treat raw result as error
        return @{
            Success = $false
            Error = "Failed to parse MCP result: $_`nRaw result: $RawResult"
        }
    }
}

function Backup-SourceFile {
    param([string]$FilePath)

    if ($NoBackup) {
        Write-Verbose "Skipping backup (NoBackup flag set)"
        return $null
    }

    $backupPath = "$FilePath.bak_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
    Copy-Item -Path $FilePath -Destination $backupPath -Force

    Write-Log "Created backup: $backupPath" -Level Info
    return $backupPath
}

# --- Main Execution ---

try {
    Write-Log "RefactorCsharpMCP Orchestrator - Starting" -Level Info
    Write-Log "Tool: $ToolName | Source: $(Split-Path -Leaf $SourceFile)" -Level Info

    # Validate prerequisites
    if (-not (Test-DockerMcpAvailable)) {
        throw "Docker MCP Toolkit is not available. Please install: https://docs.docker.com/desktop/mcp/"
    }

    if (-not (Test-McpServerEnabled 'refactor-csharp-mcp')) {
        throw "RefactorCsharpMCP server is not enabled. Run: docker mcp server enable refactor-csharp-mcp"
    }

    # Read source file
    $sourceFilePath = Resolve-Path $SourceFile
    $sourceCode = Get-Content -Path $sourceFilePath -Raw

    Write-Log "Source file size: $($sourceCode.Length) characters" -Level Info

    # Build arguments for MCP tool
    $mcpArguments = @{
        sourceCode = $sourceCode
    }

    # Add tool-specific arguments
    foreach ($key in $ToolArguments.Keys) {
        $mcpArguments[$key] = $ToolArguments[$key]
    }

    # Add targetFramework for tools that require it
    $frameworkAwareTools = @('extract_method', 'analyze_code', 'fix_diagnostic', 'remove_unused_usings', 'inline_method', 'inline_variable')
    if ($ToolName -in $frameworkAwareTools) {
        if (-not $mcpArguments.ContainsKey('targetFramework')) {
            $mcpArguments['targetFramework'] = $TargetFramework
        }
    }

    # Log arguments (without source code for brevity)
    Write-Verbose "Tool arguments:"
    foreach ($key in $mcpArguments.Keys) {
        if ($key -ne 'sourceCode') {
            Write-Verbose "  $key = $($mcpArguments[$key])"
        }
    }

    if ($DryRun) {
        Write-Log "DRY RUN: Would invoke $ToolName with $($mcpArguments.Count) arguments" -Level Warning
        return
    }

    # Invoke MCP tool
    Write-Log "Invoking MCP tool..." -Level Info
    $rawResult = Invoke-McpToolDirect -Tool $ToolName -Arguments $mcpArguments

    # Parse result
    $result = Parse-McpResult -RawResult $rawResult

    if ($result.Success) {
        Write-Log "Refactoring succeeded!" -Level Success

        if ($result.Message) {
            Write-Log "Message: $($result.Message)" -Level Info
        }

        # Determine output file
        if (-not $OutputFile) {
            $OutputFile = $sourceFilePath -replace '\.cs$', '.refactored.cs'
        }

        # Backup original if writing to same file
        if ((Resolve-Path $OutputFile -ErrorAction SilentlyContinue) -eq $sourceFilePath) {
            Backup-SourceFile -FilePath $sourceFilePath
        }

        # Write refactored code
        $utf8NoBom = New-Object System.Text.UTF8Encoding $false
        [System.IO.File]::WriteAllText($OutputFile, $result.RefactoredCode, $utf8NoBom)

        Write-Log "Refactored code written to: $OutputFile" -Level Success

        # Show diff statistics
        $originalLines = ($sourceCode -split "`r?`n").Count
        $refactoredLines = ($result.RefactoredCode -split "`r?`n").Count
        $lineDiff = $refactoredLines - $originalLines

        Write-Log "Line count: $originalLines → $refactoredLines ($($lineDiff -ge 0 ? '+' : '')$lineDiff)" -Level Info

    } else {
        Write-Log "Refactoring failed!" -Level Error
        Write-Log "Error: $($result.Error)" -Level Error
        exit 1
    }

} catch {
    Write-Log "Fatal error: $($_.Exception.Message)" -Level Error
    Write-Verbose $_.ScriptStackTrace
    exit 1
}

Write-Log "Refactoring orchestration complete" -Level Success