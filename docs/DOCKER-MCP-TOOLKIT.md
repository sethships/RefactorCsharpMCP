# Docker MCP Toolkit Integration Guide

**RefactorCsharpMCP** - Comprehensive guide for Docker Desktop MCP Toolkit integration

---

## Table of Contents

- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [Quick Start](#quick-start)
- [Integration Options](#integration-options)
- [Setup Instructions](#setup-instructions)
- [Management Commands](#management-commands)
- [Configuration](#configuration)
- [Troubleshooting](#troubleshooting)
- [Advanced Topics](#advanced-topics)
- [Best Practices](#best-practices)
- [FAQ](#faq)

---

## Overview

RefactorCsharpMCP is fully integrated with Docker Desktop's MCP (Model Context Protocol) Toolkit, providing centralized management and discovery of MCP servers. This integration enables:

- **Centralized Management**: Manage all MCP servers through Docker Desktop
- **Zero-Config Deployment**: Register once, use everywhere
- **Automatic Discovery**: Servers appear in Docker Desktop UI
- **Resource Limits**: Automatic enforcement of CPU and memory limits
- **Version Management**: Easy updates and rollbacks
- **Standardized Configuration**: Consistent setup across all MCP servers

### Architecture

```
┌────────────────────────────────────────────────────────┐
│              Docker Desktop                            │
│  ┌──────────────────────────────────────────────────┐ │
│  │         MCP Toolkit / Gateway                    │ │
│  │  ┌────────────────────────────────────────────┐  │ │
│  │  │  Catalog: local-dev                        │  │ │
│  │  │  - docker-mcp.yaml                         │  │ │
│  │  │  - refactor-csharp-mcp (enabled)          │  │ │
│  │  └────────────────────────────────────────────┘  │ │
│  └──────────────────────────────────────────────────┘ │
│                     │                                  │
│                     ▼                                  │
│  ┌──────────────────────────────────────────────────┐ │
│  │  RefactorCsharpMCP Container                     │ │
│  │  - Image: refactor-csharp-mcp:latest            │ │
│  │  - User: mcpuser (non-root)                     │ │
│  │  - Transport: stdio                              │ │
│  │  - Resources: 1 CPU, 2GB RAM                     │ │
│  │  - Health: Monitored                             │ │
│  └──────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────┘
                     │
                     ▼
┌────────────────────────────────────────────────────────┐
│  AI Clients (Claude Desktop, VS Code, Cursor)          │
│  - Connects via stdio over Docker MCP Gateway          │
│  - Access to all 11 refactoring tools                  │
└────────────────────────────────────────────────────────┘
```

---

## Prerequisites

### Required

- **Docker Desktop 4.25+** with MCP Gateway support
  - Download from: https://www.docker.com/products/docker-desktop/
  - Verify: `docker mcp --help`

- **.NET 8 SDK** (for building from source)
  - Download from: https://dotnet.microsoft.com/download
  - Required only if building locally

### Optional

- **PowerShell 7+** (for Windows registration scripts)
  - Download from: https://github.com/PowerShell/PowerShell
  - Alternative: Use bash version on WSL/Linux

### Verification

```bash
# Check Docker version
docker --version
# Expected: Docker version 28.x or later

# Check MCP Gateway availability
docker mcp --help
# Expected: Docker MCP Toolkit's CLI - Manage your MCP servers and clients.

# Check .NET SDK (if building)
dotnet --version
# Expected: 8.0.x or later
```

### Script Permissions (Linux/macOS)

The deployment and registration scripts require execute permissions on Unix-based systems:

```bash
# Grant execute permissions to all shell scripts
chmod +x scripts/*.sh

# Or individually:
chmod +x scripts/deploy-docker.sh
chmod +x scripts/register-mcp-gateway.sh
```

On Windows, PowerShell scripts may require execution policy changes:
```powershell
# Check current policy
Get-ExecutionPolicy

# Allow local scripts (if needed)
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

---

## Quick Start

### 5-Minute Setup

```bash
# 1. Clone and build
git clone https://github.com/sethb75/RefactorCsharpMCP.git
cd RefactorCsharpMCP
docker build -t refactor-csharp-mcp:latest .

# 2. Register with Docker MCP Gateway
# Windows
pwsh ./scripts/register-mcp-gateway.ps1

# Linux/macOS
./scripts/register-mcp-gateway.sh

# 3. Verify
docker mcp server ls
# Should see: refactor-csharp-mcp

# 4. Configure Claude Desktop
# See "Configuration" section below
```

---

## Integration Options

RefactorCsharpMCP supports two integration approaches:

### Option 1: Docker MCP Gateway (Recommended)

**Pros:**
- ✅ Centralized management
- ✅ Discoverable in Docker Desktop UI
- ✅ Automatic resource limits
- ✅ Easy updates and version management
- ✅ Standardized configuration

**Cons:**
- ❌ Requires Docker Desktop with MCP Gateway
- ❌ Slightly higher overhead

**Best for:** Production use, teams, multi-server environments

### Option 2: Direct Docker

**Pros:**
- ✅ No gateway dependency
- ✅ Direct container control
- ✅ Lower overhead
- ✅ Simpler debugging

**Cons:**
- ❌ Manual configuration per client
- ❌ No centralized management
- ❌ Manual resource limit enforcement

**Best for:** Development, testing, single-server setups

---

## Setup Instructions

### Option 1: Gateway Setup (Detailed)

#### Step 1: Build Docker Image

```bash
# Navigate to project directory
cd RefactorCsharpMCP

# Build the image
docker build -t refactor-csharp-mcp:latest .

# Verify build
docker images | grep refactor-csharp-mcp
# Expected output:
# refactor-csharp-mcp  latest  ...  238MB
```

#### Step 2: Register with Gateway

**Windows (PowerShell):**
```powershell
# Standard registration
pwsh ./scripts/register-mcp-gateway.ps1

# With validation
pwsh ./scripts/register-mcp-gateway.ps1 -Validate

# Specific version
pwsh ./scripts/register-mcp-gateway.ps1 -Version "1.0.0"

# Custom catalog
pwsh ./scripts/register-mcp-gateway.ps1 -Catalog "my-catalog"
```

**Linux/macOS (Bash):**
```bash
# Standard registration
./scripts/register-mcp-gateway.sh

# With validation
./scripts/register-mcp-gateway.sh latest local-dev true

# Specific version
./scripts/register-mcp-gateway.sh 1.0.0
```

**What the script does:**
1. Validates Docker and MCP Gateway availability
2. Checks Docker image exists
3. Initializes MCP catalog system (if needed)
4. Copies `docker-mcp.yaml` to Docker's catalog
5. Enables the server
6. Verifies registration

#### Step 3: Verify Registration

```bash
# Check catalog
docker mcp catalog show local-dev
# Expected: refactor-csharp-mcp appears in list

# Verify server is enabled
docker mcp server ls
# Expected: refactor-csharp-mcp in output

# Inspect server details
docker mcp server inspect refactor-csharp-mcp
# Shows tools, resources, transport configuration
```

#### Step 4: Configure AI Client

**Claude Desktop:**

Location: `%APPDATA%\Claude\claude_desktop_config.json` (Windows) or `~/Library/Application Support/Claude/claude_desktop_config.json` (macOS)

```json
{
  "mcpServers": {
    "refactor-csharp-mcp": {
      "command": "docker",
      "args": ["mcp", "gateway", "run"]
    }
  }
}
```

**VS Code (with MCP extension):**

Location: `.vscode/settings.json` or User Settings

```json
{
  "mcp.servers": {
    "refactor-csharp-mcp": {
      "command": "docker",
      "args": ["mcp", "gateway", "run"],
      "type": "stdio"
    }
  }
}
```

**Cursor:**

Location: Cursor settings JSON

```json
{
  "mcpServers": {
    "refactor-csharp-mcp": {
      "command": "docker",
      "args": ["mcp", "gateway", "run"]
    }
  }
}
```

#### Step 5: Test Connection

Restart your AI client and verify the connection:

**In Claude Desktop:**
```
User: "Can you list the available refactoring tools?"

Expected: Claude should list all 11 refactoring tools:
- extract_method
- constructor_injection
- make_field_readonly
- safe_delete_method
- extract_class
- remove_unused_usings
- inline_method
- rename_symbol
- fix_diagnostic
- inline_variable
- analyze_code
```

### Option 2: Direct Docker Setup

**Configure AI Client:**

```json
{
  "mcpServers": {
    "refactor-csharp-mcp": {
      "command": "docker",
      "args": ["run", "--rm", "-i", "refactor-csharp-mcp:latest"]
    }
  }
}
```

**Optional: Add resource limits**

```json
{
  "mcpServers": {
    "refactor-csharp-mcp": {
      "command": "docker",
      "args": [
        "run", "--rm", "-i",
        "--memory=2g",
        "--cpus=1",
        "refactor-csharp-mcp:latest"
      ]
    }
  }
}
```

---

## Management Commands

### Server Management

```bash
# Enable server
docker mcp server enable refactor-csharp-mcp

# Disable server
docker mcp server disable refactor-csharp-mcp

# List all enabled servers
docker mcp server ls

# Inspect server configuration
docker mcp server inspect refactor-csharp-mcp

# Reset all servers (disable all)
docker mcp server reset
```

### Catalog Management

```bash
# List catalogs
docker mcp catalog ls

# Show catalog contents
docker mcp catalog show local-dev

# Add server to catalog manually
docker mcp catalog add local-dev refactor-csharp-mcp ./docker-mcp.yaml --force

# Remove server from catalog
docker mcp catalog rm local-dev refactor-csharp-mcp

# Create new catalog
docker mcp catalog create my-catalog

# Export catalog
docker mcp catalog export local-dev > my-catalog.yaml

# Import catalog
docker mcp catalog import my-catalog.yaml
```

### Gateway Management

```bash
# Start gateway
docker mcp gateway run

# Start gateway on specific port (if HTTP transport)
docker mcp gateway run --port 8080

# View gateway help
docker mcp gateway --help
```

### Configuration Management

```bash
# Read current configuration
docker mcp config read

# Write configuration
docker mcp config write '<yaml-config>'
```

### Rollback and Unregistration

**Unregister server from gateway:**

```bash
# Disable server (keeps in catalog)
docker mcp server disable refactor-csharp-mcp

# Remove from catalog entirely
docker mcp catalog rm local-dev refactor-csharp-mcp

# Verify removal
docker mcp server ls
```

**Rollback to previous version:**

```bash
# 1. Tag desired version
docker tag refactor-csharp-mcp:1.0.0 refactor-csharp-mcp:rollback

# 2. Update catalog to use rollback tag
# Edit docker-mcp.yaml to use :rollback or :1.0.0
# Or set MCP_VERSION environment variable:
export MCP_VERSION=1.0.0

# 3. Re-register with updated version
docker mcp catalog add local-dev refactor-csharp-mcp docker-mcp.yaml --force

# 4. Restart gateway
docker mcp gateway stop
docker mcp gateway run
```

**Complete cleanup:**

```bash
# Stop all running containers
docker stop $(docker ps -q --filter ancestor=refactor-csharp-mcp)

# Remove from gateway
docker mcp server disable refactor-csharp-mcp
docker mcp catalog rm local-dev refactor-csharp-mcp

# Remove Docker images
docker rmi refactor-csharp-mcp:latest
docker rmi refactor-csharp-mcp:1.0.0

# Remove dangling images
docker image prune -f
```

**Emergency rollback procedure:**

If the server is causing issues, perform quick rollback:

```bash
# 1. Disable immediately
docker mcp server disable refactor-csharp-mcp

# 2. Restart gateway without the server
docker mcp gateway stop
docker mcp gateway run

# 3. Investigate issues in logs
docker logs $(docker ps -q --filter ancestor=refactor-csharp-mcp)

# 4. Once fixed, re-enable
docker mcp server enable refactor-csharp-mcp
```

---

## Configuration

### docker-mcp.yaml

The catalog definition file (`docker-mcp.yaml`) defines server metadata:

```yaml
apiVersion: mcp/v1
kind: Server
metadata:
  name: refactor-csharp-mcp
  displayName: RefactorCsharp MCP Server
  description: Roslyn-based C# refactoring capabilities for AI clients
  vendor: RefactorCsharpMCP
  version: 1.0.0
  homepage: https://github.com/sethb75/RefactorCsharpMCP
  documentation: https://github.com/sethb75/RefactorCsharpMCP#readme
  license: Apache-2.0

spec:
  container:
    image: refactor-csharp-mcp:${MCP_VERSION:-latest}
    command: ["dotnet", "RefactorCsharpMCP.Server.dll"]

  transport: stdio

  resources:
    limits:
      cpu: "1000m"      # 1 CPU
      memory: "2Gi"     # 2GB RAM
    requests:
      cpu: "250m"       # Minimum 250 millicores
      memory: "512Mi"   # Minimum 512MB RAM

  capabilities:
    tools: true
    resources: false
    prompts: false

  environment:
    - name: DOTNET_SYSTEM_GLOBALIZATION_INVARIANT
      value: "1"
    - name: DOTNET_RUNNING_IN_CONTAINER
      value: "true"

  tools:
    - name: extract_method
      description: Extract selected code into a new private method
    - name: constructor_injection
      description: Convert method parameters to constructor-injected fields or properties
    # ... (11 tools total)
```

### Environment Variables

The following environment variables configure the MCP server and Docker behavior:

| Variable | Purpose | Default | Required |
|----------|---------|---------|----------|
| `MCP_VERSION` | Docker image version tag used in docker-mcp.yaml | `latest` | No |
| `MCP_CPU_LIMIT` | Maximum CPU allocation for MCP server container | `1000m` (1 CPU) | No |
| `MCP_MEMORY_LIMIT` | Maximum memory allocation for MCP server container | `2Gi` (2GB) | No |
| `MCP_CPU_REQUEST` | Minimum CPU reservation for MCP server container | `250m` | No |
| `MCP_MEMORY_REQUEST` | Minimum memory reservation for MCP server container | `512Mi` | No |
| `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT` | Disables culture-specific formatting (container optimization) | `1` | Yes (in container) |
| `DOTNET_RUNNING_IN_CONTAINER` | Indicates .NET is running in a container | `true` | Yes (in container) |
| `DOTNET_EnableDiagnostics` | Enables .NET diagnostics | `0` | No |
| `ASPNETCORE_ENVIRONMENT` | ASP.NET Core environment name | Not set | No |

**Setting MCP_VERSION:**

The `MCP_VERSION` variable controls which image version the gateway uses:

```bash
# Use specific version
export MCP_VERSION=1.0.0
docker mcp catalog add local-dev refactor-csharp-mcp docker-mcp.yaml

# Use latest (default)
unset MCP_VERSION
docker mcp catalog add local-dev refactor-csharp-mcp docker-mcp.yaml
```

**Setting Resource Limits:**

The `MCP_CPU_*` and `MCP_MEMORY_*` variables allow you to override default resource limits without editing docker-mcp.yaml:

```bash
# Increase resources for large refactoring operations
export MCP_CPU_LIMIT=2000m          # 2 CPUs
export MCP_MEMORY_LIMIT=4Gi         # 4GB RAM
export MCP_CPU_REQUEST=500m         # Reserve 500 millicores
export MCP_MEMORY_REQUEST=1Gi       # Reserve 1GB RAM

# Register with custom resource limits
pwsh ./scripts/register-mcp-gateway.ps1

# Use defaults (reset environment)
unset MCP_CPU_LIMIT MCP_MEMORY_LIMIT MCP_CPU_REQUEST MCP_MEMORY_REQUEST
```

**Resource Limit Guidelines:**
- **Light use** (small files, simple refactorings): Use defaults (1 CPU, 2GB)
- **Medium use** (multiple files, complex refactorings): 1-2 CPUs, 2-4GB
- **Heavy use** (large codebases, bulk operations): 2-4 CPUs, 4-8GB

**Container Environment Variables:**

The `DOTNET_*` variables are set automatically in the Dockerfile and docker-mcp.yaml. Only modify these if customizing container behavior:

```yaml
# In docker-mcp.yaml
environment:
  - name: DOTNET_SYSTEM_GLOBALIZATION_INVARIANT
    value: "1"  # Reduces container size, disables culture-specific formatting
  - name: DOTNET_RUNNING_IN_CONTAINER
    value: "true"  # Optimizes .NET for container environment
```

### Version Management

**Use specific version:**

```bash
# Build with version tag
docker build -t refactor-csharp-mcp:1.0.0 .

# Register specific version
pwsh ./scripts/register-mcp-gateway.ps1 -Version "1.0.0"

# Set version via environment variable
export MCP_VERSION=1.0.0
docker mcp server enable refactor-csharp-mcp
```

**Version environment variable in docker-mcp.yaml:**
```yaml
spec:
  container:
    image: refactor-csharp-mcp:${MCP_VERSION:-latest}
```

---

## Troubleshooting

### Common Issues

#### 1. Server Not Found in Catalog

**Symptoms:**
```bash
$ docker mcp server inspect refactor-csharp-mcp
server "refactor-csharp-mcp" not found in catalog
```

**Solutions:**
```bash
# Re-register the server
pwsh ./scripts/register-mcp-gateway.ps1 -Validate

# Verify catalog file exists
ls docker-mcp.yaml

# Check catalog contents
docker mcp catalog show local-dev
```

#### 2. Gateway Commands Not Found

**Symptoms:**
```bash
$ docker mcp --help
unknown command: mcp
```

**Solutions:**
```bash
# Update Docker Desktop to 4.25+
# Download from: https://www.docker.com/products/docker-desktop/

# Verify version
docker --version

# Check if MCP plugin is enabled in Docker Desktop settings
```

#### 3. Docker Image Not Found

**Symptoms:**
```bash
[ERROR] Image refactor-csharp-mcp:latest not found
```

**Solutions:**
```bash
# Build the image
docker build -t refactor-csharp-mcp:latest .

# Verify image exists
docker images | grep refactor-csharp-mcp

# Pull from registry (if published)
docker pull <registry>/refactor-csharp-mcp:latest
```

#### 4. Registration Failures

**Symptoms:**
- Registration script fails with unclear error messages
- Server doesn't appear in catalog after registration

**Diagnostics:**

Check the registration log file for detailed error information:

```bash
# View registration log (Linux/macOS/WSL)
cat registration.log

# View registration log (Windows PowerShell)
Get-Content registration.log

# View last 50 lines
tail -50 registration.log  # Linux/macOS
Get-Content registration.log -Tail 50  # PowerShell
```

The `registration.log` file is created in the project root and contains timestamped entries for:
- Image verification steps
- Catalog initialization
- Server registration attempts
- Error messages with exit codes
- Gateway validation results

**Common fixes:**
```bash
# Re-run with validation to get detailed diagnostics
pwsh ./scripts/register-mcp-gateway.ps1 -Validate

# Check Docker Desktop is running
docker info

# Verify MCP Gateway is available
docker mcp version

# Force re-registration
docker mcp catalog add local-dev refactor-csharp-mcp ./docker-mcp.yaml --force
```

#### 5. Container Won't Start

**Symptoms:**
- Gateway starts but server doesn't respond
- Connection timeouts in AI client

**Diagnostics:**
```bash
# Test container directly
docker run --rm -i refactor-csharp-mcp:latest

# Check logs
docker ps -a | grep refactor-csharp-mcp
docker logs <container-id>

# Verify health
docker inspect --format='{{.State.Health.Status}}' <container-id>
```

**Common fixes:**
```bash
# Rebuild image
docker build --no-cache -t refactor-csharp-mcp:latest .

# Check for port conflicts (if using HTTP transport)
docker ps

# Verify .NET runtime is working
docker run --rm -i refactor-csharp-mcp:latest dotnet --version
```

#### 5. Permission Denied Errors

**Symptoms:**
```
Error: permission denied while trying to connect to the Docker daemon
```

**Solutions:**
```bash
# Linux: Add user to docker group
sudo usermod -aG docker $USER
# Log out and back in

# Windows: Ensure Docker Desktop is running
# Check Docker Desktop settings -> Resources

# Verify Docker socket permissions
ls -la /var/run/docker.sock
```

#### 6. Registration Script Fails

**Windows PowerShell:**
```powershell
# Check execution policy
Get-ExecutionPolicy

# If Restricted, set to RemoteSigned
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser

# Run with validation
pwsh ./scripts/register-mcp-gateway.ps1 -Validate
```

**Linux/macOS:**
```bash
# Ensure script is executable
chmod +x ./scripts/register-mcp-gateway.sh

# Run with verbose output
bash -x ./scripts/register-mcp-gateway.sh
```

### Diagnostic Commands

```bash
# Check Docker daemon
docker info

# Verify MCP Gateway status
docker mcp catalog ls
docker mcp server ls

# Test container manually
echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}' | docker run -i --rm refactor-csharp-mcp:latest

# Inspect image
docker inspect refactor-csharp-mcp:latest

# Check resource usage
docker stats --no-stream
```

### Logging

**Increase log verbosity:**

Modify `appsettings.json` or set environment variable:
```bash
docker run -i --rm -e ASPNETCORE_LOGGING__LOGLEVEL__DEFAULT=Debug refactor-csharp-mcp:latest
```

**View container logs:**
```bash
# Real-time logs
docker logs -f <container-id>

# Last 100 lines
docker logs --tail 100 <container-id>

# Since specific time
docker logs --since 10m <container-id>
```

---

## Advanced Topics

### Custom Catalogs

Create a custom catalog for team-specific servers:

```bash
# Create catalog
docker mcp catalog create team-refactoring

# Add servers
docker mcp catalog add team-refactoring refactor-csharp-mcp ./docker-mcp.yaml

# Share catalog
docker mcp catalog export team-refactoring > team-refactoring.yaml
# Distribute team-refactoring.yaml to team members

# Team members import
docker mcp catalog import team-refactoring.yaml
```

**Catalog Naming Conventions:**

Use descriptive, environment-specific catalog names for better organization:

| Catalog Name | Purpose | Example Servers |
|--------------|---------|-----------------|
| `local-dev` | Local development and testing | Latest builds, experimental features |
| `staging` | Pre-production testing | Release candidates, integration testing |
| `production` | Production deployments | Stable versions only |
| `team-<name>` | Team-specific servers | Custom team tools and services |
| `project-<name>` | Project-specific servers | Project-scoped refactoring tools |

**Best Practices:**
- Use lowercase with hyphens (kebab-case)
- Include environment indicator (dev, staging, prod)
- Keep names under 50 characters
- Avoid special characters except hyphens and underscores

**Examples:**
```bash
# Development catalog
docker mcp catalog create local-dev

# Team-specific catalog
docker mcp catalog create team-backend

# Project-specific catalog
docker mcp catalog create project-microservices

# Production catalog
docker mcp catalog create production
```

### Resource Tuning

Adjust resource limits in `docker-mcp.yaml`:

```yaml
resources:
  limits:
    cpu: "2000m"      # 2 CPUs
    memory: "4Gi"     # 4GB RAM
  requests:
    cpu: "500m"       # Minimum 500 millicores
    memory: "1Gi"     # Minimum 1GB RAM
```

### Network Configuration

For HTTP transport (if supported in future):

```yaml
spec:
  transport: http
  network:
    port: 8080
    host: localhost
```

### Security Hardening

**Enable Docker Content Trust:**
```bash
export DOCKER_CONTENT_TRUST=1
docker pull refactor-csharp-mcp:latest
```

**Image Scanning:**
```bash
# Docker Scout
docker scout cves refactor-csharp-mcp:latest

# Trivy
trivy image refactor-csharp-mcp:latest
```

**Runtime Security:**
```yaml
spec:
  container:
    securityContext:
      runAsNonRoot: true
      runAsUser: 1000
      readOnlyRootFilesystem: true
```

(Already implemented in current Dockerfile)

### CI/CD Integration

**GitHub Actions example:**

```yaml
name: Build and Register MCP Server

on:
  push:
    branches: [ main ]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Build Docker image
        run: docker build -t refactor-csharp-mcp:${{ github.sha }} .

      - name: Tag as latest
        run: docker tag refactor-csharp-mcp:${{ github.sha }} refactor-csharp-mcp:latest

      - name: Register with MCP Gateway
        run: ./scripts/register-mcp-gateway.sh latest local-dev true
```

---

## Best Practices

### Development

1. **Use Direct Docker** during development for faster iteration
2. **Enable verbose logging** to debug issues
3. **Test container locally** before registering with gateway
4. **Version your images** with semantic versioning

### Production

1. **Use Docker MCP Gateway** for centralized management
2. **Pin specific versions** instead of `latest`
3. **Enable health checks** and monitoring
4. **Set appropriate resource limits** based on workload
5. **Scan images** for vulnerabilities before deployment
6. **Enable Docker Content Trust** for signed images

### Team Collaboration

1. **Share custom catalogs** with team members
2. **Document configuration** in team wiki
3. **Standardize versions** across team
4. **Use version control** for catalog files

### Performance

1. **Monitor resource usage** with `docker stats`
2. **Adjust limits** based on actual usage patterns
3. **Use caching** for faster builds
4. **Clean up unused images** regularly

```bash
# Remove dangling images
docker image prune

# Remove all unused images
docker image prune -a
```

---

## FAQ

### General

**Q: Do I need Docker Desktop or can I use Docker Engine?**
A: Docker MCP Gateway is currently only available in Docker Desktop 4.25+.

**Q: Can I use multiple MCP servers with the gateway?**
A: Yes! The gateway manages multiple servers. Register each server separately.

**Q: Does the gateway add latency?**
A: Minimal overhead (<10%). Performance testing shows acceptable latency for most use cases.

**Q: Can I use this without Docker?**
A: Yes, you can run the server directly with .NET: `dotnet run --project src/RefactorCsharpMCP.Server`

### Setup

**Q: Which integration option should I use?**
A: Use Docker MCP Gateway for production and teams. Use Direct Docker for development and testing.

**Q: How do I update to a new version?**
A: Build new image, tag with version, re-register with gateway using the registration script.

**Q: Can I run multiple versions simultaneously?**
A: Yes, but only one can be enabled at a time per catalog. Use different catalogs for multiple versions.

### Troubleshooting

**Q: Gateway shows "server not found" but catalog lists it**
A: The server may not be enabled. Run: `docker mcp server enable refactor-csharp-mcp`

**Q: Container starts but AI client can't connect**
A: Verify transport is `stdio` in client configuration. Check gateway is running: `docker mcp gateway run`

**Q: How do I debug connection issues?**
A: Test container manually: `docker run --rm -i refactor-csharp-mcp:latest` and send MCP initialize message.

### Advanced

**Q: Can I customize resource limits per invocation?**
A: Yes, modify `docker-mcp.yaml` and re-register. Gateway enforces limits defined in catalog.

**Q: How do I share my server with others?**
A: Export catalog with `docker mcp catalog export`, share the YAML file, and publish the Docker image.

**Q: Can I use this with Kubernetes?**
A: The Docker image can run in Kubernetes, but MCP Gateway integration is Docker Desktop specific.

---

## Additional Resources

- **Project Repository:** https://github.com/sethb75/RefactorCsharpMCP
- **Issue Tracker:** https://github.com/sethb75/RefactorCsharpMCP/issues
- **Docker MCP Documentation:** https://docs.docker.com/ai/mcp-catalog-and-toolkit/
- **MCP Specification:** https://modelcontextprotocol.io/
- **Test Results:** [DOCKER-MCP-TOOLKIT-TESTS.md](../DOCKER-MCP-TOOLKIT-TESTS.md)

---

## Support

For issues specific to:
- **RefactorCsharpMCP:** Open an issue on GitHub
- **Docker MCP Gateway:** Check Docker Desktop documentation
- **AI Client Configuration:** Consult client-specific documentation

---

**Last Updated:** 2025-11-06
**Version:** 1.0.0
**Status:** Production Ready
