#!/bin/bash
# Script to manually generate database ERD diagram

cd "$(dirname "$0")/BeDemo.Api" || exit 1

# Connection string for local database
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=bedemo;Username=bedemo_user;Password=bedemo_password"

echo "📊 Generating database ERD diagram..."
echo ""

# Create a simple C# script that will generate the diagram
dotnet run --no-build -- generate-diagram 2>/dev/null || {
    echo "⚠️  Could not generate diagram via dotnet run"
    echo ""
    echo "💡 Diagram will be generated automatically when you start the backend API"
    echo "   The diagram is generated in InitializeDatabase.cs after migrations"
}
