<#
.SYNOPSIS
    Deploy RefactorCsharpMCP Docker image with comprehensive validation and security scanning.

.DESCRIPTION
    This script automates the complete Docker deployment process:
    - Runs test suite to validate code quality
    - Builds Docker image (fast local build or full multi-stage with SBOM)
    - Performs health checks on the container
    - Runs security scans (Docker Scout and Trivy)
    - Validates stdio transport functionality
    - Generates deployment report

    Build Strategies:
    - Dev/SkipSBOM: Two-step fast build (dotnet publish + runtime-only image, ~10-15 seconds)
    - Production: Full multi-stage build with SBOM generation (~2-5 minutes)

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

.PARAMETER SkipSBOM
    Use fast two-step build (local publish + runtime-only image) instead of multi-stage build with SBOM.
    Build time: ~10-15 seconds vs ~2-5 minutes. Not recommended for production.

.PARAMETER Dev
    Development mode: automatically enables -SkipSBOM, -SkipSecurity, and -Verbose for fastest local builds.

.PARAMETER Clean
    Clean up all existing containers and images for this project before deployment.

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
    .\deploy-docker.ps1 -Dev
    Development mode - equivalent to -SkipSBOM -SkipSecurity -Verbose
    Uses fast two-step build (~10-15 seconds) for rapid local iteration

.EXAMPLE
    .\deploy-docker.ps1 -Dev -Clean
    Development mode with cleanup of all existing containers and images

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
    [switch]$SkipSBOM,

    [Parameter()]
    [switch]$Dev,

    [Parameter()]
    [switch]$Clean,

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

# Apply -Dev mode settings
if ($Dev) {
    Write-Host "Development mode enabled: -SkipSBOM -SkipSecurity -Verbose" -ForegroundColor Cyan
    $SkipSBOM = $true
    $SkipSecurity = $true
    $VerbosePreference = 'Continue'
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

    # Step 1.5: Clean up existing containers and images (if requested)
    if ($Clean) {
        Write-Header "Cleaning Up Existing Containers and Images"

        # Find all containers (running and stopped) - search by command pattern
        # This catches containers even if the image was rebuilt and only shows as image ID
        Write-Info "Finding all RefactorCsharpMCP containers..."
        $allContainers = docker ps -a --format "{{.ID}} {{.Status}} {{.Command}}" 2>&1 | Where-Object { $_ -match "RefactorCsha" }

        if ($allContainers -and $allContainers.Count -gt 0) {
            $containerIds = @()
            $runningCount = 0
            $stoppedCount = 0

            foreach ($line in $allContainers) {
                # Format: ID STATUS COMMAND
                if ($line -match '^([a-f0-9]+)\s+(.*?)\s+"') {
                    $containerId = $Matches[1]
                    $status = $Matches[2]
                    $containerIds += $containerId

                    if ($status -match 'Up') {
                        $runningCount++
                    } else {
                        $stoppedCount++
                    }
                }
            }

            if ($containerIds.Count -gt 0) {
                Write-Info "Found $($containerIds.Count) container(s): $runningCount running, $stoppedCount stopped"

                # Stop running containers with timeout and fallback to kill
                if ($runningCount -gt 0) {
                    Write-Info "Stopping running containers (10 second timeout)..."
                    $stopArgs = @('stop', '--time', '10') + $containerIds

                    try {
                        $stopJob = Start-Job -ScriptBlock { param($args) & docker $args 2>&1 } -ArgumentList (,$stopArgs)
                        $stopJob | Wait-Job -Timeout 15 | Out-Null

                        if ($stopJob.State -eq 'Running') {
                            Write-Warning-Custom "Stop command timed out, forcing kill..."
                            Stop-Job $stopJob
                            Remove-Job $stopJob

                            # Force kill
                            $killArgs = @('kill') + $containerIds
                            & docker $killArgs 2>&1 | Out-Null
                            Write-Success "Force killed $runningCount container(s)"
                        } else {
                            Receive-Job $stopJob | Out-Null
                            Remove-Job $stopJob
                            Write-Success "Stopped $runningCount running container(s)"
                        }
                    } catch {
                        Write-Warning-Custom "Failed to stop containers: $($_.Exception.Message)"
                        Write-Info "Attempting force kill..."
                        $killArgs = @('kill') + $containerIds
                        & docker $killArgs 2>&1 | Out-Null
                        Write-Success "Force killed containers"
                    }
                }

                # Remove all containers
                Write-Info "Removing containers..."
                try {
                    $rmArgs = @('rm', '-f') + $containerIds
                    & docker $rmArgs 2>&1 | Out-Null
                    Write-Success "Removed $($containerIds.Count) container(s)"
                } catch {
                    Write-Error-Custom "Failed to remove containers: $($_.Exception.Message)"
                    Write-Warning-Custom "Some containers may require manual cleanup"
                    throw "Container removal failed - please check Docker Desktop and try again"
                }
            } else {
                Write-Info "No containers found using ${ImageName}"
            }
        } else {
            Write-Info "No containers found using ${ImageName}"
        }

        # Remove all images with this name (including untagged/dangling ones from rebuilds)
        Write-Info "Removing all ${ImageName} images..."
        $allImages = docker images -a --format "{{.ID}} {{.Repository}}" 2>&1 | Where-Object { $_ -match "${ImageName}" }

        if ($allImages -and $allImages.Count -gt 0) {
            $imageIds = @()
            foreach ($line in $allImages) {
                if ($line -match '^([a-f0-9]+)\s+') {
                    $imageIds += $Matches[1]
                }
            }

            if ($imageIds.Count -gt 0) {
                $uniqueImageIds = $imageIds | Select-Object -Unique
                Write-Info "Found $($uniqueImageIds.Count) image(s) to remove"

                try {
                    $rmiArgs = @('rmi', '-f') + $uniqueImageIds
                    & docker $rmiArgs 2>&1 | Out-Null
                    Write-Success "Removed $($uniqueImageIds.Count) image(s)"
                } catch {
                    Write-Warning-Custom "Failed to remove some images: $($_.Exception.Message)"
                    Write-Info "Continuing with cleanup..."
                }
            } else {
                Write-Info "No images found with name ${ImageName}"
            }
        } else {
            Write-Info "No images found with name ${ImageName}"
        }

        # Clean up dangling images
        Write-Info "Cleaning up dangling images..."
        $danglingImages = docker images -f "dangling=true" -q 2>&1
        if ($danglingImages -and $danglingImages.GetType().Name -ne 'ErrorRecord' -and $danglingImages.Count -gt 0) {
            $rmiDanglingArgs = @('rmi') + $danglingImages
            & docker $rmiDanglingArgs 2>&1 | Out-Null
            Write-Success "Removed $($danglingImages.Count) dangling image(s)"
        } else {
            Write-Info "No dangling images found"
        }

        $cleanupDuration = (Get-Date) - $StartTime
        Write-Success "Cleanup completed in $($cleanupDuration.TotalSeconds.ToString('F2')) seconds"

        # Check if this is cleanup-only mode (Clean specified but no other action flags)
        # Count the bound parameters excluding Clean, Version, and Catalog (which are not action triggers)
        $actionParams = @('SecurityScan', 'Test', 'SkipTests', 'SkipSecurity', 'SkipSBOM', 'Dev', 'Push', 'RegisterGateway')
        $hasActionFlag = $false
        foreach ($param in $actionParams) {
            if ($PSBoundParameters.ContainsKey($param)) {
                $hasActionFlag = $true
                break
            }
        }

        if (-not $hasActionFlag) {
            Write-Host "`nCleanup-only mode - skipping deployment" -ForegroundColor Cyan
            Write-Host "To deploy after cleanup, add deployment flags (e.g., -Dev, -SkipTests, etc.)" -ForegroundColor Gray
            exit 0
        }
    }

    # Step 2: Run tests (unless skipped)
    if (-not $SkipTests) {
        Write-Header "Running Test Suite"
        Write-Info "Running dotnet test..."

        $testVerbosity = if ($VerbosePreference -eq 'Continue') { "normal" } else { "minimal" }

        if ($VerbosePreference -eq 'Continue') {
            # Dev mode: Stream output in real-time for immediate feedback
            Write-Info "Test output streaming (real-time)..."
            dotnet test --configuration Release --verbosity $testVerbosity
            if ($LASTEXITCODE -ne 0) {
                Write-Error-Custom "Tests failed!"
                throw "Test suite must pass before deployment"
            }
            Write-Success "All tests passed"
        } else {
            # Non-verbose: Capture output and parse results
            $testOutput = dotnet test --configuration Release --verbosity $testVerbosity 2>&1
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
        }
    } else {
        Write-Warning-Custom "Skipping test suite (not recommended for production)"
    }

    # Step 3: Clean previous builds
    if (-not $SkipSBOM) {
        Write-Header "Cleaning Previous Builds"
        Write-Info "Removing old images..."

        $oldImages = docker images -q "${ImageName}:${Version}" 2>$null
        if ($oldImages) {
            docker rmi -f $oldImages 2>&1 | Out-Null
            Write-Success "Removed previous image: ${ImageName}:${Version}"
        }
    } else {
        Write-Info "Skipping image cleanup in fast build mode (Docker will replace automatically)"
    }

    # Step 4: Build Docker image
    $buildStart = Get-Date

    if ($SkipSBOM) {
        Write-Header "Building Docker Image (Fast Local Build)"
        Write-Info "Using two-step build process for faster local development..."
        Write-Warning-Custom "SBOM generation skipped (not recommended for production)"

        # Step 1: Publish locally
        Write-Info "Step 1/2: Publishing project locally..."
        $publishDir = Join-Path $ProjectRoot "app"

        # Clean previous publish directory
        if (Test-Path $publishDir) {
            Remove-Item -Path $publishDir -Recurse -Force
        }

        $publishArgs = @(
            "publish",
            "src/RefactorCsharpMCP.Server/RefactorCsharpMCP.Server.csproj",
            "-c", "Release",
            "-o", $publishDir,
            "--no-restore"
        )

        if ($VerbosePreference -eq 'Continue') {
            $publishArgs += "-v", "normal"
        } else {
            $publishArgs += "-v", "quiet"
        }

        & dotnet $publishArgs
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed (exit code: $LASTEXITCODE)"
        }

        Write-Success "Project published to $publishDir"

        # Step 2: Build runtime-only Docker image
        Write-Info "Step 2/2: Building Docker image from published artifacts..."

        # Create temporary runtime-only Dockerfile
        $runtimeDockerfile = Join-Path $ProjectRoot "Dockerfile.local"
        $dockerfileContent = @"
FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app
COPY app/ .

ENTRYPOINT ["dotnet", "RefactorCsharpMCP.Server.dll"]
"@
        Set-Content -Path $runtimeDockerfile -Value $dockerfileContent -Encoding UTF8

        $buildArgs = @("build", "-f", "Dockerfile.local", "-t", "${ImageName}:${Version}", "-t", "${ImageName}:latest")
        if ($VerbosePreference -eq 'Continue') {
            $buildArgs += "--progress=plain"
        }
        $buildArgs += "."

        & docker $buildArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Docker build failed (exit code: $LASTEXITCODE)"
        }

        # Cleanup temporary files
        if (Test-Path $runtimeDockerfile) {
            Remove-Item -Path $runtimeDockerfile -Force
        }

        $buildDuration = (Get-Date) - $buildStart
        Write-Success "Image built successfully in $($buildDuration.TotalSeconds.ToString('F2')) seconds"
        Write-Info "Temporary artifacts (app/) can be cleaned up manually if desired"

    } else {
        Write-Header "Building Docker Image with SBOM"
        Write-Info "Building ${ImageName}:${Version}..."

        # Ensure buildx builder exists for SBOM support
        Write-Info "Checking buildx builder..."
    $builderCheck = docker buildx ls 2>&1 | Select-String "sbom-builder"
    if (-not $builderCheck) {
        Write-Info "Creating buildx builder for SBOM support..."
        $builderOutput = docker buildx create --name sbom-builder --driver docker-container --bootstrap 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Warning-Custom "Failed to create buildx builder:"
            $builderOutput | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
            Write-Warning-Custom "Attempting to use default builder, SBOM generation may not be available"

            # Verify default builder supports SBOM (requires docker-container driver)
            $defaultBuilderInfo = docker buildx inspect 2>&1
            if ($defaultBuilderInfo -notmatch "Driver.*docker-container") {
                throw "Default builder does not support SBOM generation. Please ensure Docker Buildx is properly configured with docker-container driver."
            }
            Write-Info "Default builder verified for SBOM support"
        } else {
            Write-Success "Created buildx builder: sbom-builder"
        }
    }

    # NOTE: Dual-build strategy required because BuildKit cannot simultaneously export
    # SBOM to filesystem AND load image to local Docker daemon in a single build.
    # Expected performance impact:
    #   - First-time builds: 5-10 minutes (buildkit-syft-scanner image download ~13.57 MB)
    #   - Subsequent builds: +30-50% build time overhead
    #   - BuildKit layer caching should minimize impact on cached builds
    # To optimize: Use 'docker buildx build --sbom=true --load' if SBOM export to filesystem not needed.

    # Build strategy: First export SBOM to filesystem, then load image locally
    Write-Info "Building with SBOM export (step 1/2)..."

    # Capture full output for debugging
    # Note: dest path must not have quotes around the entire parameter
    $sbomOutputPath = Join-Path $ProjectRoot "sbom-output"

    # Validate path contains only safe characters (defense-in-depth security)
    if ($sbomOutputPath -notmatch '^[a-zA-Z0-9\\/:._-]+$') {
        throw "Invalid characters in SBOM output path: $sbomOutputPath"
    }

    $buildOutput = docker buildx build `
        --builder sbom-builder `
        --sbom=true `
        --output "type=local,dest=$sbomOutputPath" `
        . 2>&1

    # Display build steps
    $buildOutput | ForEach-Object {
        if ($VerbosePreference -eq 'Continue') {
            Write-Info $_
        } elseif ($_ -match "^#") {
            Write-Info $_
        }
    }

    # Check for errors in output
    $hasError = $buildOutput | Where-Object { $_ -match "error|failed" -and $_ -notmatch "^#" }
    if ($LASTEXITCODE -ne 0 -or $hasError) {
        Write-Error-Custom "Docker buildx output:"
        $buildOutput | ForEach-Object { Write-Host $_ -ForegroundColor Red }

        # Provide diagnostic hints based on error patterns
        if ($buildOutput -match "sbom.*not available|sbom.*disabled|sbom.*not supported") {
            Write-Error-Custom "SBOM generation not available in current BuildKit version"
            Write-Info "Ensure Docker Desktop >= 4.24 or BuildKit >= 0.12"
        } elseif ($buildOutput -match "no space left|disk full") {
            Write-Error-Custom "Insufficient disk space for build output"
        } elseif ($buildOutput -match "permission denied|access denied") {
            Write-Error-Custom "Permission denied - check Docker daemon permissions"
        }

        throw "Docker build with SBOM export failed (exit code: $LASTEXITCODE)"
    }

    # Move SBOM to project root and clean up
    if (Test-Path "${ProjectRoot}\sbom-output\sbom.spdx.json") {
        Move-Item -Path "${ProjectRoot}\sbom-output\sbom.spdx.json" -Destination "${ProjectRoot}\sbom.spdx.json" -Force

        # Validate SBOM is not empty (minimum valid SBOM is ~500 bytes)
        $sbomFileInfo = Get-Item "${ProjectRoot}\sbom.spdx.json"
        if ($sbomFileInfo.Length -lt 500) {
            throw "SBOM file generated but appears invalid (size: $($sbomFileInfo.Length) bytes)"
        }
        Write-Success "SBOM exported: sbom.spdx.json ($([math]::Round($sbomFileInfo.Length / 1KB, 2)) KB)"
    } else {
        # SBOM generation is required for production builds
        $isProductionBuild = $Version -match '^\d+\.\d+\.\d+$' -and $Version -ne "latest"
        if ($isProductionBuild) {
            throw "SBOM generation failed - required for production version $Version"
        }
        Write-Warning-Custom "SBOM file not found in output directory (skipping for non-production build)"
    }

    # Clean up sbom-output directory
    if (Test-Path "${ProjectRoot}\sbom-output") {
        Remove-Item -Path "${ProjectRoot}\sbom-output" -Recurse -Force
    }

    # Step 4.5: SBOM Validation (before loading image - fail fast)
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

            # Save package summary with timestamp
            $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
            $csvPath = "${ProjectRoot}\sbom-packages-${Version}-${timestamp}.csv"
            $sbom.packages | Select-Object name, versionInfo, licenseConcluded |
                Export-Csv -Path $csvPath -NoTypeInformation
            Write-Info "Package list exported to $(Split-Path -Leaf $csvPath)"

        } catch {
            Write-Warning-Custom "Failed to validate SBOM: $($_.Exception.Message)"
        }
    } else {
        Write-Warning-Custom "SBOM file not found, skipping validation"
    }

    # Build and load image locally (step 2/2)
    Write-Info "Building and loading image locally (step 2/2)..."

    # Capture full output
    $buildOutput2 = docker buildx build `
        --builder sbom-builder `
        --sbom=true `
        --tag "${ImageName}:${Version}" `
        --tag "${ImageName}:latest" `
        --load `
        . 2>&1

    # Display build steps
    $buildOutput2 | ForEach-Object {
        if ($VerbosePreference -eq 'Continue') {
            Write-Info $_
        } elseif ($_ -match "^#") {
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
    }
    # End of SBOM conditional build section

    # Step 5: Inspect image
    Write-Header "Image Inspection"
    $imageInfo = docker inspect "${ImageName}:${Version}" | ConvertFrom-Json
    $imageSize = [math]::Round($imageInfo[0].Size / 1MB, 2)
    Write-Info "Image Size: ${imageSize} MB"
    Write-Info "Created: $($imageInfo[0].Created)"

    # Step 6: Health check and CycloneDX SBOM generation (skip in dev mode)
    if (-not $SkipSecurity) {
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
    } else {
        Write-Warning-Custom "Container health check and CycloneDX SBOM generation skipped in dev mode"
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
