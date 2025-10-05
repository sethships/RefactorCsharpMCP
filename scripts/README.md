# RefactorCsharpMCP Deployment Scripts

Automated deployment, security scanning, and validation scripts for Docker containers.

## Scripts Overview

| Script | Purpose | Platform |
|--------|---------|----------|
| `deploy-docker.ps1` | Full deployment pipeline | Windows (PowerShell) |
| `deploy-docker.sh` | Full deployment pipeline | Linux/Mac (Bash) |
| `security-scan.ps1` | Security vulnerability scanning | Windows (PowerShell) |
| `security-scan.sh` | Security vulnerability scanning | Linux/Mac (Bash) |
| `test-deployment.ps1` | Post-deployment validation | Windows (PowerShell) |
| `test-deployment.sh` | Post-deployment validation | Linux/Mac (Bash) |
| `toggle-mcp.ps1` | Enable/disable MCP server in Claude Code | Windows (PowerShell) |

## Quick Start

### Windows (PowerShell)

**Full deployment with all checks:**
```powershell
.\scripts\deploy-docker.ps1 -Version "0.4.0" -SecurityScan -Test
```

**Quick development build:**
```powershell
.\scripts\deploy-docker.ps1 -SkipSecurity
```

**Security scan only:**
```powershell
.\scripts\security-scan.ps1 -Detailed -GenerateSBOM
```

### Linux/Mac (Bash)

**Full deployment:**
```bash
./scripts/deploy-docker.sh -v 0.4.0 -s -t
```

**Quick build:**
```bash
./scripts/deploy-docker.sh --skip-security
```

**Security scan:**
```bash
./scripts/security-scan.sh refactor-csharp-mcp:latest --detailed --sbom
```

## Deployment Pipeline (`deploy-docker`)

### Features
- ✅ Pre-deployment test validation (107 tests)
- ✅ Multi-stage Docker build with caching
- ✅ Automatic version tagging
- ✅ Container health verification
- ✅ Optional security scanning
- ✅ Post-deployment validation
- ✅ Registry push support
- ✅ Detailed logging

### Options (PowerShell)

| Option | Description |
|--------|-------------|
| `-Version` | Image version tag (default: "latest") |
| `-SecurityScan` | Run security scans |
| `-Test` | Run post-deployment tests |
| `-SkipTests` | Skip pre-deployment test suite |
| `-SkipSecurity` | Skip security scanning (not recommended) |
| `-Push` | Push to registry after build |
| `-Registry` | Docker registry URL |

### Options (Bash)

| Option | Description |
|--------|-------------|
| `-v, --version` | Image version tag |
| `-s, --security` | Run security scans |
| `-t, --test` | Run validation tests |
| `--skip-tests` | Skip test suite |
| `--skip-security` | Skip security scanning |
| `-p, --push` | Push to registry |
| `-r, --registry` | Docker registry URL |

### Examples

**Production deployment:**
```powershell
.\scripts\deploy-docker.ps1 -Version "1.0.0" -SecurityScan -Test -Push -Registry "myregistry.io/myuser"
```

**Development build:**
```powershell
.\scripts\deploy-docker.ps1 -SkipSecurity
```

**CI/CD pipeline:**
```bash
./scripts/deploy-docker.sh -v "${CI_COMMIT_TAG}" -s -t --fail-on-critical
```

## Security Scanning (`security-scan`)

### Tools Used
- **Docker Scout** - Official Docker CVE database
- **Trivy** - Comprehensive vulnerability scanner

### Features
- CVE vulnerability detection
- Dependency analysis
- SBOM generation (CycloneDX format)
- Severity filtering (CRITICAL, HIGH, MEDIUM, LOW)
- Detailed reporting
- Fail-on-critical option

### Options (PowerShell)

| Option | Description |
|--------|-------------|
| `-ImageName` | Image to scan (default: refactor-csharp-mcp:latest) |
| `-Detailed` | Generate detailed reports |
| `-GenerateSBOM` | Create Software Bill of Materials |
| `-FailOnCritical` | Exit with error on CRITICAL vulnerabilities |
| `-OutputDir` | Directory for reports |

### Examples

**Comprehensive scan:**
```powershell
.\scripts\security-scan.ps1 -ImageName "refactor-csharp-mcp:0.4.0" -Detailed -GenerateSBOM -FailOnCritical
```

**Quick scan:**
```bash
./scripts/security-scan.sh refactor-csharp-mcp:latest
```

### Output Files

| File | Content |
|------|---------|
| `security-scout-TIMESTAMP.txt` | Docker Scout CVE scan results |
| `security-trivy-TIMESTAMP.txt` | Trivy vulnerability scan results |
| `security-recommendations-TIMESTAMP.txt` | Docker Scout recommendations |
| `sbom-TIMESTAMP.json` | Software Bill of Materials (CycloneDX) |
| `image-layers-TIMESTAMP.txt` | Docker layer analysis |

## Validation Testing (`test-deployment`)

### Tests Performed
1. ✅ Container startup verification
2. ✅ Running status check
3. ✅ Health check validation
4. ✅ Resource usage monitoring
5. ✅ Stdio transport validation

### Usage

**PowerShell:**
```powershell
.\scripts\test-deployment.ps1 -ImageName "refactor-csharp-mcp:test"
```

**Bash:**
```bash
./scripts/test-deployment.sh refactor-csharp-mcp:test
```

## Prerequisites

### Required
- Docker Desktop 4.42.0+
- .NET 8 SDK

### Optional (for security scanning)
- [Docker Scout](https://docs.docker.com/scout/) - Included with Docker Desktop
- [Trivy](https://github.com/aquasecurity/trivy/releases) - Download separately

### Installing Trivy

**Windows (winget):**
```powershell
winget install Aqua.Trivy
```

**macOS (Homebrew):**
```bash
brew install trivy
```

**Linux:**
```bash
wget https://github.com/aquasecurity/trivy/releases/download/v0.55.0/trivy_0.55.0_Linux-64bit.tar.gz
tar zxvf trivy_0.55.0_Linux-64bit.tar.gz
sudo mv trivy /usr/local/bin/
```

## Managing MCP Server in Claude Code

The MCP server runs continuously when Claude Code is active. You can toggle it on/off as needed:

### Check Status
```powershell
.\scripts\toggle-mcp.ps1 status
```

### Enable MCP Server
```powershell
.\scripts\toggle-mcp.ps1 on
# Then restart Claude Code to load it
```

### Disable MCP Server
```powershell
.\scripts\toggle-mcp.ps1 off
# Then restart Claude Code to unload it
```

**Note**: Changes require a Claude Code restart to take effect. This is useful when you want the MCP server available only for specific sessions.

## Workflow Recommendations

### Development
```powershell
# Quick iteration - skip security for speed
.\scripts\deploy-docker.ps1 -SkipSecurity
```

### Pre-commit
```powershell
# Full validation before committing
.\scripts\deploy-docker.ps1 -Version "dev" -SecurityScan -Test
```

### Pre-release
```powershell
# Comprehensive production validation
.\scripts\deploy-docker.ps1 -Version "1.0.0" -SecurityScan -Test -FailOnCritical
.\scripts\security-scan.ps1 -Version "1.0.0" -Detailed -GenerateSBOM
```

### Production Deployment
```powershell
# Build, scan, test, and push
.\scripts\deploy-docker.ps1 `
    -Version "1.0.0" `
    -SecurityScan `
    -Test `
    -FailOnCritical `
    -Push `
    -Registry "myregistry.io/myorg"
```

## CI/CD Integration

### GitHub Actions Example
```yaml
name: Build and Deploy

on:
  push:
    tags:
      - 'v*'

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Deploy Docker Image
        run: |
          chmod +x scripts/deploy-docker.sh
          ./scripts/deploy-docker.sh \
            -v ${GITHUB_REF#refs/tags/v} \
            -s \
            -t \
            --fail-on-critical \
            -p \
            -r ${{ secrets.DOCKER_REGISTRY }}
```

## Troubleshooting

### "Docker Scout not available"
Docker Scout is included with Docker Desktop 4.42.0+. Update Docker Desktop or install Scout CLI separately.

### "Trivy not installed"
Install Trivy from https://github.com/aquasecurity/trivy/releases

### "Tests failed"
Ensure all 107 tests pass before building:
```bash
dotnet test
```

### Container exits immediately
This is normal for stdio MCP servers - they wait for stdin and exit when no input is received.

## Security Best Practices

1. **Always scan before production** - Use `-SecurityScan` flag
2. **Review CRITICAL vulnerabilities** - Check security reports
3. **Keep base images updated** - Rebuild quarterly or on security patches
4. **Generate SBOMs** - Track all dependencies for compliance
5. **Use pinned digests** - Already configured in Dockerfile

## Support

For issues with these scripts, check:
1. Prerequisites are installed
2. Docker daemon is running
3. .NET SDK is accessible
4. Review `deployment.log` for details

---

**Last Updated:** October 2025
**Version:** 1.0.0
