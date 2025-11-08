#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Test direct MCP tool invocation through Docker MCP Toolkit
.DESCRIPTION
    This script demonstrates the working pattern for calling MCP tools with large payloads
#>

$ErrorActionPreference = 'Stop'

# Test with a small source file first
$testSource = @'
using System;

namespace TestNamespace
{
    public class TestClass
    {
        private readonly ILogger _logger;

        public void ProcessData(string input)
        {
            // Complex logic that should be extracted
            if (string.IsNullOrEmpty(input))
            {
                throw new ArgumentNullException(nameof(input));
            }

            var normalized = input.Trim().ToLowerInvariant();
            Console.WriteLine($"Processing: {normalized}");
        }
    }
}
'@

Write-Host "Creating test source file..." -ForegroundColor Cyan
$testFile = Join-Path $env:TEMP "test_refactor_source.cs"
$testSource | Out-File -FilePath $testFile -Encoding UTF8 -NoNewline

try {
    # Create arguments JSON
    $arguments = @{
        sourceCode = $testSource
        startLine = 13
        endLine = 16
        newMethodName = "ValidateInput"
        targetFramework = "net8.0"
    } | ConvertTo-Json -Compress

    Write-Host "Arguments JSON length: $($arguments.Length) bytes" -ForegroundColor Gray
    Write-Host ""

    # Method 1: Try with stdin (recommended for large payloads)
    Write-Host "Method 1: Using stdin..." -ForegroundColor Yellow
    $result1 = $arguments | docker mcp tools call extract_method 2>&1

    Write-Host "Result:" -ForegroundColor Green
    Write-Host $result1

    Write-Host "`n---`n" -ForegroundColor Gray

    # Method 2: Try with command-line argument (works for small payloads)
    Write-Host "Method 2: Using command-line argument..." -ForegroundColor Yellow
    $escapedArgs = $arguments -replace '"', '\"'
    $result2 = docker mcp tools call extract_method "$arguments" 2>&1

    Write-Host "Result:" -ForegroundColor Green
    Write-Host $result2

} catch {
    Write-Error "Test failed: $_"
} finally {
    Remove-Item -Path $testFile -Force -ErrorAction SilentlyContinue
}

Write-Host "`n=== Next Steps ===" -ForegroundColor Cyan
Write-Host "If Method 1 works, use it for large files with pipes"
Write-Host "If Method 2 works, use it for small refactorings"
Write-Host ""
Write-Host "For actual refactoring, use:"
Write-Host '  Get-Content source.cs -Raw | ConvertTo-Json | docker mcp tools call extract_method -' -ForegroundColor Green