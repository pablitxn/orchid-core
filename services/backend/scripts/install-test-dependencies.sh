#!/bin/bash

# Script to install dependencies required for running all tests

echo "==================================="
echo "Installing Test Dependencies"
echo "==================================="

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Detect OS
if [[ "$OSTYPE" == "linux-gnu"* ]]; then
    echo -e "${BLUE}Detected Linux system${NC}"
    
    # Check if running with proper permissions
    if [ "$EUID" -ne 0 ]; then 
        echo -e "${YELLOW}Some dependencies may require sudo. You might be prompted for your password.${NC}"
    fi
    
    # Install SkiaSharp dependencies
    echo -e "${YELLOW}Installing SkiaSharp dependencies...${NC}"
    
    # For Ubuntu/Debian based systems
    if command -v apt-get &> /dev/null; then
        sudo apt-get update
        sudo apt-get install -y \
            libfontconfig1 \
            libfreetype6 \
            libgl1-mesa-glx \
            libglu1-mesa \
            libgdiplus \
            libc6-dev
    
    # For Fedora/RHEL based systems
    elif command -v dnf &> /dev/null; then
        sudo dnf install -y \
            fontconfig \
            freetype \
            mesa-libGL \
            mesa-libGLU \
            libgdiplus
    
    # For Arch based systems
    elif command -v pacman &> /dev/null; then
        sudo pacman -S --noconfirm \
            fontconfig \
            freetype2 \
            mesa \
            libgdiplus
    else
        echo -e "${RED}Unable to detect package manager. Please install dependencies manually.${NC}"
        echo "Required packages: fontconfig, freetype, mesa/GL libraries, libgdiplus"
        exit 1
    fi
    
elif [[ "$OSTYPE" == "darwin"* ]]; then
    echo -e "${BLUE}Detected macOS system${NC}"
    
    # Check if Homebrew is installed
    if ! command -v brew &> /dev/null; then
        echo -e "${RED}Homebrew not found. Please install it from https://brew.sh/${NC}"
        exit 1
    fi
    
    echo -e "${YELLOW}Installing SkiaSharp dependencies...${NC}"
    brew install mono-libgdiplus

elif [[ "$OSTYPE" == "msys" ]] || [[ "$OSTYPE" == "cygwin" ]] || [[ "$OSTYPE" == "win32" ]]; then
    echo -e "${BLUE}Detected Windows system${NC}"
    echo -e "${YELLOW}SkiaSharp should work out of the box on Windows.${NC}"
    echo "If you encounter issues, install Visual C++ Redistributables."
    
else
    echo -e "${RED}Unsupported OS: $OSTYPE${NC}"
    exit 1
fi

# Install .NET tools
echo ""
echo -e "${YELLOW}Installing .NET tools...${NC}"

# Install ReportGenerator for coverage reports
if ! dotnet tool list -g | grep -q "dotnet-reportgenerator-globaltool"; then
    echo "Installing ReportGenerator..."
    dotnet tool install -g dotnet-reportgenerator-globaltool
else
    echo "ReportGenerator already installed"
fi

# Install dotnet-coverage tool
if ! dotnet tool list -g | grep -q "dotnet-coverage"; then
    echo "Installing dotnet-coverage..."
    dotnet tool install -g dotnet-coverage
else
    echo "dotnet-coverage already installed"
fi

echo ""
echo -e "${GREEN}✓ Dependencies installation completed!${NC}"
echo ""
echo "You can now run the full test suite with:"
echo "  ./run-unit-tests.sh"
echo "  ./run-integration-tests.sh"
echo "  ./run-all-tests.sh"
echo ""
echo "If you still encounter issues with SkiaSharp, try:"
echo "  ./run-unit-tests-safe.sh  (excludes problematic tests)"