#!/bin/bash

#
# Comprehensive security scanning for RefactorCsharpMCP Docker image
#
# Usage: ./security-scan.sh [IMAGE_NAME] [OPTIONS]
#
# Options:
#   --detailed          Generate detailed reports
#   --sbom              Generate Software Bill of Materials
#   --fail-on-critical  Exit with error if CRITICAL vulnerabilities found
#   --output-dir DIR    Directory for reports (default: current directory)
#

set -e

IMAGE_NAME="${1:-refactor-csharp-mcp:latest}"
DETAILED=false
GENERATE_SBOM=false
FAIL_ON_CRITICAL=false
OUTPUT_DIR="."
HAS_CRITICAL=false
REPORT_TIME=$(date +%Y%m%d-%H%M%S)

# Parse options
shift
while [[ $# -gt 0 ]]; do
    case $1 in
        --detailed) DETAILED=true; shift ;;
        --sbom) GENERATE_SBOM=true; shift ;;
        --fail-on-critical) FAIL_ON_CRITICAL=true; shift ;;
        --output-dir) OUTPUT_DIR="$2"; shift 2 ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;90m'
NC='\033[0m'

header() { echo -e "\n${CYAN}==== $1 ====${NC}"; }
success() { echo -e "${GREEN}✓ $1${NC}"; }
error() { echo -e "${RED}✗ $1${NC}"; }
warning() { echo -e "${YELLOW}⚠ $1${NC}"; }
info() { echo -e "${GRAY}  $1${NC}"; }

header "Security Scanning: $IMAGE_NAME"

# Verify image exists
info "Verifying image exists..."
if ! docker images -q "$IMAGE_NAME" | grep -q .; then
    error "Image '$IMAGE_NAME' not found. Build it first."
    exit 1
fi
success "Image found"

# Docker Scout
header "Docker Scout Analysis"
if docker scout version &> /dev/null; then
    info "Running CVE scan..."
    SCOUT_REPORT="$OUTPUT_DIR/security-scout-$REPORT_TIME.txt"
    docker scout cves "$IMAGE_NAME" 2>&1 | tee "$SCOUT_REPORT"

    CRITICAL_COUNT=$(grep -c "CRITICAL" "$SCOUT_REPORT" || echo "0")
    if [ "$CRITICAL_COUNT" -gt 0 ]; then
        error "Found $CRITICAL_COUNT CRITICAL vulnerabilities"
        HAS_CRITICAL=true
    else
        success "No CRITICAL vulnerabilities found"
    fi

    docker scout recommendations "$IMAGE_NAME" > "$OUTPUT_DIR/security-recommendations-$REPORT_TIME.txt" 2>&1
    success "Docker Scout scan completed: $SCOUT_REPORT"
else
    warning "Docker Scout not available"
fi

# Trivy
header "Trivy Analysis"
if command -v trivy &> /dev/null; then
    info "Running comprehensive scan..."
    TRIVY_REPORT="$OUTPUT_DIR/security-trivy-$REPORT_TIME.txt"
    trivy image --severity UNKNOWN,LOW,MEDIUM,HIGH,CRITICAL "$IMAGE_NAME" 2>&1 | tee "$TRIVY_REPORT"

    TRIVY_CRITICAL=$(grep -c "CRITICAL" "$TRIVY_REPORT" || echo "0")
    if [ "$TRIVY_CRITICAL" -gt 0 ]; then
        error "Trivy found $TRIVY_CRITICAL CRITICAL vulnerabilities"
        HAS_CRITICAL=true
    else
        success "No CRITICAL vulnerabilities found by Trivy"
    fi

    if [ "$GENERATE_SBOM" = true ]; then
        header "Generating SBOM"
        SBOM_FILE="$OUTPUT_DIR/sbom-$REPORT_TIME.json"
        trivy image --format cyclonedx --output "$SBOM_FILE" "$IMAGE_NAME" 2>&1
        success "SBOM generated: $SBOM_FILE"
    fi

    success "Trivy scan completed: $TRIVY_REPORT"
else
    warning "Trivy not installed"
fi

# Summary
header "Security Scan Summary"
success "Image scanned: $IMAGE_NAME"
success "Reports in: $OUTPUT_DIR"

if [ "$HAS_CRITICAL" = true ]; then
    error "CRITICAL vulnerabilities detected!"
    if [ "$FAIL_ON_CRITICAL" = true ]; then
        error "Failing due to --fail-on-critical flag"
        exit 1
    else
        warning "Review security reports before deployment"
    fi
else
    success "No CRITICAL vulnerabilities detected"
fi
