#
# Cache Stability Test Script - WSL Bridge (PowerShell → WSL)
#
# Runs the bash version of the cache stability test via WSL on Windows.
# This validates that the bash script works correctly on Windows/WSL.
#
# Usage: .\test-cache-stability-wsl.ps1 [-Iterations N]
#
# Falls back to native PowerShell version if WSL is not available.
#

param(
    [int]$Iterations = 10
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "==== Cache Stability Test - WSL Bridge ====" -ForegroundColor Cyan
Write-Host ""

# Check if WSL is available
try {
    $wslCheck = wsl --status 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "WSL not available"
    }
    Write-Host "✓ WSL detected" -ForegroundColor Green
} catch {
    Write-Host "✗ WSL not available on this system" -ForegroundColor Red
    Write-Host "⚠ Falling back to native PowerShell version..." -ForegroundColor Yellow
    Write-Host ""

    # Fall back to native PowerShell version
    $scriptPath = Join-Path $PSScriptRoot "test-cache-stability.ps1"
    & $scriptPath -Iterations $Iterations
    exit $LASTEXITCODE
}

# Get the script directory in WSL format (support any drive letter)
$scriptDir = $PSScriptRoot
$driveLetter = ($scriptDir -split ':')[0].ToLower()
$wslScriptDir = $scriptDir -replace '\\', '/' -replace "^${driveLetter}:", "/mnt/${driveLetter}"

# Build the bash script path
$bashScript = "$wslScriptDir/test-cache-stability.sh"

Write-Host "Script location: $bashScript" -ForegroundColor Gray
Write-Host "Iterations: $Iterations" -ForegroundColor Gray
Write-Host ""

# Run the bash script via WSL
Write-Host "Running bash script via WSL..." -ForegroundColor Cyan
Write-Host ""

wsl bash $bashScript --iterations $Iterations

# Capture exit code
$exitCode = $LASTEXITCODE

Write-Host ""
if ($exitCode -eq 0) {
    Write-Host "✓ WSL execution successful" -ForegroundColor Green
} else {
    Write-Host "✗ WSL execution failed with exit code $exitCode" -ForegroundColor Red
}

exit $exitCode
