# Custom Docker MCP Catalog Setup

This directory contains the configuration for adding RefactorCsharpMCP to a custom Docker MCP Toolkit catalog.

## Overview

Instead of running RefactorCsharpMCP as a separate MCP server, this approach integrates it into the Docker MCP Toolkit gateway, making all tools accessible through a single `MCP_DOCKER` connection.

## Files

- `server.yaml` - Server definition for RefactorCsharpMCP (used for official registry submission)
- `catalog.yaml` - Custom catalog definition including RefactorCsharpMCP
- `setup-catalog.ps1` - PowerShell script to set up the custom catalog
- `README.md` - This file

## Setup Process

### Prerequisites

1. Docker Desktop 4.42.0+ running with MCP Toolkit enabled
2. RefactorCsharpMCP Docker image built (`refactor-csharp-mcp:latest`)

### Automated Setup

Run the setup script:

```powershell
.\setup-catalog.ps1
```

This script will:
1. Check Docker Desktop is running
2. Verify the RefactorCsharpMCP image exists (builds if needed)
3. Fork the official Docker MCP catalog
4. Export the catalog to YAML
5. Import the custom catalog with RefactorCsharpMCP

### Manual Setup

If you prefer manual setup:

```bash
# 1. Fork the official catalog
docker mcp catalog fork docker-mcp my-mcp-catalog

# 2. Export to see the format (optional)
docker mcp catalog show my-mcp-catalog --format yaml > exported-catalog.yaml

# 3. Import the custom catalog
docker mcp catalog import catalog.yaml

# 4. List catalogs to verify
docker mcp catalog ls

# 5. Show servers in the catalog
docker mcp catalog show my-mcp-catalog
```

## Configuration

### Update Claude Code MCP Configuration

After setting up the catalog, update your Claude Code MCP configuration (`%USERPROFILE%\.claude\mcp_servers.json` on Windows):

```json
{
  "mcpServers": {
    "MCP_DOCKER": {
      "command": "docker",
      "args": ["mcp", "gateway", "run", "--catalog", "my-mcp-catalog"],
      "env": {
        "LOCALAPPDATA": "${LOCALAPPDATA}",
        "ProgramData": "${ProgramData}",
        "ProgramFiles": "${ProgramFiles}"
      },
      "type": "stdio"
    }
  }
}
```

**Key change:** Added `"--catalog", "my-mcp-catalog"` to the args array.

### Verify Connection

After updating the configuration and restarting Claude Code:

```bash
# Check MCP client status
docker mcp client ls

# List enabled servers
docker mcp server ls
```

## Available Tools

After setup, RefactorCsharpMCP tools will be available through the MCP_DOCKER gateway:

- `mcp__MCP_DOCKER__extract_method`
- `mcp__MCP_DOCKER__constructor_injection`
- `mcp__MCP_DOCKER__make_field_readonly`
- `mcp__MCP_DOCKER__safe_delete_method`
- `mcp__MCP_DOCKER__extract_class`
- `mcp__MCP_DOCKER__remove_unused_usings`
- `mcp__MCP_DOCKER__inline_method`

Plus all the existing Docker MCP Toolkit servers (fetch, git, github-official, etc.)

## Benefits

1. **Single Gateway**: All MCP tools accessible through one connection
2. **Unified Management**: Use `docker mcp` CLI to manage all servers
3. **Consistent Security**: Same security model as official Docker servers
4. **Easy Updates**: Update the catalog to add/remove servers

## Troubleshooting

### Catalog Import Fails

If the catalog import fails, try:

```bash
# Reset the catalog system
docker mcp catalog reset

# Re-fork the official catalog
docker mcp catalog fork docker-mcp my-mcp-catalog

# Try importing again
docker mcp catalog import catalog.yaml
```

### Tools Not Appearing

1. Verify the catalog is active:
   ```bash
   docker mcp catalog show my-mcp-catalog
   ```

2. Check the gateway is using the custom catalog:
   ```bash
   # In your MCP config, ensure --catalog flag is set
   "args": ["mcp", "gateway", "run", "--catalog", "my-mcp-catalog"]
   ```

3. Restart Claude Code completely

### Image Not Found

If Docker can't find the RefactorCsharpMCP image:

```bash
# Build the image (from the repository root)
cd /path/to/RefactorCsharpMCP
docker build -t refactor-csharp-mcp:latest .

# Verify it exists
docker images refactor-csharp-mcp
```

## Next Steps

See issue #[NUMBER] for submitting RefactorCsharpMCP to the official Docker MCP Registry for broader community access.

## References

- [Docker MCP Catalog Documentation](https://docs.docker.com/ai/mcp-catalog-and-toolkit/catalog/)
- [Docker MCP Toolkit](https://docs.docker.com/ai/mcp-catalog-and-toolkit/toolkit/)
- [MCP Registry GitHub](https://github.com/docker/mcp-registry)
