#!/bin/bash

# KustoTerminal Homebrew Build Script
# This script builds macOS releases for Homebrew distribution

set -e

VERSION=${1:-"0.1.0"}
PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
BUILD_DIR="$PROJECT_DIR/build/homebrew"
RELEASE_DIR="$PROJECT_DIR/releases"

echo "=========================================="
echo "Building KustoTerminal v$VERSION for Homebrew"
echo "=========================================="

# Clean previous builds
echo "Cleaning previous builds..."
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"
mkdir -p "$RELEASE_DIR"

# Build for macOS x64
echo ""
echo "Building for macOS x64..."
dotnet publish src/KustoTerminal.CLI/KustoTerminal.CLI.csproj \
  -c Release \
  -r osx-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:PublishTrimmed=false \
  -o "$BUILD_DIR/osx-x64"

# Build for macOS ARM64
echo ""
echo "Building for macOS ARM64..."
dotnet publish src/KustoTerminal.CLI/KustoTerminal.CLI.csproj \
  -c Release \
  -r osx-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:PublishTrimmed=false \
  -o "$BUILD_DIR/osx-arm64"

# Create tar.gz archives
echo ""
echo "Creating tar.gz archives..."

cd "$BUILD_DIR/osx-x64"
tar -czf "$RELEASE_DIR/KustoTerminal-osx-x64.tar.gz" KustoTerminal
X64_CHECKSUM=$(shasum -a 256 "$RELEASE_DIR/KustoTerminal-osx-x64.tar.gz" | awk '{print $1}')

cd "$BUILD_DIR/osx-arm64"
tar -czf "$RELEASE_DIR/KustoTerminal-osx-arm64.tar.gz" KustoTerminal
ARM64_CHECKSUM=$(shasum -a 256 "$RELEASE_DIR/KustoTerminal-osx-arm64.tar.gz" | awk '{print $1}')

cd "$PROJECT_DIR"

# Display results
echo ""
echo "=========================================="
echo "Build Complete!"
echo "=========================================="
echo ""
echo "Release files created in: $RELEASE_DIR"
echo ""
echo "Files:"
echo "  - KustoTerminal-osx-x64.tar.gz"
echo "  - KustoTerminal-osx-arm64.tar.gz"
echo ""
echo "SHA256 Checksums:"
echo "  x64:   $X64_CHECKSUM"
echo "  arm64: $ARM64_CHECKSUM"
echo ""
echo "=========================================="
echo "Next Steps:"
echo "=========================================="
echo ""
echo "1. Create a GitHub release (v$VERSION):"
echo "   gh release create v$VERSION \\"
echo "     --title \"v$VERSION\" \\"
echo "     --notes \"Release notes here\" \\"
echo "     $RELEASE_DIR/KustoTerminal-osx-x64.tar.gz \\"
echo "     $RELEASE_DIR/KustoTerminal-osx-arm64.tar.gz"
echo ""
echo "2. Update homebrew/kustoterminal.rb:"
echo "   - Set version to: $VERSION"
echo "   - Replace CHECKSUM_X64_PLACEHOLDER with: $X64_CHECKSUM"
echo "   - Replace CHECKSUM_ARM64_PLACEHOLDER with: $ARM64_CHECKSUM"
echo "   - Update download URLs to: v$VERSION"
echo ""
echo "3. Test locally:"
echo "   brew install --build-from-source homebrew/kustoterminal.rb"
echo ""
echo "4. Publish to Homebrew:"
echo "   See HOMEBREW-README.md for instructions"
echo ""
