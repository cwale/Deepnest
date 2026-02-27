#!/bin/bash
# Build script for DeepNest Rhino plugin
# Produces outputs for both net48 and net7.0-windows targets

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
SLN="$SCRIPT_DIR/DeepNestRhino.sln"

echo "=== Building DeepNest for Rhino ==="

# Clean
dotnet clean "$SLN" -c Release --nologo -v q

# Build both targets
dotnet build "$SLN" -c Release --nologo

echo ""
echo "=== Build outputs ==="
echo "net48:          $SCRIPT_DIR/src/DeepNestRhino/bin/Release/net48/"
echo "net7.0-windows: $SCRIPT_DIR/src/DeepNestRhino/bin/Release/net7.0-windows/"

echo ""
echo "=== Running tests ==="
dotnet test "$SCRIPT_DIR/tests/DeepNestRhino.Tests/DeepNestRhino.Tests.csproj" -c Release --nologo

echo ""
echo "=== To create Yak package ==="
echo "1. Copy manifest.yml to build output directory"
echo "2. yak build --platform win"
echo ""
echo "Done."
