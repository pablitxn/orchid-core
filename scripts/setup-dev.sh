#!/bin/bash

# Complete development setup script for Orchid Core
# This script starts all services and runs migrations

set -e

echo "🌸 Orchid Core Development Setup"
echo "================================"

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Step 1: Start Docker services
echo ""
echo "📦 Starting Docker services..."
if docker-compose -f docker-compose.dev.yaml up -d; then
    echo -e "${GREEN}✅ Docker services started successfully${NC}"
else
    echo -e "${RED}❌ Failed to start Docker services${NC}"
    exit 1
fi

# Step 2: Wait a bit for services to initialize
echo ""
echo "⏳ Waiting for services to initialize..."
sleep 5

# Step 3: Run migrations
echo ""
./run-migrations.sh

# Step 4: Show service URLs
echo ""
echo -e "${GREEN}🎉 Development environment is ready!${NC}"
echo ""
echo "📍 Service URLs:"
echo "  - PostgreSQL:      localhost:5433"
echo "  - PgAdmin:         http://localhost:8888 (admin@admin.com / admin)"
echo "  - Redis:           localhost:6379"
echo "  - Redis Commander: http://localhost:8081 (root / root)"
echo "  - RabbitMQ:        http://localhost:15672 (guest / guest)"
echo "  - Langfuse:        http://localhost:3033"
echo "  - MinIO:           http://localhost:9090 (minio / miniosecret)"
echo ""
echo "To start the backend API:"
echo "  cd services/backend/src/Adapters/WebApi"
echo "  dotnet run"
echo ""
echo "To stop all services:"
echo "  docker-compose -f docker-compose.dev.yaml down"