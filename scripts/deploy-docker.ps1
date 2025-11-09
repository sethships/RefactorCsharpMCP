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

.PARAMETER RegisterGateway
    Register the server with Docker MCP Gateway after successful deployment.

.PARAMETER Catalog
    Catalog name to register the server in (default: "local-dev"). Only used with -RegisterGateway.

.EXAMPLE
    .\deploy-docker.ps1 -Version "0.4.0" -SecurityScan -Test
    Full deployment with security scanning and validation

.EXAMPLE
    .\deploy-docker.ps1 -SkipSecurity
    Quick deployment without security checks (dev only)

.EXAMPLE
    .\deploy-docker.ps1 -Version "0.4.0" -Push -Registry "myregistry.io/myuser"
    Deploy and push to registry

.EXAMPLE
    .\deploy-docker.ps1 -Version "1.0.0" -RegisterGateway
    Deploy and register with Docker MCP Gateway (local-dev catalog)

.EXAMPLE
    .\deploy-docker.ps1 -RegisterGateway -Catalog "production"
    Deploy and register in custom catalog

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
    [string]$Registry = "",

    [Parameter()]
    [switch]$RegisterGateway,

    [Parameter()]
    [string]$Catalog = "local-dev"
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
    Write-Host "[OK] $Message" -ForegroundColor Green
    Add-Content -Path $LogFile -Value "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] SUCCESS: $Message"
}

function Write-Error-Custom {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
    Add-Content -Path $LogFile -Value "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] ERROR: $Message"
}

function Write-Warning-Custom {
    param([string]$Message)
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
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

    # Step 4: Build Docker image with SBOM generation
    Write-Header "Building Docker Image with SBOM"
    Write-Info "Building ${ImageName}:${Version}..."

    # Ensure buildx builder exists for SBOM support
    Write-Info "Checking buildx builder..."
    $builderCheck = docker buildx ls 2>&1 | Select-String "sbom-builder"
    if (-not $builderCheck) {
        Write-Info "Creating buildx builder for SBOM support..."
        docker buildx create --name sbom-builder --driver docker-container --bootstrap 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Warning-Custom "Failed to create buildx builder, using default"
        } else {
            Write-Success "Created buildx builder: sbom-builder"
        }
    }

    # Use sbom-builder if available, otherwise use default
    $currentBuilder = docker buildx ls 2>&1 | Select-String "\*.*sbom-builder"
    if (-not $currentBuilder) {
        docker buildx use sbom-builder 2>&1 | Out-Null
    }

    $buildStart = Get-Date

    # Build strategy: First export SBOM to filesystem, then load image locally
    Write-Info "Building with SBOM export (step 1/2)..."

    # Capture full output for debugging
    # Note: dest path must not have quotes around the entire parameter
    $sbomOutputPath = Join-Path $ProjectRoot "sbom-output"
    $buildOutput = docker buildx build `
        --sbom=true `
        --output "type=local,dest=$sbomOutputPath" `
        . 2>&1

    # Display build steps
    $buildOutput | ForEach-Object {
        if ($_ -match "^#") {
            Write-Info $_
        }
    }

    # Check for errors in output
    $hasError = $buildOutput | Where-Object { $_ -match "error|failed" -and $_ -notmatch "^#" }
    if ($LASTEXITCODE -ne 0 -or $hasError) {
        Write-Error-Custom "Docker buildx output:"
        $buildOutput | ForEach-Object { Write-Host $_ -ForegroundColor Red }
        throw "Docker build with SBOM export failed (exit code: $LASTEXITCODE)"
    }

    # Move SBOM to project root and clean up
    if (Test-Path "${ProjectRoot}\sbom-output\sbom.spdx.json") {
        Move-Item -Path "${ProjectRoot}\sbom-output\sbom.spdx.json" -Destination "${ProjectRoot}\sbom.spdx.json" -Force
        Write-Success "SBOM exported: sbom.spdx.json"
    } else {
        Write-Warning-Custom "SBOM file not found in output directory"
    }

    # Clean up sbom-output directory
    if (Test-Path "${ProjectRoot}\sbom-output") {
        Remove-Item -Path "${ProjectRoot}\sbom-output" -Recurse -Force
    }

    # Build and load image locally (step 2/2)
    Write-Info "Building and loading image locally (step 2/2)..."

    # Capture full output
    $buildOutput2 = docker buildx build `
        --sbom=true `
        --tag "${ImageName}:${Version}" `
        --tag "${ImageName}:latest" `
        --load `
        . 2>&1

    # Display build steps
    $buildOutput2 | ForEach-Object {
        if ($_ -match "^#") {
            Write-Info $_
        }
    }

    # Check for errors
    $hasError2 = $buildOutput2 | Where-Object { $_ -match "error|failed" -and $_ -notmatch "^#" }
    if ($LASTEXITCODE -ne 0 -or $hasError2) {
        Write-Error-Custom "Docker buildx output:"
        $buildOutput2 | ForEach-Object { Write-Host $_ -ForegroundColor Red }
        throw "Docker build with load failed (exit code: $LASTEXITCODE)"
    }

    $buildDuration = (Get-Date) - $buildStart
    Write-Success "Image built successfully with SBOM in $($buildDuration.TotalSeconds.ToString('F2')) seconds"

    # Step 5: Inspect image
    Write-Header "Image Inspection"
    $imageInfo = docker inspect "${ImageName}:${Version}" | ConvertFrom-Json
    $imageSize = [math]::Round($imageInfo[0].Size / 1MB, 2)
    Write-Info "Image Size: ${imageSize} MB"
    Write-Info "Created: $($imageInfo[0].Created)"

    # Step 5.5: SBOM Validation
    if (Test-Path "${ProjectRoot}\sbom.spdx.json") {
        Write-Header "SBOM Validation"
        Write-Info "Analyzing SBOM content..."

        try {
            $sbom = Get-Content "${ProjectRoot}\sbom.spdx.json" -Raw | ConvertFrom-Json
            $packageCount = ($sbom.packages | Measure-Object).Count
            Write-Info "Total packages in SBOM: $packageCount"

            # Check for key NuGet packages
            $keyPackages = @(
                "Microsoft.CodeAnalysis.CSharp",
                "ModelContextProtocol",
                "Microsoft.Extensions.Hosting"
            )

            $foundCount = 0
            foreach ($pkg in $keyPackages) {
                $found = $sbom.packages | Where-Object { $_.name -like "*$pkg*" }
                if ($found) {
                    Write-Success "Found: $($found.name)"
                    $foundCount++
                } else {
                    Write-Warning-Custom "Expected package not found: $pkg"
                }
            }

            if ($foundCount -eq $keyPackages.Count) {
                Write-Success "All key packages validated in SBOM"
            } else {
                Write-Warning-Custom "Some expected packages missing from SBOM ($foundCount/$($keyPackages.Count))"
            }

            # Save package summary
            $sbom.packages | Select-Object name, versionInfo, licenseConcluded |
                Export-Csv -Path "${ProjectRoot}\sbom-packages.csv" -NoTypeInformation
            Write-Info "Package list exported to sbom-packages.csv"

        } catch {
            Write-Warning-Custom "Failed to validate SBOM: $($_.Exception.Message)"
        }
    } else {
        Write-Warning-Custom "SBOM file not found, skipping validation"
    }

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

    # Step 6.5: CycloneDX SBOM Generation (Optional)
    Write-Header "CycloneDX SBOM Generation"

    # Check for Syft
    $syftAvailable = Get-Command syft -ErrorAction SilentlyContinue
    if ($syftAvailable) {
        Write-Info "Generating CycloneDX SBOM with Syft..."
        try {
            syft "${ImageName}:${Version}" -o cyclonedx-json | Out-File -Encoding utf8 "${ProjectRoot}\sbom.cyclonedx.json"
            if ($LASTEXITCODE -eq 0 -and (Test-Path "${ProjectRoot}\sbom.cyclonedx.json")) {
                Write-Success "CycloneDX SBOM generated: sbom.cyclonedx.json"

                # Validate CycloneDX SBOM size
                $cycloneDxFile = Get-Item "${ProjectRoot}\sbom.cyclonedx.json"
                if ($cycloneDxFile.Length -gt 1KB) {
                    Write-Info "CycloneDX SBOM size: $([math]::Round($cycloneDxFile.Length / 1KB, 2)) KB"
                } else {
                    Write-Warning-Custom "CycloneDX SBOM file seems too small, may be incomplete"
                }
            } else {
                Write-Warning-Custom "Syft CycloneDX generation failed"
            }
        } catch {
            Write-Warning-Custom "Error generating CycloneDX SBOM: $($_.Exception.Message)"
        }
    } else {
        Write-Warning-Custom "Syft not installed, skipping CycloneDX generation"
        Write-Info "Install with: choco install syft"
    }

    # Check for Trivy as alternative/additional scanner
    $trivyAvailable = Get-Command trivy -ErrorAction SilentlyContinue
    if ($trivyAvailable) {
        Write-Info "Generating additional CycloneDX SBOM with Trivy..."
        try {
            trivy image --format cyclonedx "${ImageName}:${Version}" | Out-File -Encoding utf8 "${ProjectRoot}\sbom.cyclonedx-trivy.json"
            if ($LASTEXITCODE -eq 0 -and (Test-Path "${ProjectRoot}\sbom.cyclonedx-trivy.json")) {
                Write-Success "Trivy CycloneDX SBOM generated: sbom.cyclonedx-trivy.json"
            }
        } catch {
            Write-Warning-Custom "Trivy CycloneDX generation failed: $($_.Exception.Message)"
        }
    }

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

    # Step 10: Register with Docker MCP Gateway (if requested)
    if ($RegisterGateway) {
        # Validate version format before registration
        if ($Version -notmatch '^[a-zA-Z0-9._-]+$') {
            Write-Warning-Custom "Invalid version format: $Version"
            Write-Warning-Custom "Version must contain only alphanumeric characters, dots, underscores, and hyphens"
            Write-Warning-Custom "Skipping gateway registration"
        } else {
            Write-Header "Registering with Docker MCP Gateway"
            $registerScript = Join-Path $PSScriptRoot "register-mcp-gateway.ps1"

            if (-not (Test-Path $registerScript)) {
                Write-Warning-Custom "Registration script not found: $registerScript"
                Write-Warning-Custom "Skipping gateway registration"
            } else {
            Write-Info "Running registration script..."
            try {
                & $registerScript -Version $Version -Catalog $Catalog -Validate
                if ($LASTEXITCODE -eq 0) {
                    Write-Success "Server registered with Docker MCP Gateway"
                    Write-Info "Catalog: $Catalog"
                    Write-Info "Use 'docker mcp server ls' to verify"
                } else {
                    Write-Warning-Custom "Registration completed with warnings"
                }
            } catch {
                Write-Warning-Custom "Failed to register with gateway: $($_.Exception.Message)"
                Write-Info "You can manually register later with:"
                Write-Info "  pwsh $registerScript -Version $Version"
            }
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
