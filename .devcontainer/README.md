# Orchid Core DevContainer

Development container configuration for VS Code, GitHub Codespaces, and compatible IDEs.

## Quick Start

### Option 1: GitHub Codespaces (Easiest)

1. Go to your repository on GitHub
2. Click **Code** > **Codespaces** > **Create codespace on main**
3. Wait for setup (~3-5 minutes first time)
4. Start coding!

### Option 2: VS Code + Docker (Local)

1. Install prerequisites:
   - [Docker Desktop](https://www.docker.com/products/docker-desktop)
   - [VS Code](https://code.visualstudio.com/)
   - [Dev Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers)

2. Clone and open:
   ```bash
   git clone https://github.com/your-org/orchid-core.git
   cd orchid-core
   code .
   ```

3. When prompted, click **"Reopen in Container"** (or run `Dev Containers: Reopen in Container` from command palette)

4. Wait for container build (~5-10 minutes first time)

### Option 3: CLI (devcontainer CLI)

```bash
# Install CLI
npm install -g @devcontainers/cli

# Build and start
devcontainer up --workspace-folder .

# Connect
devcontainer exec --workspace-folder . bash
```

## What's Included

### Services (auto-started)

| Service | Port | URL |
|---------|------|-----|
| PostgreSQL + pgvector | 5433 | `localhost:5433` |
| Redis | 6379 | `localhost:6379` |
| RabbitMQ | 5672, 15672 | `localhost:15672` |

### Optional Services (profiles)

Start with `--profile tools` for UI tools:
```bash
docker compose -f .devcontainer/docker-compose.yml --profile tools up -d
```

| Service | Port | Credentials |
|---------|------|-------------|
| pgAdmin | 8888 | `dev@orchid.local` / `dev_password_123` |
| Redis Commander | 8082 | — |

Start with `--profile langfuse` for LLM observability:
```bash
docker compose -f .devcontainer/docker-compose.yml --profile langfuse up -d
```

| Service | Port |
|---------|------|
| Langfuse | 3033 |
| MinIO Console | 9091 |

### Pre-installed Tools

- .NET 10 SDK
- Node.js 22 + yarn/pnpm
- dotnet-ef (Entity Framework CLI)
- PostgreSQL client (psql)
- Redis CLI
- GitHub CLI (gh)
- Docker CLI
- Claude Code CLI (if installed)

### VS Code Extensions

- C# Dev Kit
- ESLint & Prettier
- Tailwind CSS IntelliSense
- PostgreSQL & Redis clients
- GitLens
- GitHub Copilot
- REST Client
- And more...

## Useful Commands

After the container starts, these aliases are available:

```bash
# Backend
api          # Start the API server
api-watch    # Start with hot reload
test-unit    # Run unit tests
test-all     # Run all tests

# Frontend
web          # Start Vite dev server

# Database
db-migrate   # Run EF migrations
psql-orchid  # Connect to PostgreSQL

# Status
orchid-status  # Check all services
```

## Configuration

### Environment Variables

The devcontainer uses these defaults (override in `.devcontainer/.env`):

```env
# Database
ConnectionStrings__DefaultConnection=Host=database;Port=5432;Database=orchid_core;Username=orchid_admin;Password=dev_password_123

# Redis
Redis__Configuration=redis_orchid_core:6379,password=dev_password_123

# RabbitMQ
RabbitMq__Host=rabbitmq
RabbitMq__Username=orchid_mq
RabbitMq__Password=dev_password_123
```

### Resource Requirements

Minimum for Codespaces:
- **CPU**: 4 cores
- **RAM**: 8 GB
- **Storage**: 32 GB

Recommended:
- **CPU**: 8 cores
- **RAM**: 16 GB

## Troubleshooting

### Container won't start

```bash
# Check Docker is running
docker info

# Rebuild without cache
devcontainer rebuild --workspace-folder . --no-cache
```

### Database connection fails

```bash
# Check PostgreSQL is healthy
docker logs orchid_devcontainer_db

# Manually test connection
psql -h localhost -p 5433 -U orchid_admin -d orchid_core
```

### Migrations fail

```bash
# Run manually with verbose output
cd /workspace/services/backend/src/Adapters/Infrastructure
dotnet ef database update --startup-project ../WebApi --verbose
```

### Reset everything

```bash
# Stop and remove all containers/volumes
docker compose -f .devcontainer/docker-compose.yml down -v

# Rebuild
devcontainer up --workspace-folder . --remove-existing-container
```

## File Structure

```
.devcontainer/
├── devcontainer.json      # Main configuration
├── docker-compose.yml     # All services definition
├── Dockerfile            # Dev container image
├── postCreateCommand.sh  # Setup script (runs once)
└── README.md            # This file
```

## Tips for AI-Assisted Development

This devcontainer is optimized for working with AI coding assistants:

1. **Claude Code**: Pre-installed CLI, run `claude` in terminal
2. **GitHub Copilot**: Extension pre-installed and configured
3. **Full context**: All services running = AI can test its code
4. **Isolated**: Safe environment for AI to experiment

Happy coding! 🌸
