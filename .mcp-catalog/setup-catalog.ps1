# Setup Custom MCP Catalog with RefactorCsharpMCP
# This script creates a custom Docker MCP catalog and adds RefactorCsharpMCP to it

Write-Host "Setting up custom Docker MCP catalog..." -ForegroundColor Cyan

# Check if Docker Desktop is running
try {
    docker ps | Out-Null
    Write-Host "✓ Docker Desktop is running" -ForegroundColor Green
} catch {
    Write-Host "✗ Docker Desktop is not running. Please start Docker Desktop first." -ForegroundColor Red
    exit 1
}

# Check if the Docker image exists
$imageExists = docker images refactor-csharp-mcp:latest --format "{{.Repository}}" | Select-String "refactor-csharp-mcp"
if (-not $imageExists) {
    Write-Host "✗ Docker image 'refactor-csharp-mcp:latest' not found." -ForegroundColor Red
    Write-Host "Building the image..." -ForegroundColor Yellow

    Push-Location $PSScriptRoot\..
    docker build -t refactor-csharp-mcp:latest .
    Pop-Location

    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ Failed to build Docker image" -ForegroundColor Red
        exit 1
    }
    Write-Host "✓ Docker image built successfully" -ForegroundColor Green
} else {
    Write-Host "✓ Docker image exists" -ForegroundColor Green
}

# Fork the official Docker MCP catalog
Write-Host "`nForking official Docker MCP catalog..." -ForegroundColor Cyan
docker mcp catalog fork docker-mcp my-mcp-catalog 2>&1 | Out-Null

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Catalog forked successfully" -ForegroundColor Green
} else {
    # Catalog might already exist, check if it does
    $catalogExists = docker mcp catalog ls | Select-String "my-mcp-catalog"
    if ($catalogExists) {
        Write-Host "✓ Catalog already exists" -ForegroundColor Yellow
    } else {
        Write-Host "✗ Failed to fork catalog" -ForegroundColor Red
        exit 1
    }
}

# Export the catalog to YAML
Write-Host "`nExporting catalog to YAML..." -ForegroundColor Cyan
docker mcp catalog show my-mcp-catalog --format yaml > "$PSScriptRoot\exported-catalog.yaml"

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Catalog exported to exported-catalog.yaml" -ForegroundColor Green
} else {
    Write-Host "✗ Failed to export catalog" -ForegroundColor Red
    exit 1
}

# Import our custom catalog with RefactorCsharpMCP
Write-Host "`nImporting custom catalog with RefactorCsharpMCP..." -ForegroundColor Cyan
docker mcp catalog import "$PSScriptRoot\catalog.yaml"

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Custom catalog imported successfully" -ForegroundColor Green
} else {
    Write-Host "⚠ Import may have failed - continuing..." -ForegroundColor Yellow
}

# List catalogs
Write-Host "`nAvailable catalogs:" -ForegroundColor Cyan
docker mcp catalog ls

# Show servers in the catalog
Write-Host "`nServers in my-mcp-catalog:" -ForegroundColor Cyan
docker mcp catalog show my-mcp-catalog

Write-Host "`n✓ Setup complete!" -ForegroundColor Green
Write-Host "`nNext steps:" -ForegroundColor Cyan
Write-Host "1. Update your MCP configuration to use the custom catalog"
Write-Host "2. Restart Claude Code"
Write-Host "3. Test the RefactorCsharpMCP tools"
Write-Host "`nTo run the gateway with the custom catalog:"
Write-Host "  docker mcp gateway run --catalog my-mcp-catalog" -ForegroundColor Yellow
