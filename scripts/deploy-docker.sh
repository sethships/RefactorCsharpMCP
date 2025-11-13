#!/bin/bash

#
# Deploy RefactorCsharpMCP Docker image with comprehensive validation and security scanning
#
# Usage:
#   ./deploy-docker.sh [OPTIONS]
#
# Options:
#   -v, --version VERSION       Version tag for the Docker image (default: latest)
#   -s, --security              Run security vulnerability scans
#   -t, --test                  Run post-deployment validation tests
#   --skip-tests                Skip pre-deployment test suite
#   --skip-security             Skip security scanning (not recommended)
#   -c, --clean                 Clean up all existing containers and images before deployment
#   -p, --push                  Push image to registry after build
#   -r, --registry REGISTRY     Docker registry to push to
#   --register-gateway          Register with Docker MCP Gateway after deployment
#   --catalog CATALOG           Catalog name for gateway registration (default: local-dev)
#   -h, --help                  Show this help message
#
# Examples:
#   ./deploy-docker.sh -v 0.4.0 -s -t
#   ./deploy-docker.sh --skip-security
#   ./deploy-docker.sh -c -v latest  # Clean up and deploy
#   ./deploy-docker.sh -v 0.4.0 -p -r myregistry.io/myuser
#   ./deploy-docker.sh -v 1.0.0 --register-gateway
#   ./deploy-docker.sh --register-gateway --catalog production
#

# Require Bash 4.0+ for associative arrays and other features
if [ "${BASH_VERSION%%.*}" -lt 4 ]; then
    echo "Error: Bash 4.0 or higher required (found: $BASH_VERSION)" >&2
    exit 1
fi

set -e  # Exit on error

# Default values
VERSION="latest"
IMAGE_NAME="refactor-csharp-mcp"
SECURITY_SCAN=false
RUN_TESTS=false
SKIP_TESTS=false
SKIP_SECURITY=false
CLEAN=false
PUSH_IMAGE=false
REGISTRY=""
REGISTER_GATEWAY=false
CATALOG="local-dev"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;90m'
NC='\033[0m' # No Color

# Get script directory and project root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
LOG_FILE="$PROJECT_ROOT/deployment.log"
START_TIME=$(date +%s)

# Functions
log() {
    echo "[$(date +'%Y-%m-%d %H:%M:%S')] $1" >> "$LOG_FILE"
}

header() {
    echo ""
    echo -e "${CYAN}==== $1 ====${NC}"
    log "==== $1 ===="
}

success() {
    echo -e "${GREEN}✓ $1${NC}"
    log "SUCCESS: $1"
}

error() {
    echo -e "${RED}✗ $1${NC}"
    log "ERROR: $1"
}

warning() {
    echo -e "${YELLOW}⚠ $1${NC}"
    log "WARNING: $1"
}

info() {
    echo -e "${GRAY}  $1${NC}"
    log "INFO: $1"
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -v|--version)
            VERSION="$2"
            shift 2
            ;;
        -s|--security)
            SECURITY_SCAN=true
            shift
            ;;
        -t|--test)
            RUN_TESTS=true
            shift
            ;;
        --skip-tests)
            SKIP_TESTS=true
            shift
            ;;
        --skip-security)
            SKIP_SECURITY=true
            shift
            ;;
        -c|--clean)
            CLEAN=true
            shift
            ;;
        -p|--push)
            PUSH_IMAGE=true
            shift
            ;;
        -r|--registry)
            REGISTRY="$2"
            shift 2
            ;;
        --register-gateway)
            REGISTER_GATEWAY=true
            shift
            ;;
        --catalog)
            CATALOG="$2"
            shift 2
            ;;
        -h|--help)
            head -n 30 "$0" | tail -n +3
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Initialize log
header "Docker Deployment Started"
info "Version: $VERSION"
info "Project Root: $PROJECT_ROOT"
info "Log File: $LOG_FILE"

# Step 1: Pre-deployment validation
header "Pre-Deployment Validation"

# Check Docker
info "Checking Docker installation..."
if ! command -v docker &> /dev/null; then
    error "Docker is not installed or not in PATH"
    exit 1
fi
DOCKER_VERSION=$(docker --version)
success "Docker found: $DOCKER_VERSION"

# Check .NET SDK
info "Checking .NET SDK..."
if ! command -v dotnet &> /dev/null; then
    error ".NET SDK is not installed or not in PATH"
    exit 1
fi
DOTNET_VERSION=$(dotnet --version)
success ".NET SDK found: $DOTNET_VERSION"

# Change to project directory
cd "$PROJECT_ROOT"

# Step 1.5: Clean up existing containers and images (if requested)
if [ "$CLEAN" = true ]; then
    header "Cleaning Up Existing Containers and Images"

    # Find all containers (running and stopped) - search by command pattern
    # This catches containers even if the image was rebuilt and only shows as image ID
    info "Finding all RefactorCsharpMCP containers..."
    ALL_CONTAINERS=$(docker ps -a --format "{{.ID}} {{.Status}}" 2>/dev/null | while read -r id status rest; do
        cmd=$(docker inspect --format='{{.Config.Cmd}}' "$id" 2>/dev/null)
        if [[ "$cmd" =~ RefactorCsha ]]; then
            echo "$id $status"
        fi
    done)

    if [ -n "$ALL_CONTAINERS" ]; then
        CONTAINER_IDS=()
        RUNNING_COUNT=0
        STOPPED_COUNT=0

        while IFS= read -r line; do
            if [[ $line =~ ^([a-f0-9]+)[[:space:]](.*)$ ]]; then
                CONTAINER_ID="${BASH_REMATCH[1]}"
                STATUS="${BASH_REMATCH[2]}"
                CONTAINER_IDS+=("$CONTAINER_ID")

                if [[ $STATUS =~ Up ]]; then
                    ((RUNNING_COUNT++))
                else
                    ((STOPPED_COUNT++))
                fi
            fi
        done <<< "$ALL_CONTAINERS"

        if [ ${#CONTAINER_IDS[@]} -gt 0 ]; then
            info "Found ${#CONTAINER_IDS[@]} container(s): $RUNNING_COUNT running, $STOPPED_COUNT stopped"

            # Stop running containers with timeout and fallback to kill
            if [ $RUNNING_COUNT -gt 0 ]; then
                info "Stopping running containers (10 second timeout)..."

                # Try stop with timeout
                if timeout 15 docker stop --time 10 "${CONTAINER_IDS[@]}" > /dev/null 2>&1; then
                    success "Stopped $RUNNING_COUNT running container(s)"
                else
                    warning "Stop command timed out or failed, forcing kill..."
                    if docker kill "${CONTAINER_IDS[@]}" > /dev/null 2>&1; then
                        success "Force killed $RUNNING_COUNT container(s)"
                    else
                        error "Failed to kill containers"
                        warning "Some containers may require manual cleanup via Docker Desktop"
                    fi
                fi
            fi

            # Remove all containers
            info "Removing containers..."
            if docker rm -f "${CONTAINER_IDS[@]}" > /dev/null 2>&1; then
                success "Removed ${#CONTAINER_IDS[@]} container(s)"
            else
                error "Failed to remove some containers"
                warning "Check Docker Desktop for remaining containers"
            fi
        else
            info "No containers found using ${IMAGE_NAME}"
        fi
    else
        info "No containers found using ${IMAGE_NAME}"
    fi

    # Remove all images with this name (including untagged/dangling ones from rebuilds)
    info "Removing all ${IMAGE_NAME} images..."
    ALL_IMAGES=$(docker images -a --format "{{.ID}} {{.Repository}}" 2>/dev/null | grep "${IMAGE_NAME}" | awk '{print $1}')

    if [ -n "$ALL_IMAGES" ]; then
        IMAGE_IDS=()
        while IFS= read -r image_id; do
            IMAGE_IDS+=("$image_id")
        done <<< "$ALL_IMAGES"

        if [ ${#IMAGE_IDS[@]} -gt 0 ]; then
            # Get unique image IDs
            UNIQUE_IMAGE_IDS=($(printf '%s\n' "${IMAGE_IDS[@]}" | sort -u))
            info "Found ${#UNIQUE_IMAGE_IDS[@]} image(s) to remove"

            if docker rmi -f "${UNIQUE_IMAGE_IDS[@]}" > /dev/null 2>&1; then
                success "Removed ${#UNIQUE_IMAGE_IDS[@]} image(s)"
            else
                warning "Failed to remove some images, continuing cleanup..."
            fi
        else
            info "No images found with name ${IMAGE_NAME}"
        fi
    else
        info "No images found with name ${IMAGE_NAME}"
    fi

    # Clean up dangling images
    info "Cleaning up dangling images..."
    DANGLING_IMAGES=$(docker images -f "dangling=true" -q 2>/dev/null)
    if [ -n "$DANGLING_IMAGES" ]; then
        IMAGE_COUNT=$(echo "$DANGLING_IMAGES" | wc -l)
        docker rmi $DANGLING_IMAGES > /dev/null 2>&1
        success "Removed $IMAGE_COUNT dangling image(s)"
    else
        info "No dangling images found"
    fi

    CLEANUP_END=$(date +%s)
    CLEANUP_DURATION=$((CLEANUP_END - START_TIME))
    success "Cleanup completed in ${CLEANUP_DURATION} seconds"

    # Check if this is cleanup-only mode by examining if version was explicitly set
    # or if any action flags were specified
    HAS_ACTION_FLAG=false

    # Check if any action flags besides -c/--clean were used
    # We check if we're still using defaults for key parameters
    if [ "$SECURITY_SCAN" = true ] || [ "$RUN_TESTS" = true ] || \
       [ "$SKIP_TESTS" = true ] || [ "$SKIP_SECURITY" = true ] || \
       [ "$PUSH_IMAGE" = true ] || [ "$REGISTER_GATEWAY" = true ]; then
        HAS_ACTION_FLAG=true
    fi

    # Also check if version was explicitly provided (not default)
    # This is tricky in bash, so we'll use a marker approach
    # If user provided ANY other flag, we should deploy
    if [ "${VERSION}" != "latest" ] || [ -n "${REGISTRY}" ]; then
        HAS_ACTION_FLAG=true
    fi

    if [ "$HAS_ACTION_FLAG" = false ]; then
        echo ""
        echo -e "${CYAN}Cleanup-only mode - skipping deployment${NC}"
        echo -e "${GRAY}To deploy after cleanup, specify version or add other flags (e.g., -v latest)${NC}"
        exit 0
    fi
fi

# Step 2: Run tests (unless skipped)
if [ "$SKIP_TESTS" = false ]; then
    header "Running Test Suite"
    info "Running dotnet test..."

    if dotnet test --configuration Release --verbosity minimal 2>&1 | tee -a "$LOG_FILE"; then
        success "Test suite passed"
    else
        error "Tests failed!"
        error "Test suite must pass before deployment"
        exit 1
    fi
else
    warning "Skipping test suite (not recommended for production)"
fi

# Step 3: Clean previous builds
header "Cleaning Previous Builds"
info "Removing old images..."

if docker images -q "${IMAGE_NAME}:${VERSION}" 2>/dev/null | grep -q .; then
    docker rmi -f $(docker images -q "${IMAGE_NAME}:${VERSION}") 2>&1 | tee -a "$LOG_FILE" > /dev/null
    success "Removed previous image: ${IMAGE_NAME}:${VERSION}"
fi

# Step 4: Build Docker image
header "Building Docker Image"
info "Building ${IMAGE_NAME}:${VERSION}..."

BUILD_START=$(date +%s)
if docker build -t "${IMAGE_NAME}:${VERSION}" -t "${IMAGE_NAME}:latest" . 2>&1 | tee -a "$LOG_FILE"; then
    BUILD_END=$(date +%s)
    BUILD_DURATION=$((BUILD_END - BUILD_START))
    success "Image built successfully in ${BUILD_DURATION} seconds"
else
    error "Docker build failed"
    exit 1
fi

# Step 5: Inspect image
header "Image Inspection"
IMAGE_SIZE=$(docker inspect "${IMAGE_NAME}:${VERSION}" | jq -r '.[0].Size' | awk '{printf "%.2f", $1/1024/1024}')
IMAGE_CREATED=$(docker inspect "${IMAGE_NAME}:${VERSION}" | jq -r '.[0].Created')
info "Image Size: ${IMAGE_SIZE} MB"
info "Created: ${IMAGE_CREATED}"

# Step 6: Health check
header "Container Health Check"
info "Starting container for health check..."

CONTAINER_ID=$(docker run -d "${IMAGE_NAME}:${VERSION}" 2>&1)
if [ $? -ne 0 ]; then
    error "Failed to start container"
    exit 1
fi

info "Container ID: $CONTAINER_ID"
sleep 5

HEALTH_STATUS=$(docker inspect --format='{{.State.Health.Status}}' "$CONTAINER_ID" 2>&1 || echo "unknown")
if [[ "$HEALTH_STATUS" =~ ^(healthy|starting)$ ]]; then
    success "Container health check: $HEALTH_STATUS"
else
    warning "Container health status: $HEALTH_STATUS"
fi

CONTAINER_STATUS=$(docker inspect --format='{{.State.Status}}' "$CONTAINER_ID" 2>&1)
if [ "$CONTAINER_STATUS" = "running" ]; then
    success "Container is running"
else
    warning "Container status: $CONTAINER_STATUS"
fi

# Cleanup test container
docker stop "$CONTAINER_ID" 2>&1 | tee -a "$LOG_FILE" > /dev/null
docker rm "$CONTAINER_ID" 2>&1 | tee -a "$LOG_FILE" > /dev/null
info "Test container cleaned up"

# Step 7: Security scanning
# Force security scan for production versions
IS_PRODUCTION=false
if [[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] && [ "$VERSION" != "latest" ]; then
    IS_PRODUCTION=true
fi

if ([ "$SECURITY_SCAN" = true ] || [ "$IS_PRODUCTION" = true ]) && [ "$SKIP_SECURITY" = false ]; then
    if [ "$IS_PRODUCTION" = true ] && [ "$SKIP_SECURITY" = true ]; then
        error "Cannot skip security scanning for production version ($VERSION)"
        exit 1
    fi
    header "Security Scanning"
    if [ "$IS_PRODUCTION" = true ]; then
        info "Production version detected - security scanning is mandatory"
    fi

    # Check for Docker Scout
    info "Checking for Docker Scout..."
    if docker scout version &> /dev/null; then
        info "Running Docker Scout CVE scan..."
        if docker scout cves "${IMAGE_NAME}:${VERSION}" 2>&1 | tee "$PROJECT_ROOT/security-scout.txt"; then
            success "Docker Scout scan completed"
        else
            warning "Docker Scout scan had warnings (check security-scout.txt)"
        fi
    else
        warning "Docker Scout not available, skipping"
    fi

    # Check for Trivy
    info "Checking for Trivy..."
    if command -v trivy &> /dev/null; then
        info "Running Trivy vulnerability scan..."
        if trivy image --severity HIGH,CRITICAL "${IMAGE_NAME}:${VERSION}" 2>&1 | tee "$PROJECT_ROOT/security-trivy.txt"; then
            success "Trivy scan completed"
        else
            warning "Trivy scan found issues (check security-trivy.txt)"
        fi
    else
        warning "Trivy not installed, skipping (install from: https://github.com/aquasecurity/trivy)"
    fi
elif [ "$SKIP_SECURITY" = true ]; then
    warning "Security scanning skipped (not recommended for production)"
fi

# Step 8: Post-deployment testing
if [ "$RUN_TESTS" = true ]; then
    header "Post-Deployment Validation"
    TEST_SCRIPT="$SCRIPT_DIR/test-deployment.sh"
    if [ -f "$TEST_SCRIPT" ]; then
        info "Running validation tests..."
        bash "$TEST_SCRIPT" "$IMAGE_NAME:$VERSION"
    else
        warning "test-deployment.sh not found, skipping validation"
    fi
fi

# Step 9: Push to registry (if requested)
if [ "$PUSH_IMAGE" = true ]; then
    if [ -z "$REGISTRY" ]; then
        warning "Registry not specified, skipping push"
    else
        header "Pushing to Registry"
        REMOTE_TAG="${REGISTRY}/${IMAGE_NAME}:${VERSION}"
        info "Tagging for registry: $REMOTE_TAG"
        docker tag "${IMAGE_NAME}:${VERSION}" "$REMOTE_TAG"

        info "Pushing to $REGISTRY..."
        if docker push "$REMOTE_TAG"; then
            success "Pushed to registry: $REMOTE_TAG"
        else
            error "Failed to push to registry"
            exit 1
        fi
    fi
fi

# Step 10: Register with Docker MCP Gateway (if requested)
if [ "$REGISTER_GATEWAY" = true ]; then
    # Validate version format before registration
    if ! [[ "$VERSION" =~ ^[a-zA-Z0-9._-]+$ ]]; then
        warning "Invalid version format: $VERSION"
        warning "Version must contain only alphanumeric characters, dots, underscores, and hyphens"
        warning "Skipping gateway registration"
    else
        header "Registering with Docker MCP Gateway"
        REGISTER_SCRIPT="$SCRIPT_DIR/register-mcp-gateway.sh"

        if [ ! -f "$REGISTER_SCRIPT" ]; then
            warning "Registration script not found: $REGISTER_SCRIPT"
            warning "Skipping gateway registration"
        else
            info "Running registration script..."
            if bash "$REGISTER_SCRIPT" "$VERSION" "$CATALOG" "true" 2>&1 | tee -a "$LOG_FILE"; then
                success "Server registered with Docker MCP Gateway"
                info "Catalog: $CATALOG"
                info "Use 'docker mcp server ls' to verify"
            else
                warning "Registration completed with warnings"
                info "You can manually register later with:"
                info "  bash $REGISTER_SCRIPT $VERSION"
            fi
        fi
    fi
fi

# Final summary
END_TIME=$(date +%s)
DURATION=$((END_TIME - START_TIME))

header "Deployment Summary"
success "Image: ${IMAGE_NAME}:${VERSION}"
success "Size: ${IMAGE_SIZE} MB"
success "Total Time: ${DURATION} seconds"
info "Log file: $LOG_FILE"

echo ""
echo -e "${GREEN}Deployment completed successfully!${NC}"
echo -e "${CYAN}To run the container:${NC}"
echo -e "  docker run --rm -i ${IMAGE_NAME}:${VERSION}"
echo ""
echo -e "${CYAN}To use with Claude Code, add to MCP configuration:${NC}"
cat << EOF
{
  "mcpServers": {
    "refactor-csharp-mcp": {
      "command": "docker",
      "args": ["run", "--rm", "-i", "${IMAGE_NAME}:${VERSION}"],
      "type": "stdio"
    }
  }
}
EOF
