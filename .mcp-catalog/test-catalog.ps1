# Test Custom MCP Catalog
# Verifies that the custom catalog is properly configured and RefactorCsharpMCP is accessible

Write-Host "Testing Custom Docker MCP Catalog..." -ForegroundColor Cyan

# Check if Docker Desktop is running
try {
    docker ps | Out-Null
    Write-Host "✓ Docker Desktop is running" -ForegroundColor Green
} catch {
    Write-Host "✗ Docker Desktop is not running. Please start Docker Desktop first." -ForegroundColor Red
    exit 1
}

# List catalogs
Write-Host "`n1. Checking available catalogs..." -ForegroundColor Cyan
docker mcp catalog ls

# Show my-mcp-catalog contents
Write-Host "`n2. Showing my-mcp-catalog contents..." -ForegroundColor Cyan
$catalogOutput = docker mcp catalog show my-mcp-catalog 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host $catalogOutput

    # Check if refactor-csharp-mcp is in the catalog
    if ($catalogOutput -like "*refactor-csharp-mcp*") {
        Write-Host "`n✓ RefactorCsharpMCP found in catalog" -ForegroundColor Green
    } else {
        Write-Host "`n✗ RefactorCsharpMCP NOT found in catalog" -ForegroundColor Red
    }
} else {
    Write-Host "✗ Failed to show catalog" -ForegroundColor Red
    exit 1
}

# Check enabled servers
Write-Host "`n3. Checking enabled servers..." -ForegroundColor Cyan
docker mcp server ls

# Test gateway with custom catalog
Write-Host "`n4. Testing gateway with custom catalog..." -ForegroundColor Cyan
Write-Host "This will start the gateway. Press Ctrl+C to stop after verification." -ForegroundColor Yellow

# Start gateway in background (if not already running)
$gatewayProcess = Start-Process -FilePath "docker" `
    -ArgumentList "mcp", "gateway", "run", "--catalog", "my-mcp-catalog" `
    -PassThru `
    -NoNewWindow `
    -RedirectStandardOutput "$PSScriptRoot\gateway-test.log" `
    -RedirectStandardError "$PSScriptRoot\gateway-test-error.log"

Start-Sleep -Seconds 5

if ($gatewayProcess.HasExited) {
    Write-Host "✗ Gateway failed to start" -ForegroundColor Red
    Write-Host "Check gateway-test-error.log for details" -ForegroundColor Yellow
} else {
    Write-Host "✓ Gateway started (PID: $($gatewayProcess.Id))" -ForegroundColor Green
    Write-Host "`nStopping gateway..." -ForegroundColor Yellow
    Stop-Process -Id $gatewayProcess.Id -Force
}

# Clean up log files
if (Test-Path "$PSScriptRoot\gateway-test.log") {
    $logContent = Get-Content "$PSScriptRoot\gateway-test.log"
    if ($logContent) {
        Write-Host "`nGateway output:" -ForegroundColor Cyan
        Write-Host $logContent
    }
    Remove-Item "$PSScriptRoot\gateway-test.log"
}

if (Test-Path "$PSScriptRoot\gateway-test-error.log") {
    $errorContent = Get-Content "$PSScriptRoot\gateway-test-error.log"
    if ($errorContent) {
        Write-Host "`nGateway errors:" -ForegroundColor Red
        Write-Host $errorContent
    }
    Remove-Item "$PSScriptRoot\gateway-test-error.log"
}

Write-Host "`n✓ Testing complete!" -ForegroundColor Green
Write-Host "`nNext step: Update Claude Code MCP configuration to use --catalog my-mcp-catalog" -ForegroundColor Cyan
