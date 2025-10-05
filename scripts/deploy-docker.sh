#!/bin/bash

#
# Deploy RefactorCsharpMCP Docker image with comprehensive validation and security scanning
#
# Usage:
#   ./deploy-docker.sh [OPTIONS]
#
# Options:
#   -v, --version VERSION    Version tag for the Docker image (default: latest)
#   -s, --security          Run security vulnerability scans
#   -t, --test              Run post-deployment validation tests
#   --skip-tests            Skip pre-deployment test suite
#   --skip-security         Skip security scanning (not recommended)
#   -p, --push              Push image to registry after build
#   -r, --registry REGISTRY Docker registry to push to
#   -h, --help              Show this help message
#
# Examples:
#   ./deploy-docker.sh -v 0.4.0 -s -t
#   ./deploy-docker.sh --skip-security
#   ./deploy-docker.sh -v 0.4.0 -p -r myregistry.io/myuser
#

set -e  # Exit on error

# Default values
VERSION="latest"
IMAGE_NAME="refactor-csharp-mcp"
SECURITY_SCAN=false
RUN_TESTS=false
SKIP_TESTS=false
SKIP_SECURITY=false
PUSH_IMAGE=false
REGISTRY=""

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
        -p|--push)
            PUSH_IMAGE=true
            shift
            ;;
        -r|--registry)
            REGISTRY="$2"
            shift 2
            ;;
        -h|--help)
            head -n 25 "$0" | tail -n +3
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
if [ "$SECURITY_SCAN" = true ] && [ "$SKIP_SECURITY" = false ]; then
    header "Security Scanning"

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
