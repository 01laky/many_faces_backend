#!/bin/bash

# Script to completely remove Be Demo API Docker containers and volumes
# Usage: ./clear-dev.sh

set -e

cd "$(dirname "$0")"

echo "🧹 Clearing Be Demo API containers and volumes..."

# Stop and remove containers
docker-compose -f docker-compose.dev.yml down -v 2>/dev/null || true

# Remove containers by name if they still exist
docker rm -f be-demo-dev seq-dev 2>/dev/null || true

# Remove volumes
docker volume rm be-demo-https 2>/dev/null || true
docker volume rm be-demo-data 2>/dev/null || true

echo "✅ Be Demo API containers and volumes cleared"
