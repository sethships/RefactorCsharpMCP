# Development Environment Setup Script for RefactorCsharpMCP (Windows)
# This script automates the setup of the development environment

$ErrorActionPreference = "Stop"

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "RefactorCsharpMCP Development Setup" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

# Function to print success messages
function Write-Success {
    param([string]$Message)
    Write-Host "✓ $Message" -ForegroundColor Green
}

# Function to print warning messages
function Write-Warning-Custom {
    param([string]$Message)
    Write-Host "⚠ $Message" -ForegroundColor Yellow
}

# Function to print error messages
function Write-Error-Custom {
    param([string]$Message)
    Write-Host "✗ $Message" -ForegroundColor Red
}

# Step 1: Check for .NET SDK
Write-Host "Step 1: Checking for .NET SDK..." -ForegroundColor Cyan

$dotnetInstalled = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnetInstalled) {
    $dotnetVersion = dotnet --version
    $majorVersion = [int]($dotnetVersion.Split('.')[0])

    if ($majorVersion -ge 8) {
        Write-Success ".NET SDK $dotnetVersion is installed"
    } else {
        Write-Warning-Custom ".NET SDK $dotnetVersion found, but version 8.0+ is required"
        Write-Host ""
        Write-Host "Installing .NET SDK 8.0 via winget..."

        if (Get-Command winget -ErrorAction SilentlyContinue) {
            try {
                winget install Microsoft.DotNet.SDK.8
                Write-Success ".NET SDK 8.0 installed via winget"
            } catch {
                Write-Warning-Custom "Failed to install via winget"
                Write-Host "Please install .NET 8 SDK manually from:"
                Write-Host "https://dotnet.microsoft.com/download/dotnet/8.0"
                exit 1
            }
        } else {
            Write-Warning-Custom "winget not found"
            Write-Host "Please install .NET 8 SDK manually from:"
            Write-Host "https://dotnet.microsoft.com/download/dotnet/8.0"
            exit 1
        }
    }
} else {
    Write-Error-Custom ".NET SDK not found"
    Write-Host ""
    Write-Host "Installing .NET SDK 8.0..."

    if (Get-Command winget -ErrorAction SilentlyContinue) {
        try {
            winget install Microsoft.DotNet.SDK.8
            Write-Success ".NET SDK 8.0 installed via winget"

            # Refresh PATH
            $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
        } catch {
            Write-Error-Custom "Failed to install .NET SDK"
            Write-Host "Please install .NET 8 SDK manually from:"
            Write-Host "https://dotnet.microsoft.com/download/dotnet/8.0"
            exit 1
        }
    } else {
        Write-Error-Custom "winget not found"
        Write-Host "Please install .NET 8 SDK manually from:"
        Write-Host "https://dotnet.microsoft.com/download/dotnet/8.0"
        Write-Host ""
        Write-Host "Or install winget from Microsoft Store:"
        Write-Host "https://www.microsoft.com/p/app-installer/9nblggh4nns1"
        exit 1
    }
}
Write-Host ""

# Step 2: Navigate to project root
Write-Host "Step 2: Locating project directory..." -ForegroundColor Cyan
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptPath
Set-Location $projectRoot
Write-Success "Project root: $projectRoot"
Write-Host ""

# Step 3: Restore NuGet packages
Write-Host "Step 3: Restoring NuGet packages..." -ForegroundColor Cyan
try {
    dotnet restore
    Write-Success "NuGet packages restored"
} catch {
    Write-Error-Custom "Failed to restore NuGet packages"
    Write-Host $_.Exception.Message
    exit 1
}
Write-Host ""

# Step 4: Build the solution
Write-Host "Step 4: Building solution..." -ForegroundColor Cyan
try {
    dotnet build
    Write-Success "Solution built successfully"
} catch {
    Write-Error-Custom "Build failed"
    Write-Host $_.Exception.Message
    exit 1
}
Write-Host ""

# Step 5: Run tests (optional)
Write-Host "Step 5: Running tests..." -ForegroundColor Cyan
Write-Host "(This may take 2-5 minutes on first run while downloading reference assemblies)" -ForegroundColor Yellow
try {
    dotnet test --no-build
    Write-Success "All tests passed"
} catch {
    Write-Warning-Custom "Some tests failed - this may be expected on first run"
}
Write-Host ""

# Step 6: Summary
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Setup Complete!" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Open the solution in your IDE:"
Write-Host "     - Visual Studio Code: code $projectRoot"
Write-Host "     - Visual Studio 2022: Open RefactorCsharpMCP.sln"
Write-Host "     - JetBrains Rider: Open RefactorCsharpMCP.sln"
Write-Host ""
Write-Host "  2. Read documentation:"
Write-Host "     - CLAUDE.md - Project guidance for AI-assisted development"
Write-Host "     - README.md - User documentation"
Write-Host "     - docs\PRD-V1-Refactoring-Capabilities.md - Project roadmap"
Write-Host ""
Write-Host "  3. Run the MCP server:"
Write-Host "     cd src\RefactorCsharpMCP.Server"
Write-Host "     dotnet run"
Write-Host ""
Write-Host "  4. Run tests:"
Write-Host "     dotnet test"
Write-Host ""
Write-Success "Happy coding!"
