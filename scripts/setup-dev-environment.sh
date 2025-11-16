#!/bin/bash
# Development Environment Setup Script for RefactorCsharpMCP
# This script automates the setup of the development environment

set -e  # Exit on error

echo "========================================="
echo "RefactorCsharpMCP Development Setup"
echo "========================================="
echo ""

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Function to print success messages
success() {
    echo -e "${GREEN}✓${NC} $1"
}

# Function to print warning messages
warning() {
    echo -e "${YELLOW}⚠${NC} $1"
}

# Function to print error messages
error() {
    echo -e "${RED}✗${NC} $1"
}

# Check if running in supported OS
OS="$(uname -s)"
case "${OS}" in
    Linux*)     MACHINE=Linux;;
    Darwin*)    MACHINE=Mac;;
    *)          MACHINE="UNKNOWN"
esac

echo "Detected OS: $MACHINE"
echo ""

# Step 1: Check for .NET SDK
echo "Step 1: Checking for .NET SDK..."
if command -v dotnet &> /dev/null; then
    DOTNET_VERSION=$(dotnet --version)
    MAJOR_VERSION=$(echo $DOTNET_VERSION | cut -d'.' -f1)

    if [ "$MAJOR_VERSION" -ge 8 ]; then
        success ".NET SDK $DOTNET_VERSION is installed"
    else
        warning ".NET SDK $DOTNET_VERSION found, but version 8.0+ is required"
        echo "Please install .NET 8 SDK from: https://dotnet.microsoft.com/download/dotnet/8.0"
        exit 1
    fi
else
    error ".NET SDK not found"
    echo ""
    echo "Installing .NET SDK 8.0..."

    if [ "$MACHINE" = "Linux" ]; then
        # Detect Linux distribution
        if [ -f /etc/os-release ]; then
            . /etc/os-release
            echo "Detected: $NAME $VERSION"

            if [ "$ID" = "ubuntu" ] || [ "$ID" = "debian" ]; then
                echo "Installing via package manager..."

                # Download Microsoft package repository config
                wget https://packages.microsoft.com/config/$ID/$VERSION_ID/packages-microsoft-prod.deb -O /tmp/packages-microsoft-prod.deb
                sudo dpkg -i /tmp/packages-microsoft-prod.deb
                rm /tmp/packages-microsoft-prod.deb

                # Install .NET SDK
                sudo apt-get update
                sudo apt-get install -y dotnet-sdk-8.0

                success ".NET SDK 8.0 installed"
            else
                warning "Unsupported Linux distribution: $ID"
                echo "Please install .NET 8 SDK manually from:"
                echo "https://dotnet.microsoft.com/download/dotnet/8.0"
                exit 1
            fi
        fi
    elif [ "$MACHINE" = "Mac" ]; then
        if command -v brew &> /dev/null; then
            echo "Installing via Homebrew..."
            brew install dotnet-sdk
            success ".NET SDK installed"
        else
            warning "Homebrew not found"
            echo "Please install .NET 8 SDK manually from:"
            echo "https://dotnet.microsoft.com/download/dotnet/8.0"
            exit 1
        fi
    else
        error "Unsupported operating system: $MACHINE"
        echo "Please install .NET 8 SDK manually from:"
        echo "https://dotnet.microsoft.com/download/dotnet/8.0"
        exit 1
    fi
fi
echo ""

# Step 2: Navigate to project root
echo "Step 2: Locating project directory..."
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
cd "$PROJECT_ROOT"
success "Project root: $PROJECT_ROOT"
echo ""

# Step 3: Restore NuGet packages
echo "Step 3: Restoring NuGet packages..."
if dotnet restore; then
    success "NuGet packages restored"
else
    error "Failed to restore NuGet packages"
    exit 1
fi
echo ""

# Step 4: Build the solution
echo "Step 4: Building solution..."
if dotnet build; then
    success "Solution built successfully"
else
    error "Build failed"
    exit 1
fi
echo ""

# Step 5: Run tests (optional, can be skipped)
echo "Step 5: Running tests..."
echo "(This may take 2-5 minutes on first run while downloading reference assemblies)"
if dotnet test --no-build; then
    success "All tests passed"
else
    warning "Some tests failed - this may be expected on first run"
fi
echo ""

# Step 6: Summary
echo "========================================="
echo "Setup Complete!"
echo "========================================="
echo ""
echo "Next steps:"
echo "  1. Open the solution in your IDE:"
echo "     - Visual Studio Code: code $PROJECT_ROOT"
echo "     - Visual Studio 2022: Open RefactorCsharpMCP.sln"
echo "     - JetBrains Rider: Open RefactorCsharpMCP.sln"
echo ""
echo "  2. Read documentation:"
echo "     - CLAUDE.md - Project guidance for AI-assisted development"
echo "     - README.md - User documentation"
echo "     - docs/PRD-V1-Refactoring-Capabilities.md - Project roadmap"
echo ""
echo "  3. Run the MCP server:"
echo "     cd src/RefactorCsharpMCP.Server"
echo "     dotnet run"
echo ""
echo "  4. Run tests:"
echo "     dotnet test"
echo ""
success "Happy coding!"
