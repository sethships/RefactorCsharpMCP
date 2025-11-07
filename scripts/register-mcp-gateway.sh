#!/bin/bash
#
# Register RefactorCsharpMCP with Docker MCP Gateway
#
# Usage:
#   ./register-mcp-gateway.sh [VERSION] [CATALOG] [VALIDATE]
#
# Arguments:
#   VERSION   - Docker image version tag (default: "latest")
#   CATALOG   - Catalog name (default: "local-dev")
#   VALIDATE  - Set to "true" to validate gateway support
#
# Examples:
#   ./register-mcp-gateway.sh
#   ./register-mcp-gateway.sh 1.0.0 local-dev true
#

set -e

# Arguments with defaults
VERSION="${1:-latest}"
CATALOG="${2:-local-dev}"
VALIDATE="${3:-false}"

# Colors
CYAN='\033[0;36m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
WHITE='\033[0;37m'
NC='\033[0m' # No Color

# Get script directory and project root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

echo -e "${CYAN}RefactorCsharpMCP - Docker MCP Gateway Registration${NC}"
echo -e "${CYAN}====================================================${NC}"
echo ""

# Step 1: Validate Docker MCP Gateway support
if [ "$VALIDATE" = "true" ]; then
    echo -e "${YELLOW}Validating Docker Desktop MCP Gateway...${NC}"

    if ! command -v docker &> /dev/null; then
        echo -e "${RED}[ERROR] Docker not installed${NC}"
        exit 1
    fi

    docker --version
    echo -e "${GREEN}[OK] Docker installed${NC}"

    if ! docker mcp version &> /dev/null 2>&1; then
        DOCKER_VERSION=$(docker version --format '{{.Server.Version}}' 2>/dev/null || echo "unknown")
        echo -e "${RED}[ERROR] Docker MCP Gateway not available${NC}"
        echo -e "${YELLOW}Current Docker version: $DOCKER_VERSION${NC}"
        echo -e "${YELLOW}MCP Gateway requires Docker Desktop 28.5.1+ or equivalent${NC}"
        echo -e "${YELLOW}Please update Docker Desktop from: https://www.docker.com/products/docker-desktop${NC}"
        exit 1
    fi
    echo -e "${GREEN}[OK] Docker MCP Gateway detected${NC}"
fi

# Step 2: Verify image exists
echo ""
echo -e "${YELLOW}Verifying Docker image...${NC}"
IMAGE_ID=$(docker images -q "refactor-csharp-mcp:$VERSION" 2>/dev/null)
if [ -z "$IMAGE_ID" ]; then
    echo -e "${RED}[ERROR] Image refactor-csharp-mcp:$VERSION not found${NC}"
    echo -e "${YELLOW}Build the image first:${NC}"
    echo -e "${WHITE}  docker build -t refactor-csharp-mcp:$VERSION .${NC}"
    echo -e "${WHITE}  or${NC}"
    echo -e "${WHITE}  ./scripts/deploy-docker.sh $VERSION${NC}"
    exit 1
fi
echo -e "${GREEN}[OK] Image found: refactor-csharp-mcp:$VERSION${NC}"

# Step 3: Check if docker-mcp.yaml exists
echo ""
echo -e "${YELLOW}Checking catalog definition...${NC}"
CATALOG_FILE="$PROJECT_ROOT/docker-mcp.yaml"
if [ ! -f "$CATALOG_FILE" ]; then
    echo -e "${RED}[ERROR] $CATALOG_FILE not found${NC}"
    echo -e "${YELLOW}Expected location: $CATALOG_FILE${NC}"
    echo -e "${YELLOW}Please ensure docker-mcp.yaml exists in the project root${NC}"
    exit 1
fi
echo -e "${GREEN}[OK] Catalog definition found${NC}"

# Step 4: Initialize catalog if needed
echo ""
echo -e "${YELLOW}Initializing catalog system...${NC}"
if docker mcp catalog ls &> /dev/null; then
    echo -e "${GREEN}[OK] Catalog system initialized${NC}"
else
    echo -e "${YELLOW}[WARN] Catalog system may need initialization${NC}"
    docker mcp catalog init &> /dev/null || true
fi

# Step 5: Add server to catalog
echo ""
echo -e "${YELLOW}Registering server in catalog '$CATALOG'...${NC}"
if OUTPUT=$(docker mcp catalog add "$CATALOG" refactor-csharp-mcp "$CATALOG_FILE" --force 2>&1); then
    echo -e "${GREEN}[OK] Server added to catalog${NC}"
else
    echo -e "${RED}[ERROR] Failed to register server in catalog${NC}"
    echo -e "${RED}Error details: $OUTPUT${NC}"
    exit 1
fi

# Step 6: Enable the server
echo ""
echo -e "${YELLOW}Enabling MCP server...${NC}"
if OUTPUT=$(docker mcp server enable refactor-csharp-mcp 2>&1); then
    echo -e "${GREEN}[OK] Server enabled${NC}"
else
    echo -e "${RED}[ERROR] Failed to enable server${NC}"
    echo -e "${RED}Error details: $OUTPUT${NC}"
    exit 1
fi

# Step 7: Verify registration
echo ""
echo -e "${YELLOW}Verifying registration...${NC}"
if OUTPUT=$(docker mcp server inspect refactor-csharp-mcp 2>&1); then
    echo ""
    echo -e "${GREEN}[OK] Registration verified${NC}"
else
    echo -e "${YELLOW}[WARN] Could not verify server registration${NC}"
    echo -e "${YELLOW}Warning details: $OUTPUT${NC}"
fi

# Summary
echo ""
echo -e "${CYAN}====================================================${NC}"
echo -e "${GREEN}Registration complete!${NC}"
echo ""
echo -e "${CYAN}Next steps:${NC}"
echo -e "${WHITE}  1. View catalog:  docker mcp catalog show $CATALOG${NC}"
echo -e "${WHITE}  2. List servers:  docker mcp server ls${NC}"
echo -e "${WHITE}  3. Start gateway: docker mcp gateway run${NC}"
echo ""
echo -e "${CYAN}Configure Claude Desktop:${NC}"
echo -e "${WHITE}  {${NC}"
echo -e "${WHITE}    \"mcpServers\": {${NC}"
echo -e "${WHITE}      \"refactor-csharp-mcp\": {${NC}"
echo -e "${WHITE}        \"command\": \"docker\",${NC}"
echo -e "${WHITE}        \"args\": [\"mcp\", \"gateway\", \"run\"]${NC}"
echo -e "${WHITE}      }${NC}"
echo -e "${WHITE}    }${NC}"
echo -e "${WHITE}  }${NC}"
