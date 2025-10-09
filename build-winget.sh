#!/bin/bash

# Build script for Winget package manifest preparation
# This script helps update SHA256 hashes in the winget manifest

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WINGET_DIR="$SCRIPT_DIR/winget"
MANIFEST_DIR="$WINGET_DIR/manifests"
INSTALLER_MANIFEST="$MANIFEST_DIR/basaba.KustoTerminal.installer.yaml"

echo "================================================"
echo "Kusto Terminal - Winget Manifest Builder"
echo "================================================"
echo ""

# Check if release files exist or need to be downloaded
if [ ! -f "releases/KustoTerminal-win-x64.zip" ] || [ ! -f "releases/KustoTerminal-win-arm64.zip" ]; then
    echo "⚠️  Windows release files not found in releases/ directory"
    echo ""
    echo "Please ensure the following files exist:"
    echo "  - releases/KustoTerminal-win-x64.zip"
    echo "  - releases/KustoTerminal-win-arm64.zip"
    echo ""
    read -p "Enter version to download from GitHub releases (e.g., 0.1.0): " VERSION
    
    if [ -n "$VERSION" ]; then
        echo ""
        echo "Downloading release files for version $VERSION..."
        mkdir -p releases
        
        # Download x64
        echo "Downloading x64 build..."
        curl -L "https://github.com/basaba/kusto-terminal/releases/download/v${VERSION}/KustoTerminal-win-x64.zip" \
            -o "releases/KustoTerminal-win-x64.zip"
        
        # Download arm64
        echo "Downloading arm64 build..."
        curl -L "https://github.com/basaba/kusto-terminal/releases/download/v${VERSION}/KustoTerminal-win-arm64.zip" \
            -o "releases/KustoTerminal-win-arm64.zip"
        
        echo "✓ Downloads complete"
        echo ""
    else
        echo "Error: No version specified. Exiting."
        exit 1
    fi
fi

# Calculate SHA256 hashes
echo "Calculating SHA256 hashes..."
echo ""

if command -v sha256sum &> /dev/null; then
    SHA256_X64=$(sha256sum "releases/KustoTerminal-win-x64.zip" | awk '{print $1}')
    SHA256_ARM64=$(sha256sum "releases/KustoTerminal-win-arm64.zip" | awk '{print $1}')
elif command -v shasum &> /dev/null; then
    SHA256_X64=$(shasum -a 256 "releases/KustoTerminal-win-x64.zip" | awk '{print $1}')
    SHA256_ARM64=$(shasum -a 256 "releases/KustoTerminal-win-arm64.zip" | awk '{print $1}')
else
    echo "Error: Neither sha256sum nor shasum found. Please install one of these tools."
    exit 1
fi

echo "x64 SHA256:   $SHA256_X64"
echo "arm64 SHA256: $SHA256_ARM64"
echo ""

# Update installer manifest
echo "Updating installer manifest..."

if [[ "$OSTYPE" == "darwin"* ]]; then
    # macOS
    sed -i '' "s/PLACEHOLDER_SHA256_X64/$SHA256_X64/g" "$INSTALLER_MANIFEST"
    sed -i '' "s/PLACEHOLDER_SHA256_ARM64/$SHA256_ARM64/g" "$INSTALLER_MANIFEST"
else
    # Linux
    sed -i "s/PLACEHOLDER_SHA256_X64/$SHA256_X64/g" "$INSTALLER_MANIFEST"
    sed -i "s/PLACEHOLDER_SHA256_ARM64/$SHA256_ARM64/g" "$INSTALLER_MANIFEST"
fi

echo "✓ Installer manifest updated"
echo ""

# Display summary
echo "================================================"
echo "Summary"
echo "================================================"
echo ""
echo "Manifest files are ready in: $MANIFEST_DIR"
echo ""
echo "Files:"
echo "  - basaba.KustoTerminal.yaml"
echo "  - basaba.KustoTerminal.installer.yaml"
echo "  - basaba.KustoTerminal.locale.en-US.yaml"
echo ""
echo "Next steps:"
echo "  1. Review the manifest files"
echo "  2. Validate with: winget validate --manifest $MANIFEST_DIR/"
echo "  3. Follow the publishing guide in winget/WINGET-README.md"
echo ""
echo "================================================"
