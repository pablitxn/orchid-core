#!/bin/bash

# Script to run Entity Framework migrations for Orchid Core
# This should be run after docker-compose.dev.yaml is up

set -e

echo "🚀 Running Orchid Core database migrations..."

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Check if docker-compose services are running
echo "📋 Checking if database is running..."
if ! docker ps | grep -q "orchid_core_pg_db"; then
    echo -e "${RED}❌ Database container is not running!${NC}"
    echo "Please run: docker-compose -f docker-compose.dev.yaml up -d"
    exit 1
fi

# Wait for database to be ready
echo "⏳ Waiting for database to be ready..."
for i in {1..30}; do
    if docker exec orchid_core_pg_db pg_isready -U admin > /dev/null 2>&1; then
        echo -e "${GREEN}✅ Database is ready!${NC}"
        break
    fi
    if [ $i -eq 30 ]; then
        echo -e "${RED}❌ Database is not responding after 30 seconds${NC}"
        exit 1
    fi
    echo -n "."
    sleep 1
done

# Navigate to the WebApi project directory
cd services/backend/src/Adapters/WebApi

# Check if dotnet ef is installed
if ! command -v dotnet-ef &> /dev/null; then
    echo "📦 Installing Entity Framework CLI tool..."
    dotnet tool install --global dotnet-ef
fi

# Run migrations
echo "🔨 Applying database migrations..."
export ASPNETCORE_ENVIRONMENT=Development
if dotnet ef database update --project ../Infrastructure/Infrastructure.csproj; then
    echo -e "${GREEN}✅ Migrations applied successfully!${NC}"
else
    echo -e "${RED}❌ Failed to apply migrations${NC}"
    exit 1
fi

echo -e "${GREEN}🎉 Database setup complete!${NC}"
echo ""
echo "You can now run the application with:"
echo "  cd services/backend/src/Adapters/WebApi"
echo "  dotnet run"