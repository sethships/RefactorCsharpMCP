<#
.SYNOPSIS
    Validate Software Bill of Materials (SBOM) content and coverage.

.DESCRIPTION
    Analyzes SBOM files (SPDX and CycloneDX formats) to verify:
    - File exists and is valid JSON
    - Expected NuGet packages are present
    - Package count meets minimum threshold
    - License information is captured
    - Base image dependencies are included

.PARAMETER SbomPath
    Path to the SBOM file to validate. Defaults to "sbom.spdx.json" in current directory.

.PARAMETER Format
    SBOM format: "spdx" or "cyclonedx". Auto-detected from file extension if not specified.

.PARAMETER MinPackages
    Minimum expected package count. Default is 50.

.PARAMETER Verbose
    Show detailed package listing and license information.

.EXAMPLE
    .\validate-sbom.ps1
    Validate default SBOM file (sbom.spdx.json)

.EXAMPLE
    .\validate-sbom.ps1 -SbomPath "sbom.cyclonedx.json" -Format cyclonedx
    Validate CycloneDX SBOM

.EXAMPLE
    .\validate-sbom.ps1 -Verbose
    Show detailed package information

.NOTES
    Author: RefactorCsharpMCP Team
    Supports: SPDX 2.3+ and CycloneDX 1.4+ formats
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$SbomPath = "sbom.spdx.json",

    [Parameter()]
    [ValidateSet("spdx", "cyclonedx", "auto")]
    [string]$Format = "auto",

    [Parameter()]
    [int]$MinPackages = 50,

    [Parameter()]
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

# Helper functions
function Write-ValidationSuccess {
    param([string]$Message)
    Write-Host "[OK] $Message" -ForegroundColor Green
}

function Write-ValidationWarning {
    param([string]$Message)
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

function Write-ValidationError {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

function Write-ValidationInfo {
    param([string]$Message)
    Write-Host "  $Message" -ForegroundColor Gray
}

# Main validation logic
try {
    Write-Host "`n=== SBOM Validation ===" -ForegroundColor Cyan
    Write-Host ""

    # Step 1: File existence check
    Write-Host "1. Checking file existence..." -ForegroundColor White
    if (-not (Test-Path $SbomPath)) {
        Write-ValidationError "SBOM file not found: $SbomPath"
        exit 1
    }
    Write-ValidationSuccess "File exists: $SbomPath"

    $fileInfo = Get-Item $SbomPath
    Write-ValidationInfo "File size: $([math]::Round($fileInfo.Length / 1KB, 2)) KB"
    Write-Host ""

    # Step 2: Detect format
    Write-Host "2. Detecting SBOM format..." -ForegroundColor White
    if ($Format -eq "auto") {
        if ($SbomPath -match "\.cyclonedx\.json$") {
            $Format = "cyclonedx"
        } else {
            $Format = "spdx"
        }
    }
    Write-ValidationSuccess "Format detected: $Format"
    Write-Host ""

    # Step 3: Parse JSON
    Write-Host "3. Parsing JSON content..." -ForegroundColor White
    try {
        $sbomContent = Get-Content $SbomPath -Raw | ConvertFrom-Json
        Write-ValidationSuccess "Valid JSON structure"
    } catch {
        Write-ValidationError "Invalid JSON: $($_.Exception.Message)"
        exit 1
    }
    Write-Host ""

    # Step 4: Validate format-specific structure
    Write-Host "4. Validating SBOM structure..." -ForegroundColor White

    $packages = @()
    $sbomVersion = $null

    if ($Format -eq "spdx") {
        # Validate SPDX format
        if (-not $sbomContent.spdxVersion) {
            Write-ValidationError "Missing spdxVersion field"
            exit 1
        }
        $sbomVersion = $sbomContent.spdxVersion
        Write-ValidationSuccess "SPDX version: $sbomVersion"

        if (-not $sbomContent.packages) {
            Write-ValidationError "Missing packages array"
            exit 1
        }
        $packages = $sbomContent.packages

    } elseif ($Format -eq "cyclonedx") {
        # Validate CycloneDX format
        if (-not $sbomContent.bomFormat) {
            Write-ValidationError "Missing bomFormat field"
            exit 1
        }
        Write-ValidationSuccess "BOM format: $($sbomContent.bomFormat)"

        if ($sbomContent.specVersion) {
            $sbomVersion = $sbomContent.specVersion
            Write-ValidationSuccess "Spec version: $sbomVersion"
        }

        if (-not $sbomContent.components) {
            Write-ValidationError "Missing components array"
            exit 1
        }
        $packages = $sbomContent.components
    }
    Write-Host ""

    # Step 5: Package count validation
    Write-Host "5. Validating package count..." -ForegroundColor White
    $packageCount = ($packages | Measure-Object).Count
    Write-ValidationInfo "Total packages/components: $packageCount"

    if ($packageCount -lt $MinPackages) {
        Write-ValidationWarning "Package count ($packageCount) below minimum threshold ($MinPackages)"
    } else {
        Write-ValidationSuccess "Package count meets minimum threshold ($MinPackages)"
    }
    Write-Host ""

    # Step 6: Check for expected packages
    Write-Host "6. Checking for key dependencies..." -ForegroundColor White
    $keyPackages = @(
        "Microsoft.CodeAnalysis.CSharp",
        "ModelContextProtocol",
        "Microsoft.Extensions.Hosting",
        "Microsoft.Extensions.DependencyInjection"
    )

    $foundCount = 0
    foreach ($pkg in $keyPackages) {
        if ($Format -eq "spdx") {
            $found = $packages | Where-Object { $_.name -like "*$pkg*" }
        } else {
            $found = $packages | Where-Object { $_.name -like "*$pkg*" }
        }

        if ($found) {
            if ($Format -eq "spdx") {
                Write-ValidationSuccess "$pkg - $($found.versionInfo)"
            } else {
                Write-ValidationSuccess "$pkg - $($found.version)"
            }
            $foundCount++
        } else {
            Write-ValidationWarning "$pkg - NOT FOUND"
        }
    }

    Write-ValidationInfo "Found $foundCount of $($keyPackages.Count) expected packages"
    Write-Host ""

    # Step 7: License validation
    Write-Host "7. Validating license information..." -ForegroundColor White

    if ($Format -eq "spdx") {
        $packagesWithLicense = $packages | Where-Object {
            $_.licenseConcluded -or $_.licenseDeclared
        }
    } else {
        $packagesWithLicense = $packages | Where-Object {
            $_.licenses -and $_.licenses.Count -gt 0
        }
    }

    $licensedCount = ($packagesWithLicense | Measure-Object).Count
    $licensePercentage = if ($packageCount -gt 0) {
        [math]::Round(($licensedCount / $packageCount) * 100, 1)
    } else {
        0
    }

    Write-ValidationInfo "Packages with license info: $licensedCount / $packageCount ($licensePercentage%)"

    if ($licensePercentage -ge 80) {
        Write-ValidationSuccess "Good license coverage"
    } elseif ($licensePercentage -ge 50) {
        Write-ValidationWarning "Moderate license coverage"
    } else {
        Write-ValidationWarning "Low license coverage"
    }
    Write-Host ""

    # Step 8: Base image check (SPDX only)
    if ($Format -eq "spdx") {
        Write-Host "8. Checking for base image dependencies..." -ForegroundColor White
        $baseImagePackages = $packages | Where-Object {
            $_.name -like "*dotnet*" -or
            $_.name -like "*runtime*" -or
            $_.name -like "*aspnet*"
        }

        $baseImageCount = ($baseImagePackages | Measure-Object).Count
        if ($baseImageCount -gt 0) {
            Write-ValidationSuccess "Found $baseImageCount base image packages"
        } else {
            Write-ValidationWarning "No base image packages detected"
        }
        Write-Host ""
    }

    # Step 9: Verbose output (if requested)
    if ($Verbose) {
        Write-Host "=== Package Details ===" -ForegroundColor Cyan
        Write-Host ""

        if ($Format -eq "spdx") {
            $packages | Sort-Object name | Select-Object -First 20 | ForEach-Object {
                Write-Host "  $($_.name)" -ForegroundColor White
                if ($_.versionInfo) {
                    Write-Host "    Version: $($_.versionInfo)" -ForegroundColor Gray
                }
                if ($_.licenseConcluded) {
                    Write-Host "    License: $($_.licenseConcluded)" -ForegroundColor Gray
                }
            }
        } else {
            $packages | Sort-Object name | Select-Object -First 20 | ForEach-Object {
                Write-Host "  $($_.name)" -ForegroundColor White
                if ($_.version) {
                    Write-Host "    Version: $($_.version)" -ForegroundColor Gray
                }
                if ($_.licenses -and $_.licenses.Count -gt 0) {
                    $licenseStr = ($_.licenses | ForEach-Object { $_.license.id }) -join ", "
                    Write-Host "    License: $licenseStr" -ForegroundColor Gray
                }
            }
        }

        if ($packageCount -gt 20) {
            Write-Host ""
            Write-Host "  ... and $($packageCount - 20) more packages" -ForegroundColor Gray
        }
        Write-Host ""
    }

    # Final summary
    Write-Host "=== Validation Summary ===" -ForegroundColor Cyan
    Write-Host ""
    Write-ValidationSuccess "SBOM file is valid"
    Write-Host "  Format: $Format ($sbomVersion)" -ForegroundColor Gray
    Write-Host "  Packages: $packageCount" -ForegroundColor Gray
    Write-Host "  Key deps found: $foundCount / $($keyPackages.Count)" -ForegroundColor Gray
    Write-Host "  License coverage: $licensePercentage%" -ForegroundColor Gray
    Write-Host ""

    exit 0

} catch {
    Write-Host ""
    Write-ValidationError "Validation failed: $($_.Exception.Message)"
    Write-Host ""
    Write-Host "Stack trace:" -ForegroundColor Gray
    Write-Host $_.ScriptStackTrace -ForegroundColor Gray
    Write-Host ""
    exit 1
}
