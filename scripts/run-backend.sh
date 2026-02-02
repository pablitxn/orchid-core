#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "Starting infrastructure services..."
docker compose -f "$ROOT_DIR/.devcontainer/docker-compose.yml" up -d database redis_orchid_core rabbitmq

echo "Waiting for database to be ready..."
sleep 3

echo "Starting backend..."
cd "$ROOT_DIR"
dotnet run --project services/backend/src/Adapters/WebApi/WebApi.csproj "$@"
