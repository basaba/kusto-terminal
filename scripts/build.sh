#!/bin/bash
# Build script for Kusto Terminal

echo "Building Kusto Terminal..."

# Clean previous builds
dotnet clean

# Restore packages
dotnet restore

# Build solution
dotnet build --configuration Release

# Test build
dotnet build --configuration Debug

echo "Build completed successfully!"
echo ""
echo "To run the application:"
echo "  dotnet run --project src/KustoTerminal.CLI"
echo ""
echo "To publish for distribution:"
echo "  dotnet publish src/KustoTerminal.CLI -c Release -o dist/"