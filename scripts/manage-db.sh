#!/bin/bash

# Database management script for Orchid Core
# Provides options to manage migrations and database state

set -e

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to show usage
show_usage() {
    echo "Usage: $0 [command]"
    echo ""
    echo "Commands:"
    echo "  migrate       - Apply pending migrations"
    echo "  reset         - Drop and recreate database, then apply migrations"
    echo "  status        - Show migration status"
    echo "  list          - List all migrations"
    echo "  add [name]    - Add a new migration"
    echo "  remove        - Remove last migration"
    echo ""
}

# Check if database is running
check_database() {
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
}

# Navigate to WebApi directory
cd services/backend/src/Adapters/WebApi

# Set environment
export ASPNETCORE_ENVIRONMENT=Development

# Check if dotnet ef is installed
if ! command -v dotnet-ef &> /dev/null; then
    echo "📦 Installing Entity Framework CLI tool..."
    dotnet tool install --global dotnet-ef
fi

# Parse command
case "${1:-migrate}" in
    "migrate")
        echo -e "${BLUE}🔨 Applying database migrations...${NC}"
        check_database
        
        # Option 1: Run migrations via EF CLI
        echo "Running migrations via EF CLI..."
        dotnet ef database update --project ../Infrastructure/Infrastructure.csproj
        
        echo -e "${GREEN}✅ Migrations check complete!${NC}"
        echo ""
        echo -e "${YELLOW}Note: The application also applies migrations automatically on startup.${NC}"
        echo "To ensure all tables are created, run:"
        echo "  cd services/backend/src/Adapters/WebApi"
        echo "  dotnet run"
        ;;
        
    "reset")
        echo -e "${YELLOW}⚠️  This will DROP and RECREATE the database!${NC}"
        read -p "Are you sure? (y/N) " -n 1 -r
        echo
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            check_database
            
            echo -e "${RED}🗑️  Dropping database...${NC}"
            docker exec orchid_core_pg_db psql -U admin -c "DROP DATABASE IF EXISTS orchid_core;"
            
            echo -e "${BLUE}🏗️  Creating database...${NC}"
            docker exec orchid_core_pg_db psql -U admin -c "CREATE DATABASE orchid_core;"
            
            echo -e "${BLUE}🔨 Applying migrations...${NC}"
            dotnet ef database update --project ../Infrastructure/Infrastructure.csproj
            
            echo -e "${GREEN}✅ Database reset complete!${NC}"
        else
            echo "Operation cancelled."
        fi
        ;;
        
    "status")
        echo -e "${BLUE}📊 Checking migration status...${NC}"
        check_database
        
        echo "Applied migrations:"
        docker exec orchid_core_pg_db psql -U admin -d orchid_core -c "SELECT \"MigrationId\", \"ProductVersion\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";" 2>/dev/null || echo "No migrations found (database might be empty)"
        
        echo ""
        echo "Pending migrations:"
        dotnet ef migrations list --project ../Infrastructure/Infrastructure.csproj | grep -E "^\s{2}" || echo "All migrations applied"
        ;;
        
    "list")
        echo -e "${BLUE}📋 Available migrations:${NC}"
        dotnet ef migrations list --project ../Infrastructure/Infrastructure.csproj
        ;;
        
    "add")
        if [ -z "$2" ]; then
            echo -e "${RED}❌ Please provide a migration name${NC}"
            echo "Usage: $0 add [MigrationName]"
            exit 1
        fi
        echo -e "${BLUE}➕ Adding new migration: $2${NC}"
        dotnet ef migrations add "$2" --project ../Infrastructure/Infrastructure.csproj --output-dir ../Infrastructure/Migrations
        echo -e "${GREEN}✅ Migration added successfully!${NC}"
        ;;
        
    "remove")
        echo -e "${YELLOW}⚠️  This will remove the last migration!${NC}"
        read -p "Are you sure? (y/N) " -n 1 -r
        echo
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            echo -e "${RED}🗑️  Removing last migration...${NC}"
            dotnet ef migrations remove --project ../Infrastructure/Infrastructure.csproj
            echo -e "${GREEN}✅ Migration removed!${NC}"
        else
            echo "Operation cancelled."
        fi
        ;;
        
    *)
        show_usage
        exit 1
        ;;
esac