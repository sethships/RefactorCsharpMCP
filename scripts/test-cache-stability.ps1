#
# Cache Stability Test Script (PowerShell)
#
# Runs cache-related tests multiple times to verify stability and detect
# intermittent failures due to concurrency issues.
#
# Usage: .\test-cache-stability.ps1 [-Iterations N]
#
# Default: 10 iterations
#

param(
    [int]$Iterations = 10
)

$ErrorActionPreference = "Stop"

# Test filter for cache-related tests (all classes with [Collection("CacheTests")])
$Filter = "FullyQualifiedName~ReferenceAssemblyCache|FullyQualifiedName~ReferenceAssemblyResolver|FullyQualifiedName~ReferenceAssemblyErrorScenario|FullyQualifiedName~FrameworkTestFixture|FullyQualifiedName~TupleReturnConverter|FullyQualifiedName~NullableReferenceTypeStripper"

Write-Host ""
Write-Host "==== Cache Stability Test - $Iterations Iterations ====" -ForegroundColor Cyan
Write-Host ""
Write-Host "Start time: $(Get-Date)"
Write-Host "Iterations: $Iterations"
Write-Host "Test filter: Cache-related tests"
Write-Host ""

# Statistics tracking
$PassCount = 0
$FailCount = 0
$Durations = @()
$TotalDuration = 0

# Run iterations
for ($i = 1; $i -le $Iterations; $i++) {
    Write-Host "Run $i/$Iterations... " -NoNewline

    $Start = Get-Date

    # Run tests and capture output
    $LogFile = "$env:TEMP\cache-stability-run-$i.txt"
    $TestResult = dotnet test --no-build --verbosity quiet --filter $Filter *> $LogFile
    $Success = $LASTEXITCODE -eq 0

    $End = Get-Date
    $Duration = [int](($End - $Start).TotalSeconds)
    $Durations += $Duration
    $TotalDuration += $Duration

    if ($Success) {
        Write-Host "✓ PASSED" -ForegroundColor Green -NoNewline
        Write-Host " (${Duration}s)"
        $PassCount++
    } else {
        Write-Host "✗ FAILED" -ForegroundColor Red -NoNewline
        Write-Host " (${Duration}s)"
        $FailCount++
        Write-Host "  See $LogFile for details" -ForegroundColor Gray
    }
}

Write-Host ""

# Calculate statistics
$AvgDuration = [int]($TotalDuration / $Iterations)
$MinDuration = ($Durations | Measure-Object -Minimum).Minimum
$MaxDuration = ($Durations | Measure-Object -Maximum).Maximum

# Calculate standard deviation
$SumSquaredDiff = 0
foreach ($duration in $Durations) {
    $Diff = $duration - $AvgDuration
    $SumSquaredDiff += $Diff * $Diff
}
$Variance = $SumSquaredDiff / $Iterations
$StdDev = [int][Math]::Sqrt($Variance)

# Calculate pass rate
$PassRate = [int](($PassCount * 100) / $Iterations)

# Display summary
Write-Host "==== Summary ====" -ForegroundColor Cyan
Write-Host ""
Write-Host "Total Runs:          $Iterations"
if ($PassCount -gt 0) {
    Write-Host "Passed:              " -NoNewline
    Write-Host "$PassCount" -ForegroundColor Green
}
if ($FailCount -gt 0) {
    Write-Host "Failed:              " -NoNewline
    Write-Host "$FailCount" -ForegroundColor Red
} else {
    Write-Host "Failed:              $FailCount"
}
Write-Host "Pass Rate:           " -NoNewline
if ($PassRate -eq 100) {
    Write-Host "${PassRate}%" -ForegroundColor Green
} else {
    Write-Host "${PassRate}%" -ForegroundColor Red
}
Write-Host ""
Write-Host "Average Time:        ${AvgDuration}s"
Write-Host "Min Time:            ${MinDuration}s"
Write-Host "Max Time:            ${MaxDuration}s"
Write-Host "Std Deviation:       ${StdDev}s"
Write-Host "Total Time:          ${TotalDuration}s"
Write-Host ""

# Final verdict
if ($FailCount -eq 0) {
    Write-Host "✓ All $Iterations runs successful - cache concurrency stable! ✅" -ForegroundColor Green
    Write-Host ""
    exit 0
} else {
    Write-Host "✗ Cache stability issues detected - $FailCount/$Iterations runs failed" -ForegroundColor Red
    Write-Host "⚠ Review logs in $env:TEMP\cache-stability-run-*.txt for details" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}
