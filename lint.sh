#!/bin/bash

# Lint be_demo (.NET format + build)
# Usage: ./lint.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "🔍 Linting be_demo..."
echo ""

dotnet format BeDemo.sln --verify-no-changes
dotnet build BeDemo.Api/BeDemo.Api.csproj -v q

echo ""
echo "✅ be_demo lint passed"
