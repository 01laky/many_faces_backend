#!/bin/bash

# Script na spustenie Admin Demo API v Docker kontajneri pre vývoj

set -e

echo "🚀 Spúšťam Admin Demo API v Docker kontajneri..."

# Zastav existujúce kontajnery ak bežia
echo "🛑 Zastavujem existujúce kontajnery..."
docker-compose -f docker-compose.dev.yml down 2>/dev/null || true

# Vytvor adresár pre databázu ak neexistuje
mkdir -p AdminDemo.Api/data

# Zostav a spusti kontajnery
echo "🔨 Zostavujem Docker image..."
docker-compose -f docker-compose.dev.yml build

echo "▶️  Spúšťam kontajnery..."
docker-compose -f docker-compose.dev.yml up -d

# Počkaj kým kontajner bude pripravený
echo "⏳ Čakám na pripravenosť kontajnera..."
sleep 8

# Spusti migrations ak databáza neexistuje
if [ ! -f "AdminDemo.Api/data/AdminDemoDb.db" ]; then
    echo "📦 Vytváram databázu pomocou migrations..."
    docker-compose -f docker-compose.dev.yml exec -T admin-demo-api dotnet ef database update || echo "⚠️  Migrations sa pokúšajú spustiť..."
    sleep 3
fi

# Skontroluj či aplikácia beží
if curl -s http://localhost:8080/swagger/index.html > /dev/null 2>&1; then
    echo "✅ Aplikácia úspešne spustená!"
    echo ""
    echo "📍 URL: http://localhost:8080"
    echo "📚 Swagger UI: http://localhost:8080/swagger"
    echo ""
    echo "📋 Užitočné príkazy:"
    echo "   - Zobraziť logy: docker-compose -f docker-compose.dev.yml logs -f"
    echo "   - Zastaviť: docker-compose -f docker-compose.dev.yml down"
    echo "   - Reštartovať: docker-compose -f docker-compose.dev.yml restart"
else
    echo "⚠️  Aplikácia sa ešte spúšťa. Skúste znova za chvíľu."
    echo "📋 Zobraziť logy: docker-compose -f docker-compose.dev.yml logs -f"
fi
