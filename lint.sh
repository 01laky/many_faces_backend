#!/usr/bin/env bash
# Lint many_faces_backend — same style check as CI (dotnet format).
# Usage: ./lint.sh

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "🔍 Linting many_faces_backend (dotnet format)..."
echo ""

dotnet restore BeDemo.sln
dotnet format BeDemo.sln --verify-no-changes --no-restore

echo ""
echo "✅ many_faces_backend lint passed"
