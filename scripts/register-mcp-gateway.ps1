<#
.SYNOPSIS
    Register RefactorCsharpMCP with Docker MCP Gateway
.DESCRIPTION
    This script registers the MCP server with Docker Desktop's MCP Toolkit.
    Requires Docker Desktop with MCP Gateway support.
.PARAMETER Version
    Version tag for the Docker image (default: "latest")
.PARAMETER Catalog
    Catalog name to add the server to (default: "local-dev")
.PARAMETER Validate
    Validate gateway support before registration
.EXAMPLE
    .\register-mcp-gateway.ps1
    Register with default settings
.EXAMPLE
    .\register-mcp-gateway.ps1 -Version "1.0.0" -Validate
    Register specific version with validation
#>
[CmdletBinding()]
param(
    [string]$Version = "latest",
    [string]$Catalog = "local-dev",
    [switch]$Validate
)

$ErrorActionPreference = "Stop"

# Get script directory and project root
$ScriptDir = Split-Path -Parent $PSCommandPath
$ProjectRoot = Split-Path -Parent $ScriptDir
$LogFile = Join-Path $ProjectRoot "registration.log"

# Logging function
function Write-Log {
    param([string]$Message)
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    Add-Content -Path $LogFile -Value "[$timestamp] $Message"
}

# Check execution policy
$executionPolicy = Get-ExecutionPolicy
if ($executionPolicy -in @('Restricted', 'AllSigned')) {
    Write-Warning "Current execution policy: $executionPolicy"
    Write-Warning "Script may not run. Consider: Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser"
}

Write-Host "RefactorCsharpMCP - Docker MCP Gateway Registration" -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host ""

Write-Log "==== Gateway Registration Started ===="
Write-Log "Version: $Version"
Write-Log "Catalog: $Catalog"
Write-Log "Validate: $Validate"

# Step 1: Validate Docker MCP Gateway support
if ($Validate) {
    Write-Host "Validating Docker Desktop MCP Gateway..." -ForegroundColor Yellow
    Write-Log "Validating Docker MCP Gateway support"

    $dockerVersion = docker --version
    Write-Host "[OK] Docker installed: $dockerVersion" -ForegroundColor Green
    Write-Log "SUCCESS: Docker installed - $dockerVersion"

    # Check for MCP Gateway support
    try {
        $mcpSupport = docker mcp version 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Docker MCP Gateway not available"
        }
        Write-Host "[OK] Docker MCP Gateway detected" -ForegroundColor Green
        Write-Log "SUCCESS: Docker MCP Gateway detected"
    } catch {
        $dockerServerVersion = docker version --format '{{.Server.Version}}' 2>$null
        if (-not $dockerServerVersion) {
            $dockerServerVersion = "unknown"
        }
        Write-Host "[ERROR] Docker MCP Gateway not available" -ForegroundColor Red
        Write-Host "Current Docker version: $dockerServerVersion" -ForegroundColor Yellow
        Write-Host "MCP Gateway requires Docker Desktop 28.5.1+ or equivalent" -ForegroundColor Yellow
        Write-Host "Please update Docker Desktop from: https://www.docker.com/products/docker-desktop" -ForegroundColor Yellow
        Write-Log "ERROR: Docker MCP Gateway not available (Docker version: $dockerServerVersion)"
        exit 1
    }
}

# Step 2: Verify image exists
Write-Host "Verifying Docker image..." -ForegroundColor Yellow
Write-Log "Verifying Docker image: refactor-csharp-mcp:$Version"
$imageExists = docker images -q "refactor-csharp-mcp:$Version" 2>$null
if (-not $imageExists) {
    Write-Host "[ERROR] Image refactor-csharp-mcp:$Version not found" -ForegroundColor Red
    Write-Host "Build the image first:" -ForegroundColor Yellow
    Write-Host "  docker build -t refactor-csharp-mcp:$Version ." -ForegroundColor White
    Write-Host "  or" -ForegroundColor White
    Write-Host "  .\scripts\deploy-docker.ps1 -Version $Version" -ForegroundColor White
    Write-Log "ERROR: Image not found: refactor-csharp-mcp:$Version"
    exit 1
}
Write-Host "[OK] Image found: refactor-csharp-mcp:$Version" -ForegroundColor Green
Write-Log "SUCCESS: Image found: refactor-csharp-mcp:$Version"

# Step 3: Check if docker-mcp.yaml exists
Write-Host ""
Write-Host "Checking catalog definition..." -ForegroundColor Yellow
Write-Log "Checking catalog definition file"
$catalogFile = Join-Path $ProjectRoot "docker-mcp.yaml"
if (-not (Test-Path $catalogFile)) {
    Write-Host "[ERROR] $catalogFile not found" -ForegroundColor Red
    Write-Host "Expected location: $catalogFile" -ForegroundColor Yellow
    Write-Host "Please ensure docker-mcp.yaml exists in the project root" -ForegroundColor Yellow
    Write-Log "ERROR: Catalog file not found: $catalogFile"
    exit 1
}
Write-Host "[OK] Catalog definition found" -ForegroundColor Green
Write-Log "SUCCESS: Catalog definition found at $catalogFile"

# Step 4: Initialize catalog if needed
Write-Host ""
Write-Host "Checking catalog system..." -ForegroundColor Yellow
Write-Log "Checking if catalog system is initialized"
try {
    $null = docker mcp catalog ls 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "[OK] Catalog system already initialized" -ForegroundColor Green
        Write-Log "INFO: Catalog system already initialized"
    } else {
        Write-Host "[INFO] Initializing catalog system..." -ForegroundColor Yellow
        Write-Log "INFO: Initializing catalog system"
        $null = docker mcp catalog init 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "[OK] Catalog system initialized" -ForegroundColor Green
            Write-Log "SUCCESS: Catalog system initialized"
        } else {
            Write-Warning "[WARN] Catalog initialization failed, continuing anyway"
            Write-Log "WARNING: Catalog initialization failed"
        }
    }
} catch {
    Write-Warning "[WARN] Could not check catalog system"
    Write-Log "WARNING: Could not check catalog system - $_"
}

# Step 5: Add server to catalog
Write-Host ""
Write-Host "Registering server in catalog '$Catalog'..." -ForegroundColor Yellow
Write-Log "Adding server to catalog: $Catalog"
try {
    $output = docker mcp catalog add $Catalog refactor-csharp-mcp $catalogFile --force 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Docker command output:" -ForegroundColor Red
        Write-Host $output -ForegroundColor Red
        Write-Log "ERROR: Failed to add server to catalog. Exit code: $LASTEXITCODE"
        Write-Log "ERROR: $output"
        throw "Failed to add server to catalog"
    }
    Write-Host "[OK] Server added to catalog" -ForegroundColor Green
    Write-Log "SUCCESS: Server added to catalog $Catalog"
} catch {
    Write-Host "[ERROR] Failed to register server in catalog" -ForegroundColor Red
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}

# Step 6: Enable the server
Write-Host ""
Write-Host "Enabling MCP server..." -ForegroundColor Yellow
Write-Log "Enabling MCP server: refactor-csharp-mcp"
try {
    $output = docker mcp server enable refactor-csharp-mcp 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Docker command output:" -ForegroundColor Red
        Write-Host $output -ForegroundColor Red
        Write-Log "ERROR: Failed to enable server. Exit code: $LASTEXITCODE"
        Write-Log "ERROR: $output"
        throw "Failed to enable server"
    }
    Write-Host "[OK] Server enabled" -ForegroundColor Green
    Write-Log "SUCCESS: Server enabled"
} catch {
    Write-Host "[ERROR] Failed to enable server" -ForegroundColor Red
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}

# Step 7: Verify registration
Write-Host ""
Write-Host "Verifying registration..." -ForegroundColor Yellow
Write-Log "Verifying server registration"
try {
    docker mcp server inspect refactor-csharp-mcp
    Write-Host ""
    Write-Host "[OK] Registration verified" -ForegroundColor Green
    Write-Log "SUCCESS: Registration verified"
} catch {
    Write-Host "[WARN] Could not verify server registration" -ForegroundColor Yellow
    Write-Log "WARNING: Could not verify registration"
}

# Summary
Write-Host ""
Write-Log "==== Gateway Registration Complete ===="
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host "Registration complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. View catalog:  docker mcp catalog show $Catalog" -ForegroundColor White
Write-Host "  2. List servers:  docker mcp server ls" -ForegroundColor White
Write-Host "  3. Start gateway: docker mcp gateway run" -ForegroundColor White
Write-Host ""
Write-Host "Configure Claude Desktop:" -ForegroundColor Cyan
Write-Host '  {' -ForegroundColor White
Write-Host '    "mcpServers": {' -ForegroundColor White
Write-Host '      "refactor-csharp-mcp": {' -ForegroundColor White
Write-Host '        "command": "docker",' -ForegroundColor White
Write-Host '        "args": ["mcp", "gateway", "run"]' -ForegroundColor White
Write-Host '      }' -ForegroundColor White
Write-Host '    }' -ForegroundColor White
Write-Host '  }' -ForegroundColor White
