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

# Validate VERSION format
if ! [[ "$VERSION" =~ ^[a-zA-Z0-9._-]+$ ]]; then
    echo -e "\033[0;31m[ERROR] Invalid version format: $VERSION\033[0m"
    echo -e "\033[1;33mVersion must contain only alphanumeric characters, dots, underscores, and hyphens\033[0m"
    exit 1
fi

# Validate CATALOG format
if ! [[ "$CATALOG" =~ ^[a-zA-Z0-9_-]+$ ]]; then
    echo -e "\033[0;31m[ERROR] Invalid catalog name: $CATALOG\033[0m"
    echo -e "\033[1;33mCatalog must contain only alphanumeric characters, underscores, and hyphens\033[0m"
    exit 1
fi

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
LOG_FILE="$PROJECT_ROOT/registration.log"

# Logging function
log() {
    echo "[$(date +'%Y-%m-%d %H:%M:%S')] $1" >> "$LOG_FILE"
}

# Check if script is executable (helpful warning for users)
if [ ! -x "$0" ]; then
    echo -e "${YELLOW}[WARN] Script may not have execute permissions${NC}"
    echo -e "${YELLOW}Run: chmod +x $0${NC}"
    echo ""
fi

echo -e "${CYAN}RefactorCsharpMCP - Docker MCP Gateway Registration${NC}"
echo -e "${CYAN}====================================================${NC}"
echo ""

log "==== Gateway Registration Started ===="
log "Version: $VERSION"
log "Catalog: $CATALOG"
log "Validate: $VALIDATE"

# Step 1: Validate Docker MCP Gateway support
if [ "$VALIDATE" = "true" ]; then
    echo -e "${YELLOW}Validating Docker Desktop MCP Gateway...${NC}"
    log "Validating Docker MCP Gateway support"

    if ! command -v docker &> /dev/null; then
        echo -e "${RED}[ERROR] Docker not installed${NC}"
        log "ERROR: Docker not installed"
        exit 1
    fi

    docker --version
    echo -e "${GREEN}[OK] Docker installed${NC}"
    log "SUCCESS: Docker installed"

    if ! docker mcp version &> /dev/null 2>&1; then
        # Check if Docker Desktop is running
        if ! docker info &> /dev/null 2>&1; then
            DOCKER_VERSION="Docker not running"
        else
            DOCKER_VERSION=$(docker version --format '{{.Server.Version}}' 2>/dev/null || echo "unknown")
        fi
        echo -e "${RED}[ERROR] Docker MCP Gateway not available${NC}"
        echo -e "${YELLOW}Current Docker version: $DOCKER_VERSION${NC}"
        echo -e "${YELLOW}MCP Gateway requires Docker Desktop 28.5.1+ or equivalent${NC}"
        echo -e "${YELLOW}Please update Docker Desktop from: https://www.docker.com/products/docker-desktop${NC}"
        log "ERROR: Docker MCP Gateway not available (Docker version: $DOCKER_VERSION)"
        exit 1
    fi
    echo -e "${GREEN}[OK] Docker MCP Gateway detected${NC}"
    log "SUCCESS: Docker MCP Gateway detected"
fi

# Step 2: Verify image exists
echo ""
echo -e "${YELLOW}Verifying Docker image...${NC}"
log "Verifying Docker image: refactor-csharp-mcp:$VERSION"
IMAGE_ID=$(docker images -q "refactor-csharp-mcp:$VERSION" 2>/dev/null)
if [ -z "$IMAGE_ID" ]; then
    echo -e "${RED}[ERROR] Image refactor-csharp-mcp:$VERSION not found${NC}"
    echo -e "${YELLOW}Build the image first:${NC}"
    echo -e "${WHITE}  docker build -t refactor-csharp-mcp:$VERSION .${NC}"
    echo -e "${WHITE}  or${NC}"
    echo -e "${WHITE}  ./scripts/deploy-docker.sh $VERSION${NC}"
    log "ERROR: Image not found: refactor-csharp-mcp:$VERSION"
    exit 1
fi
echo -e "${GREEN}[OK] Image found: refactor-csharp-mcp:$VERSION${NC}"
log "SUCCESS: Image found: $IMAGE_ID"

# Step 3: Check if docker-mcp.yaml exists
echo ""
echo -e "${YELLOW}Checking catalog definition...${NC}"
log "Checking catalog definition file"
CATALOG_FILE="$PROJECT_ROOT/docker-mcp.yaml"
if [ ! -f "$CATALOG_FILE" ]; then
    echo -e "${RED}[ERROR] $CATALOG_FILE not found${NC}"
    echo -e "${YELLOW}Expected location: $CATALOG_FILE${NC}"
    echo -e "${YELLOW}Please ensure docker-mcp.yaml exists in the project root${NC}"
    log "ERROR: Catalog file not found: $CATALOG_FILE"
    exit 1
fi
echo -e "${GREEN}[OK] Catalog definition found${NC}"
log "SUCCESS: Catalog definition found at $CATALOG_FILE"

# Step 4: Initialize catalog if needed
echo ""
echo -e "${YELLOW}Checking catalog system...${NC}"
log "Checking if catalog system is initialized"
if docker mcp catalog ls &> /dev/null; then
    echo -e "${GREEN}[OK] Catalog system already initialized${NC}"
    log "INFO: Catalog system already initialized"
else
    echo -e "${YELLOW}[INFO] Initializing catalog system...${NC}"
    log "INFO: Initializing catalog system"
    if docker mcp catalog init &> /dev/null; then
        echo -e "${GREEN}[OK] Catalog system initialized${NC}"
        log "SUCCESS: Catalog system initialized"
    else
        echo -e "${YELLOW}[WARN] Catalog initialization failed, continuing anyway${NC}"
        log "WARNING: Catalog initialization failed"
    fi
fi

# Step 5: Add server to catalog
echo ""
echo -e "${YELLOW}Registering server in catalog '$CATALOG'...${NC}"
log "Adding server to catalog: $CATALOG"
OUTPUT=$(docker mcp catalog add "$CATALOG" refactor-csharp-mcp "$CATALOG_FILE" --force 2>&1)
EXIT_CODE=$?
if [ $EXIT_CODE -eq 0 ]; then
    echo -e "${GREEN}[OK] Server added to catalog${NC}"
    log "SUCCESS: Server added to catalog $CATALOG"
else
    echo -e "${RED}[ERROR] Failed to register server in catalog${NC}"
    echo -e "${RED}Error details: $OUTPUT${NC}"
    log "ERROR: Failed to add server to catalog. Exit code: $EXIT_CODE"
    log "ERROR: $OUTPUT"
    exit 1
fi

# Step 6: Enable the server
echo ""
echo -e "${YELLOW}Enabling MCP server...${NC}"
log "Enabling MCP server: refactor-csharp-mcp"
OUTPUT=$(docker mcp server enable refactor-csharp-mcp 2>&1)
EXIT_CODE=$?
if [ $EXIT_CODE -eq 0 ]; then
    echo -e "${GREEN}[OK] Server enabled${NC}"
    log "SUCCESS: Server enabled"
else
    echo -e "${RED}[ERROR] Failed to enable server${NC}"
    echo -e "${RED}Error details: $OUTPUT${NC}"
    log "ERROR: Failed to enable server. Exit code: $EXIT_CODE"
    log "ERROR: $OUTPUT"
    exit 1
fi

# Step 7: Verify registration
echo ""
echo -e "${YELLOW}Verifying registration...${NC}"
log "Verifying server registration"
if OUTPUT=$(docker mcp server inspect refactor-csharp-mcp 2>&1); then
    echo ""
    echo -e "${GREEN}[OK] Registration verified${NC}"
    log "SUCCESS: Registration verified"
else
    echo -e "${YELLOW}[WARN] Could not verify server registration${NC}"
    echo -e "${YELLOW}Warning details: $OUTPUT${NC}"
    log "WARNING: Could not verify registration"
fi

# Summary
echo ""
log "==== Gateway Registration Complete ===="
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
