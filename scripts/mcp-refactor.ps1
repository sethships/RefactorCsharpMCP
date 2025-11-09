#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Direct MCP refactoring invocation using mcp-exec with file-based approach
.DESCRIPTION
    Optimized for large source files, uses temporary files to avoid JSON escaping issues
#>
param(
    [Parameter(Mandatory=$true)]
    [string]$Tool,

    [Parameter(Mandatory=$true)]
    [string]$SourceFile,

    [string]$OutputFile,

    [Parameter(ValueFromRemainingArguments=$true)]
    $ExtraArgs
)

$ErrorActionPreference = 'Stop'

function Invoke-MCPTool {
    param(
        [string]$ToolName,
        [hashtable]$Arguments
    )

    # Create a temporary PowerShell script that will be executed
    $tempScript = [System.IO.Path]::GetTempFileName()
    $tempScript = [System.IO.Path]::ChangeExtension($tempScript, '.ps1')

    $scriptContent = @"
# Direct invocation script for MCP tool
`$arguments = @{
$(
    foreach ($key in $Arguments.Keys) {
        $value = $Arguments[$key]
        if ($value -is [string]) {
            "    '$key' = @'`n$value`n'@"
        } else {
            "    '$key' = $value"
        }
    }
)
}

# Use docker mcp tools exec with proper JSON encoding
`$json = `$arguments | ConvertTo-Json -Depth 10 -Compress
`$result = `$json | docker mcp tools exec refactor-csharp-mcp $ToolName -
Write-Output `$result
"@

    $scriptContent | Out-File -FilePath $tempScript -Encoding UTF8

    try {
        $result = & pwsh -NoProfile -ExecutionPolicy Bypass -File $tempScript
        return $result | ConvertFrom-Json
    } finally {
        Remove-Item -Path $tempScript -Force -ErrorAction SilentlyContinue
    }
}

# Main execution
try {
    Write-Host "Reading source file: $SourceFile" -ForegroundColor Cyan
    $sourceCode = Get-Content -Path $SourceFile -Raw

    # Parse extra arguments into hashtable
    $args = @{
        sourceCode = $sourceCode
    }

    # Parse key=value pairs from ExtraArgs
    foreach ($arg in $ExtraArgs) {
        if ($arg -match '^(\w+)=(.+)$') {
            $key = $Matches[1]
            $value = $Matches[2]

            # Try to parse as number if possible
            if ($value -match '^\d+$') {
                $args[$key] = [int]$value
            } else {
                $args[$key] = $value
            }
        }
    }

    # Add default targetFramework if needed
    if ($Tool -in @('analyze_code', 'fix_diagnostic', 'remove_unused_usings')) {
        if (-not $args.ContainsKey('targetFramework')) {
            $args['targetFramework'] = 'net8.0'
        }
    }

    Write-Host "Invoking $Tool with:" -ForegroundColor Yellow
    $args.Keys | ForEach-Object {
        if ($_ -ne 'sourceCode') {
            Write-Host "  $_`: $($args[$_])" -ForegroundColor Gray
        } else {
            Write-Host "  sourceCode: $($args['sourceCode'].Length) characters" -ForegroundColor Gray
        }
    }

    $result = Invoke-MCPTool -ToolName $Tool -Arguments $args

    if ($result.success -eq $true -or $result.isSuccess -eq $true) {
        $refactoredCode = $result.refactoredCode

        if ($OutputFile) {
            $refactoredCode | Out-File -FilePath $OutputFile -Encoding UTF8 -NoNewline
            Write-Host "✓ Refactored code written to: $OutputFile" -ForegroundColor Green
        } else {
            Write-Output $refactoredCode
        }
    } else {
        throw "Refactoring failed: $($result.error -or $result.errorMessage)"
    }

} catch {
    Write-Error "Failed to execute refactoring: $_"
    exit 1
}

# Usage examples:
Write-Host @"

EXAMPLES:
  .\mcp-refactor.ps1 -Tool extract_method -SourceFile SyntaxValidator.cs -OutputFile SyntaxValidator.refactored.cs startLine=117 endLine=158 newMethodName=ValidateMethodParameters

  .\mcp-refactor.ps1 -Tool remove_unused_usings -SourceFile Program.cs -OutputFile Program.clean.cs targetFramework=net8.0

  .\mcp-refactor.ps1 -Tool make_field_readonly -SourceFile MyClass.cs className=MyClass fieldName=_logger
"@ -ForegroundColor DarkGray