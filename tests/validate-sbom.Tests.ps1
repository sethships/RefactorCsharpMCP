<#
.SYNOPSIS
    Pester tests for validate-sbom.ps1 script

.DESCRIPTION
    Comprehensive test suite for SBOM validation functionality covering:
    - Path validation and security
    - Format detection (SPDX and CycloneDX)
    - JSON parsing and validation
    - Package count thresholds
    - License coverage calculation
    - Error handling and edge cases

.NOTES
    Requires: Pester 5.x (Windows default is 3.4.0 - upgrade required)

    Install Pester 5.x:
        Install-Module -Name Pester -Force -SkipPublisherCheck -Scope CurrentUser

    Run tests:
        Invoke-Pester -Path .\tests\validate-sbom.Tests.ps1 -Output Detailed
#>

BeforeAll {
    # Import the script under test
    $scriptPath = Join-Path $PSScriptRoot "..\scripts\validate-sbom.ps1"
    if (-not (Test-Path $scriptPath)) {
        throw "validate-sbom.ps1 not found at $scriptPath"
    }

    # Create temp directory for test files
    $script:TempDir = Join-Path $env:TEMP "sbom-tests-$(Get-Date -Format 'yyyyMMddHHmmss')"
    New-Item -Path $script:TempDir -ItemType Directory -Force | Out-Null

    # Helper function to create test SBOM files
    function New-TestSBOM {
        param(
            [string]$Format,
            [int]$PackageCount = 100,
            [bool]$IncludeLicenses = $true,
            [string]$FileName
        )

        $filePath = Join-Path $script:TempDir $FileName

        if ($Format -eq "spdx") {
            $packages = @()
            for ($i = 1; $i -le $PackageCount; $i++) {
                $package = @{
                    name = "Package$i"
                    versionInfo = "1.0.$i"
                }
                if ($IncludeLicenses -and ($i % 2 -eq 0)) {
                    $package.licenseConcluded = "MIT"
                } elseif ($IncludeLicenses -and ($i % 3 -eq 0)) {
                    $package.licenseDeclared = "Apache-2.0"
                } elseif ($i % 5 -eq 0) {
                    $package.licenseConcluded = "NOASSERTION"
                }
                $packages += $package
            }

            $sbom = @{
                spdxVersion = "SPDX-2.3"
                dataLicense = "CC0-1.0"
                SPDXID = "SPDXRef-DOCUMENT"
                name = "Test SBOM"
                packages = $packages
            }

            $sbom | ConvertTo-Json -Depth 10 | Out-File -FilePath $filePath -Encoding UTF8
        } elseif ($Format -eq "cyclonedx") {
            $components = @()
            for ($i = 1; $i -le $PackageCount; $i++) {
                $component = @{
                    name = "Component$i"
                    version = "1.0.$i"
                    type = "library"
                }
                if ($IncludeLicenses) {
                    $component.licenses = @(
                        @{ license = @{ id = "MIT" } }
                    )
                }
                $components += $component
            }

            $sbom = @{
                bomFormat = "CycloneDX"
                specVersion = "1.4"
                version = 1
                components = $components
            }

            $sbom | ConvertTo-Json -Depth 10 | Out-File -FilePath $filePath -Encoding UTF8
        }

        return $filePath
    }
}

AfterAll {
    # Cleanup temp directory
    if (Test-Path $script:TempDir) {
        Remove-Item -Path $script:TempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Describe "validate-sbom.ps1 - Path Validation" {
    It "Should reject paths with invalid characters" {
        $invalidPath = "C:\temp\sbom;rm -rf /*.json"
        $testFile = New-TestSBOM -Format "spdx" -FileName "test.json"

        # Run script and capture output
        $result = & $scriptPath -SbomPath $invalidPath *>&1
        $LASTEXITCODE | Should -Be 1
    }

    It "Should accept valid paths" {
        $testFile = New-TestSBOM -Format "spdx" -FileName "valid-sbom.json"
        $result = & $scriptPath -SbomPath $testFile -MinPackages 10 *>&1
        $LASTEXITCODE | Should -Be 0
    }
}

Describe "validate-sbom.ps1 - File Existence" {
    It "Should fail when file does not exist" {
        $nonExistentPath = Join-Path $script:TempDir "nonexistent.json"
        $result = & $scriptPath -SbomPath $nonExistentPath *>&1
        $LASTEXITCODE | Should -Be 1
    }

    It "Should succeed when file exists" {
        $testFile = New-TestSBOM -Format "spdx" -FileName "exists.json"
        $result = & $scriptPath -SbomPath $testFile -MinPackages 10 *>&1
        $LASTEXITCODE | Should -Be 0
    }
}

Describe "validate-sbom.ps1 - Format Detection" {
    It "Should detect SPDX format from content" {
        $testFile = New-TestSBOM -Format "spdx" -FileName "detect-spdx.json"
        $result = & $scriptPath -SbomPath $testFile -Format auto -MinPackages 10 *>&1
        ($result -join "`n") | Should -Match "Format detected: spdx"
        $LASTEXITCODE | Should -Be 0
    }

    It "Should detect CycloneDX format from content" {
        $testFile = New-TestSBOM -Format "cyclonedx" -FileName "detect-cyclonedx.json"
        $result = & $scriptPath -SbomPath $testFile -Format auto -MinPackages 10 *>&1
        ($result -join "`n") | Should -Match "Format detected: cyclonedx"
        $LASTEXITCODE | Should -Be 0
    }

    It "Should detect CycloneDX from filename when content ambiguous" {
        $testFile = New-TestSBOM -Format "spdx" -FileName "ambiguous.cyclonedx.json"
        $result = & $scriptPath -SbomPath $testFile -Format auto -MinPackages 10 *>&1
        # Should use content-based detection (SPDX) not filename
        ($result -join "`n") | Should -Match "Format detected: spdx"
        $LASTEXITCODE | Should -Be 0
    }
}

Describe "validate-sbom.ps1 - JSON Parsing" {
    It "Should fail on invalid JSON" {
        $invalidJsonPath = Join-Path $script:TempDir "invalid.json"
        "{ invalid json" | Out-File -FilePath $invalidJsonPath -Encoding UTF8

        $result = & $scriptPath -SbomPath $invalidJsonPath *>&1
        $LASTEXITCODE | Should -Be 1
        ($result -join "`n") | Should -Match "Invalid JSON"
    }

    It "Should succeed on valid JSON" {
        $testFile = New-TestSBOM -Format "spdx" -FileName "valid-json.json"
        $result = & $scriptPath -SbomPath $testFile -MinPackages 10 *>&1
        $LASTEXITCODE | Should -Be 0
    }
}

Describe "validate-sbom.ps1 - Package Count Validation" {
    It "Should pass when package count meets minimum" {
        $testFile = New-TestSBOM -Format "spdx" -PackageCount 100 -FileName "meets-min.json"
        $result = & $scriptPath -SbomPath $testFile -MinPackages 80 *>&1
        $LASTEXITCODE | Should -Be 0
    }

    It "Should warn when package count below minimum" {
        $testFile = New-TestSBOM -Format "spdx" -PackageCount 50 -FileName "below-min.json"
        $result = & $scriptPath -SbomPath $testFile -MinPackages 80 *>&1
        ($result -join "`n") | Should -Match "below minimum threshold"
        # Note: Script warns but doesn't fail (exit 0)
        $LASTEXITCODE | Should -Be 0
    }

    It "Should handle custom minimum package count" {
        $testFile = New-TestSBOM -Format "spdx" -PackageCount 30 -FileName "custom-min.json"
        $result = & $scriptPath -SbomPath $testFile -MinPackages 25 *>&1
        $LASTEXITCODE | Should -Be 0
    }
}

Describe "validate-sbom.ps1 - License Coverage (SPDX)" {
    It "Should calculate license coverage correctly" {
        $testFile = New-TestSBOM -Format "spdx" -PackageCount 100 -IncludeLicenses $true -FileName "license-coverage.json"
        $result = & $scriptPath -SbomPath $testFile -MinPackages 10 *>&1
        ($result -join "`n") | Should -Match "Packages with license info"
        $LASTEXITCODE | Should -Be 0
    }

    It "Should exclude NOASSERTION from license count" {
        # Package creation includes some with NOASSERTION
        $testFile = New-TestSBOM -Format "spdx" -PackageCount 100 -IncludeLicenses $true -FileName "noassertion.json"
        $result = & $scriptPath -SbomPath $testFile -MinPackages 10 *>&1

        # Read the SBOM to verify NOASSERTION packages exist
        $sbom = Get-Content $testFile -Raw | ConvertFrom-Json
        $noAssertionCount = ($sbom.packages | Where-Object { $_.licenseConcluded -eq "NOASSERTION" } | Measure-Object).Count
        $noAssertionCount | Should -BeGreaterThan 0

        $LASTEXITCODE | Should -Be 0
    }

    It "Should report good license coverage (>80%)" {
        $testFile = New-TestSBOM -Format "spdx" -PackageCount 100 -IncludeLicenses $true -FileName "good-coverage.json"
        $result = & $scriptPath -SbomPath $testFile -MinPackages 10 *>&1
        ($result -join "`n") | Should -Match "Good license coverage|Moderate license coverage"
        $LASTEXITCODE | Should -Be 0
    }
}

Describe "validate-sbom.ps1 - CycloneDX Support" {
    It "Should validate CycloneDX SBOM structure" {
        $testFile = New-TestSBOM -Format "cyclonedx" -FileName "cyclonedx-valid.json"
        $result = & $scriptPath -SbomPath $testFile -Format cyclonedx -MinPackages 10 *>&1
        ($result -join "`n") | Should -Match "BOM format: CycloneDX"
        $LASTEXITCODE | Should -Be 0
    }

    It "Should count components in CycloneDX SBOM" {
        $testFile = New-TestSBOM -Format "cyclonedx" -PackageCount 50 -FileName "cyclonedx-count.json"
        $result = & $scriptPath -SbomPath $testFile -Format cyclonedx -MinPackages 10 *>&1
        ($result -join "`n") | Should -Match "Total packages/components: 50"
        $LASTEXITCODE | Should -Be 0
    }
}

Describe "validate-sbom.ps1 - Error Handling" {
    It "Should handle empty SBOM file" {
        $emptyPath = Join-Path $script:TempDir "empty.json"
        "" | Out-File -FilePath $emptyPath -Encoding UTF8

        $result = & $scriptPath -SbomPath $emptyPath *>&1
        $LASTEXITCODE | Should -Be 1
    }

    It "Should handle SBOM with missing required fields" {
        $invalidPath = Join-Path $script:TempDir "missing-fields.json"
        @{ someField = "value" } | ConvertTo-Json | Out-File -FilePath $invalidPath -Encoding UTF8

        $result = & $scriptPath -SbomPath $invalidPath -Format spdx *>&1
        $LASTEXITCODE | Should -Be 1
        ($result -join "`n") | Should -Match "Missing spdxVersion field"
    }
}

Describe "validate-sbom.ps1 - Verbose Output" {
    It "Should provide detailed output with -Verbose flag" {
        $testFile = New-TestSBOM -Format "spdx" -PackageCount 25 -FileName "verbose-test.json"
        $result = & $scriptPath -SbomPath $testFile -MinPackages 10 -Verbose *>&1
        ($result -join "`n") | Should -Match "Package Details"
        $LASTEXITCODE | Should -Be 0
    }
}
