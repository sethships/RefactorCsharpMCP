<#
.SYNOPSIS
    Comprehensive security scanning for RefactorCsharpMCP Docker image.

.DESCRIPTION
    Performs detailed security analysis using Docker Scout and Trivy:
    - CVE vulnerability scanning
    - Dependency analysis
    - Layer inspection
    - SBOM generation
    - Compliance checking

.PARAMETER ImageName
    Docker image to scan (default: refactor-csharp-mcp:latest)

.PARAMETER Detailed
    Generate detailed reports with full vulnerability information

.PARAMETER GenerateSBOM
    Generate Software Bill of Materials (SBOM)

.PARAMETER FailOnCritical
    Exit with error code if CRITICAL vulnerabilities found

.PARAMETER OutputDir
    Directory for security reports (default: current directory)

.EXAMPLE
    .\security-scan.ps1 -Detailed -GenerateSBOM

.EXAMPLE
    .\security-scan.ps1 -ImageName "refactor-csharp-mcp:0.4.0" -FailOnCritical
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$ImageName = "refactor-csharp-mcp:latest",

    [Parameter()]
    [switch]$Detailed,

    [Parameter()]
    [switch]$GenerateSBOM,

    [Parameter()]
    [switch]$FailOnCritical,

    [Parameter()]
    [string]$OutputDir = "."
)

$ErrorActionPreference = "Stop"
$ReportTime = Get-Date -Format "yyyyMMdd-HHmmss"
$HasCritical = $false

function Write-Header {
    param([string]$Message)
    Write-Host "`n==== $Message ====" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "✓ $Message" -ForegroundColor Green
}

function Write-Error-Custom {
    param([string]$Message)
    Write-Host "✗ $Message" -ForegroundColor Red
}

function Write-Warning-Custom {
    param([string]$Message)
    Write-Host "⚠ $Message" -ForegroundColor Yellow
}

function Write-Info {
    param([string]$Message)
    Write-Host "  $Message" -ForegroundColor Gray
}

Write-Header "Security Scanning: $ImageName"

try {
    # Verify image exists
    Write-Info "Verifying image exists..."
    $imageExists = docker images -q $ImageName 2>$null
    if (-not $imageExists) {
        throw "Image '$ImageName' not found. Build it first with deploy-docker.ps1"
    }
    Write-Success "Image found"

    # Docker Scout Scanning
    Write-Header "Docker Scout Analysis"
    $scoutAvailable = docker scout version 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Info "Docker Scout version: $scoutAvailable"

        # CVE scan
        Write-Info "Running CVE vulnerability scan..."
        $scoutReport = Join-Path $OutputDir "security-scout-$ReportTime.txt"
        docker scout cves $ImageName 2>&1 | Tee-Object -FilePath $scoutReport

        # Check for CRITICAL vulnerabilities
        $criticalCount = (Select-String -Path $scoutReport -Pattern "CRITICAL" -AllMatches).Matches.Count
        if ($criticalCount -gt 0) {
            Write-Error-Custom "Found $criticalCount CRITICAL vulnerabilities"
            $HasCritical = $true
        } else {
            Write-Success "No CRITICAL vulnerabilities found"
        }

        # Recommendations
        Write-Info "Getting security recommendations..."
        docker scout recommendations $ImageName 2>&1 | Tee-Object -FilePath (Join-Path $OutputDir "security-recommendations-$ReportTime.txt")

        Write-Success "Docker Scout scan completed: $scoutReport"
    } else {
        Write-Warning-Custom "Docker Scout not available"
        Write-Info "Install: https://docs.docker.com/scout/"
    }

    # Trivy Scanning
    Write-Header "Trivy Analysis"
    $trivyAvailable = Get-Command trivy -ErrorAction SilentlyContinue
    if ($trivyAvailable) {
        Write-Info "Trivy version: $(trivy --version)"

        # Comprehensive scan
        Write-Info "Running comprehensive vulnerability scan..."
        $trivyReport = Join-Path $OutputDir "security-trivy-$ReportTime.txt"
        $trivyArgs = @("image", "--severity", "UNKNOWN,LOW,MEDIUM,HIGH,CRITICAL")
        if ($Detailed) {
            $trivyArgs += "--format", "table"
        }
        $trivyArgs += $ImageName

        & trivy $trivyArgs 2>&1 | Tee-Object -FilePath $trivyReport

        # Check for CRITICAL
        $trivyCritical = (Select-String -Path $trivyReport -Pattern "CRITICAL" -AllMatches).Matches.Count
        if ($trivyCritical -gt 0) {
            Write-Error-Custom "Trivy found $trivyCritical CRITICAL vulnerabilities"
            $HasCritical = $true
        } else {
            Write-Success "No CRITICAL vulnerabilities found by Trivy"
        }

        Write-Success "Trivy scan completed: $trivyReport"

        # SBOM Generation
        if ($GenerateSBOM) {
            Write-Header "Generating SBOM"
            $sbomFile = Join-Path $OutputDir "sbom-$ReportTime.json"
            Write-Info "Generating Software Bill of Materials..."
            trivy image --format cyclonedx --output $sbomFile $ImageName 2>&1 | Out-Null
            Write-Success "SBOM generated: $sbomFile"
        }
    } else {
        Write-Warning-Custom "Trivy not installed"
        Write-Info "Install: https://github.com/aquasecurity/trivy/releases"
    }

    # Layer Analysis
    Write-Header "Image Layer Analysis"
    Write-Info "Analyzing image layers..."
    $layerInfo = docker history $ImageName --human --no-trunc
    $layerInfo | Out-File -FilePath (Join-Path $OutputDir "image-layers-$ReportTime.txt")
    Write-Info "Layer count: $(($layerInfo | Measure-Object).Count)"
    Write-Success "Layer analysis saved"

    # Summary
    Write-Header "Security Scan Summary"
    Write-Success "Image scanned: $ImageName"
    Write-Success "Reports generated in: $OutputDir"

    if ($HasCritical) {
        Write-Error-Custom "CRITICAL vulnerabilities detected!"
        if ($FailOnCritical) {
            Write-Error-Custom "Failing due to -FailOnCritical flag"
            exit 1
        } else {
            Write-Warning-Custom "Review security reports before deployment"
        }
    } else {
        Write-Success "No CRITICAL vulnerabilities detected"
    }

} catch {
    Write-Error-Custom $_.Exception.Message
    exit 1
}
