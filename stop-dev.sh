#!/bin/bash

# Script na zastavenie Admin Demo API Docker kontajnerov

echo "🛑 Zastavujem Admin Demo API kontajnery..."

docker-compose -f docker-compose.dev.yml down

echo "✅ Kontajnery zastavené a odstránené"
