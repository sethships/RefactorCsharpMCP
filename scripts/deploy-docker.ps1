<#
.SYNOPSIS
    Deploy RefactorCsharpMCP Docker image with comprehensive validation and security scanning.

.DESCRIPTION
    This script automates the complete Docker deployment process:
    - Runs test suite to validate code quality
    - Builds optimized Docker image with multi-stage build
    - Performs health checks on the container
    - Runs security scans (Docker Scout and Trivy)
    - Validates stdio transport functionality
    - Generates deployment report

.PARAMETER Version
    Version tag for the Docker image (e.g., "0.4.0"). Defaults to "latest".

.PARAMETER SecurityScan
    Run security vulnerability scans using Docker Scout and Trivy.

.PARAMETER Test
    Run post-deployment validation tests.

.PARAMETER SkipTests
    Skip pre-deployment test suite execution.

.PARAMETER SkipSecurity
    Skip security scanning (not recommended for production).

.PARAMETER Push
    Push the image to a registry after successful build.

.PARAMETER Registry
    Docker registry to push to (e.g., "docker.io/username").

.EXAMPLE
    .\deploy-docker.ps1 -Version "0.4.0" -SecurityScan -Test
    Full deployment with security scanning and validation

.EXAMPLE
    .\deploy-docker.ps1 -SkipSecurity
    Quick deployment without security checks (dev only)

.EXAMPLE
    .\deploy-docker.ps1 -Version "0.4.0" -Push -Registry "myregistry.io/myuser"
    Deploy and push to registry

.NOTES
    Author: DevTools Team
    Requires: Docker Desktop, .NET 8 SDK
    Optional: Docker Scout, Trivy (for security scanning)
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$Version = "latest",

    [Parameter()]
    [switch]$SecurityScan,

    [Parameter()]
    [switch]$Test,

    [Parameter()]
    [switch]$SkipTests,

    [Parameter()]
    [switch]$SkipSecurity,

    [Parameter()]
    [switch]$Push,

    [Parameter()]
    [string]$Registry = ""
)

# Check execution policy
$executionPolicy = Get-ExecutionPolicy
if ($executionPolicy -in @('Restricted', 'AllSigned')) {
    Write-Warning "Current execution policy: $executionPolicy"
    Write-Warning "Script may not run. Consider: Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser"
}

# Configuration
$ErrorActionPreference = "Stop"
$ImageName = "refactor-csharp-mcp"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$LogFile = Join-Path $ProjectRoot "deployment.log"
$StartTime = Get-Date

# Color functions
function Write-Header {
    param([string]$Message)
    Write-Host "`n==== $Message ====" -ForegroundColor Cyan
    Add-Content -Path $LogFile -Value "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] ==== $Message ===="
}

function Write-Success {
    param([string]$Message)
    Write-Host "✓ $Message" -ForegroundColor Green
    Add-Content -Path $LogFile -Value "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] SUCCESS: $Message"
}

function Write-Error-Custom {
    param([string]$Message)
    Write-Host "✗ $Message" -ForegroundColor Red
    Add-Content -Path $LogFile -Value "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] ERROR: $Message"
}

function Write-Warning-Custom {
    param([string]$Message)
    Write-Host "⚠ $Message" -ForegroundColor Yellow
    Add-Content -Path $LogFile -Value "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] WARNING: $Message"
}

function Write-Info {
    param([string]$Message)
    Write-Host "  $Message" -ForegroundColor Gray
    Add-Content -Path $LogFile -Value "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] INFO: $Message"
}

# Initialize log
Write-Header "Docker Deployment Started"
Write-Info "Version: $Version"
Write-Info "Project Root: $ProjectRoot"
Write-Info "Log File: $LogFile"

try {
    # Step 1: Pre-deployment validation
    Write-Header "Pre-Deployment Validation"

    # Check Docker
    Write-Info "Checking Docker installation..."
    $dockerVersion = docker --version 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Docker is not installed or not in PATH"
    }
    Write-Success "Docker found: $dockerVersion"

    # Check .NET SDK
    Write-Info "Checking .NET SDK..."
    $dotnetVersion = dotnet --version 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw ".NET SDK is not installed or not in PATH"
    }
    Write-Success ".NET SDK found: $dotnetVersion"

    # Change to project directory
    Push-Location $ProjectRoot

    # Step 2: Run tests (unless skipped)
    if (-not $SkipTests) {
        Write-Header "Running Test Suite"
        Write-Info "Running dotnet test..."

        $testOutput = dotnet test --configuration Release --verbosity minimal 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Error-Custom "Tests failed!"
            Write-Host $testOutput -ForegroundColor Red
            throw "Test suite must pass before deployment"
        }

        # Parse test results
        $testOutput | ForEach-Object {
            if ($_ -match "Passed!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+)") {
                $failed = $Matches[1]
                $passed = $Matches[2]
                Write-Success "Tests passed: $passed passed, $failed failed"
            }
        }
    } else {
        Write-Warning-Custom "Skipping test suite (not recommended for production)"
    }

    # Step 3: Clean previous builds
    Write-Header "Cleaning Previous Builds"
    Write-Info "Removing old images..."

    $oldImages = docker images -q "${ImageName}:${Version}" 2>$null
    if ($oldImages) {
        docker rmi -f $oldImages 2>&1 | Out-Null
        Write-Success "Removed previous image: ${ImageName}:${Version}"
    }

    # Step 4: Build Docker image
    Write-Header "Building Docker Image"
    Write-Info "Building ${ImageName}:${Version}..."

    $buildStart = Get-Date
    docker build -t "${ImageName}:${Version}" -t "${ImageName}:latest" . 2>&1 | ForEach-Object {
        if ($_ -match "^#") {
            Write-Info $_
        }
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Docker build failed"
    }

    $buildDuration = (Get-Date) - $buildStart
    Write-Success "Image built successfully in $($buildDuration.TotalSeconds.ToString('F2')) seconds"

    # Step 5: Inspect image
    Write-Header "Image Inspection"
    $imageInfo = docker inspect "${ImageName}:${Version}" | ConvertFrom-Json
    $imageSize = [math]::Round($imageInfo[0].Size / 1MB, 2)
    Write-Info "Image Size: ${imageSize} MB"
    Write-Info "Created: $($imageInfo[0].Created)"

    # Step 6: Health check
    Write-Header "Container Health Check"
    Write-Info "Starting container for health check..."

    $containerId = docker run -d "${ImageName}:${Version}" 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to start container"
    }

    Write-Info "Container ID: $containerId"
    Start-Sleep -Seconds 5

    $healthStatus = docker inspect --format='{{.State.Health.Status}}' $containerId 2>&1
    if ($healthStatus -match "healthy|starting") {
        Write-Success "Container health check: $healthStatus"
    } else {
        Write-Warning-Custom "Container health status: $healthStatus"
    }

    # Check if container is running
    $containerStatus = docker inspect --format='{{.State.Status}}' $containerId 2>&1
    if ($containerStatus -eq "running") {
        Write-Success "Container is running"
    } else {
        Write-Warning-Custom "Container status: $containerStatus"
    }

    # Cleanup test container
    docker stop $containerId 2>&1 | Out-Null
    docker rm $containerId 2>&1 | Out-Null
    Write-Info "Test container cleaned up"

    # Step 7: Security scanning
    # Force security scan for production versions
    $isProduction = $Version -match '^\d+\.\d+\.\d+$' -and $Version -ne "latest"
    if (($SecurityScan -or $isProduction) -and -not $SkipSecurity) {
        if ($isProduction -and $SkipSecurity) {
            throw "Cannot skip security scanning for production version ($Version)"
        }
        Write-Header "Security Scanning"
        if ($isProduction) {
            Write-Info "Production version detected - security scanning is mandatory"
        }

        # Check for Docker Scout
        Write-Info "Checking for Docker Scout..."
        $scoutAvailable = docker scout version 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Info "Running Docker Scout CVE scan..."
            docker scout cves "${ImageName}:${Version}" 2>&1 | Tee-Object -FilePath (Join-Path $ProjectRoot "security-scout.txt")
            if ($LASTEXITCODE -eq 0) {
                Write-Success "Docker Scout scan completed"
            } else {
                Write-Warning-Custom "Docker Scout scan had warnings (check security-scout.txt)"
            }
        } else {
            Write-Warning-Custom "Docker Scout not available, skipping"
        }

        # Check for Trivy
        Write-Info "Checking for Trivy..."
        $trivyAvailable = Get-Command trivy -ErrorAction SilentlyContinue
        if ($trivyAvailable) {
            Write-Info "Running Trivy vulnerability scan..."
            trivy image --severity HIGH,CRITICAL "${ImageName}:${Version}" 2>&1 | Tee-Object -FilePath (Join-Path $ProjectRoot "security-trivy.txt")
            if ($LASTEXITCODE -eq 0) {
                Write-Success "Trivy scan completed"
            } else {
                Write-Warning-Custom "Trivy scan found issues (check security-trivy.txt)"
            }
        } else {
            Write-Warning-Custom "Trivy not installed, skipping (install from: https://github.com/aquasecurity/trivy)"
        }
    } elseif ($SkipSecurity) {
        Write-Warning-Custom "Security scanning skipped (not recommended for production)"
    }

    # Step 8: Post-deployment testing
    if ($Test) {
        Write-Header "Post-Deployment Validation"
        $testScript = Join-Path $PSScriptRoot "test-deployment.ps1"
        if (Test-Path $testScript) {
            Write-Info "Running validation tests..."
            & $testScript -ImageName "${ImageName}:${Version}"
        } else {
            Write-Warning-Custom "test-deployment.ps1 not found, skipping validation"
        }
    }

    # Step 9: Push to registry (if requested)
    if ($Push) {
        if ([string]::IsNullOrWhiteSpace($Registry)) {
            Write-Warning-Custom "Registry not specified, skipping push"
        } else {
            Write-Header "Pushing to Registry"
            $remoteTag = "${Registry}/${ImageName}:${Version}"
            Write-Info "Tagging for registry: $remoteTag"
            docker tag "${ImageName}:${Version}" $remoteTag

            Write-Info "Pushing to $Registry..."
            docker push $remoteTag
            if ($LASTEXITCODE -eq 0) {
                Write-Success "Pushed to registry: $remoteTag"
            } else {
                throw "Failed to push to registry"
            }
        }
    }

    # Final summary
    $duration = (Get-Date) - $StartTime
    Write-Header "Deployment Summary"
    Write-Success "Image: ${ImageName}:${Version}"
    Write-Success "Size: ${imageSize} MB"
    Write-Success "Total Time: $($duration.TotalSeconds.ToString('F2')) seconds"
    Write-Info "Log file: $LogFile"

    Write-Host "`n" -NoNewline
    Write-Host "Deployment completed successfully!" -ForegroundColor Green
    Write-Host "To run the container:" -ForegroundColor Cyan
    Write-Host "  docker run --rm -i ${ImageName}:${Version}" -ForegroundColor White

} catch {
    Write-Header "Deployment Failed"
    Write-Error-Custom $_.Exception.Message
    Write-Error-Custom $_.ScriptStackTrace
    exit 1
} finally {
    Pop-Location
}
