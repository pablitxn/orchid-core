#!/bin/bash
# Orchid Core DevContainer - Post Create Command
# This script runs once when the container is first created

set -e

echo "═══════════════════════════════════════════════════════════════════════════"
echo "🌸 Orchid Core DevContainer Setup"
echo "═══════════════════════════════════════════════════════════════════════════"

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

print_step() {
    echo -e "\n${BLUE}▶ $1${NC}"
}

print_success() {
    echo -e "${GREEN}✅ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

# ═══════════════════════════════════════════════════════════════════════════
# 1. VERIFY TOOLS
# ═══════════════════════════════════════════════════════════════════════════
print_step "Verifying installed tools..."

echo "  .NET SDK:    $(dotnet --version)"
echo "  Node.js:     $(node --version)"
echo "  npm:         $(npm --version)"
echo "  yarn:        $(yarn --version 2>/dev/null || echo 'not installed')"
echo "  dotnet-ef:   $(dotnet ef --version 2>/dev/null || echo 'not installed')"

print_success "Tools verified"

# ═══════════════════════════════════════════════════════════════════════════
# 2. WAIT FOR DATABASE
# ═══════════════════════════════════════════════════════════════════════════
print_step "Waiting for PostgreSQL to be ready..."

MAX_RETRIES=30
RETRY_COUNT=0

until pg_isready -h database -p 5432 -U orchid_admin -d orchid_core -q 2>/dev/null; do
    RETRY_COUNT=$((RETRY_COUNT + 1))
    if [ $RETRY_COUNT -ge $MAX_RETRIES ]; then
        print_warning "PostgreSQL not ready after ${MAX_RETRIES} attempts, continuing anyway..."
        break
    fi
    echo "  Waiting for PostgreSQL... (attempt $RETRY_COUNT/$MAX_RETRIES)"
    sleep 2
done

print_success "PostgreSQL is ready"

# ═══════════════════════════════════════════════════════════════════════════
# 3. RESTORE .NET PACKAGES
# ═══════════════════════════════════════════════════════════════════════════
print_step "Restoring .NET packages..."

cd /workspace/services/backend
dotnet restore Backend.sln --verbosity minimal

print_success ".NET packages restored"

# ═══════════════════════════════════════════════════════════════════════════
# 4. RUN DATABASE MIGRATIONS
# ═══════════════════════════════════════════════════════════════════════════
print_step "Running database migrations..."

cd /workspace/services/backend/src/Adapters/Infrastructure

# Ensure dotnet tools are in PATH
export PATH="$PATH:$HOME/.dotnet/tools"

# Use the global dotnet-ef tool directly
DOTNET_EF="$HOME/.dotnet/tools/dotnet-ef"

if [ -f "$DOTNET_EF" ]; then
    # Set connection string for migrations
    export ConnectionStrings__DefaultConnection="Host=database;Port=5432;Database=orchid_core;Username=orchid_admin;Password=dev_password_123"

    cd /workspace/services/backend
    $DOTNET_EF database update \
        --project src/Adapters/Infrastructure/Infrastructure.csproj \
        --startup-project src/Adapters/WebApi/WebApi.csproj \
        || print_warning "Migrations failed - you may need to run them manually"

    print_success "Database migrations applied"
else
    print_warning "dotnet-ef not found. Install with: dotnet tool install --global dotnet-ef"
fi

# ═══════════════════════════════════════════════════════════════════════════
# 5. INSTALL FRONTEND DEPENDENCIES (if frontend exists)
# ═══════════════════════════════════════════════════════════════════════════
print_step "Installing frontend dependencies..."

if [ -d "/workspace/apps/web" ]; then
    cd /workspace/apps/web

    if [ -f "package.json" ]; then
        if [ -f "yarn.lock" ]; then
            yarn install --frozen-lockfile || yarn install
        elif [ -f "pnpm-lock.yaml" ]; then
            pnpm install --frozen-lockfile || pnpm install
        else
            npm ci || npm install
        fi
        print_success "Frontend dependencies installed"
    else
        print_warning "No package.json found in apps/web"
    fi
else
    print_warning "Frontend directory (apps/web) not found - skipping"
fi

# ═══════════════════════════════════════════════════════════════════════════
# 6. BUILD BACKEND (OPTIONAL - for faster first run)
# ═══════════════════════════════════════════════════════════════════════════
print_step "Building backend..."

cd /workspace/services/backend
dotnet build Backend.sln --configuration Debug --verbosity minimal --no-restore

print_success "Backend built"

# ═══════════════════════════════════════════════════════════════════════════
# 7. SETUP GIT HOOKS (if any)
# ═══════════════════════════════════════════════════════════════════════════
print_step "Setting up Git configuration..."

cd /workspace

# Configure git to use the host's credentials
git config --global --add safe.directory /workspace

# Setup git hooks if husky is present
if [ -f "apps/web/.husky/pre-commit" ]; then
    print_success "Git hooks configured"
else
    print_warning "No git hooks found"
fi

# ═══════════════════════════════════════════════════════════════════════════
# 8. CREATE HELPFUL ALIASES
# ═══════════════════════════════════════════════════════════════════════════
print_step "Setting up shell aliases..."

# Add aliases to .zshrc
cat >> ~/.zshrc << 'EOF'

# ═══════════════════════════════════════════════════════════════════════════
# Orchid Core Aliases
# ═══════════════════════════════════════════════════════════════════════════

# Backend
alias api='cd /workspace/services/backend/src/Adapters/WebApi && dotnet run'
alias api-watch='cd /workspace/services/backend/src/Adapters/WebApi && dotnet watch run'
alias backend='cd /workspace/services/backend'
alias test-unit='cd /workspace/services/backend && dotnet test tests/Unit/'
alias test-int='cd /workspace/services/backend && dotnet test tests/Integration/'
alias test-all='cd /workspace/services/backend && dotnet test'

# Frontend
alias web='cd /workspace/apps/web && yarn dev'
alias frontend='cd /workspace/apps/web'

# Database
alias db-migrate='cd /workspace/services/backend/src/Adapters/Infrastructure && dotnet ef database update --startup-project ../WebApi'
alias db-add='cd /workspace/services/backend/src/Adapters/Infrastructure && dotnet ef migrations add'
alias psql-orchid='PGPASSWORD=dev_password_123 psql -h database -U orchid_admin -d orchid_core'

# Services
alias logs-api='docker logs -f orchid_devcontainer_api 2>/dev/null || echo "API not running in container"'
alias logs-db='docker logs -f orchid_devcontainer_db'
alias logs-redis='docker logs -f orchid_devcontainer_redis'
alias logs-rabbit='docker logs -f orchid_devcontainer_rabbitmq'

# Quick navigation
alias ws='cd /workspace'

# Status check
alias orchid-status='echo "PostgreSQL:" && pg_isready -h database -p 5432 && echo "Redis:" && redis-cli -h redis_orchid_core -a dev_password_123 ping 2>/dev/null && echo "RabbitMQ:" && curl -s -u orchid_mq:dev_password_123 http://rabbitmq:15672/api/overview | jq -r ".rabbitmq_version" 2>/dev/null || echo "Not reachable"'

EOF

# Also add to .bashrc for bash users
cat >> ~/.bashrc << 'EOF'

# Orchid Core Aliases (see .zshrc for full list)
alias api='cd /workspace/services/backend/src/Adapters/WebApi && dotnet run'
alias web='cd /workspace/apps/web && yarn dev'
alias ws='cd /workspace'

EOF

print_success "Shell aliases configured"

# ═══════════════════════════════════════════════════════════════════════════
# DONE!
# ═══════════════════════════════════════════════════════════════════════════
echo ""
echo "═══════════════════════════════════════════════════════════════════════════"
echo -e "${GREEN}🎉 DevContainer setup complete!${NC}"
echo "═══════════════════════════════════════════════════════════════════════════"
echo ""
echo "📍 Quick Start Commands:"
echo "   api          - Start the backend API"
echo "   api-watch    - Start backend with hot reload"
echo "   web          - Start the frontend dev server"
echo "   test-all     - Run all tests"
echo "   orchid-status - Check all services"
echo ""
echo "📍 Service URLs:"
echo "   Backend API:     http://localhost:5000"
echo "   Frontend:        http://localhost:5173"
echo "   pgAdmin:         http://localhost:8888  (dev@orchid.local / dev_password_123)"
echo "   RabbitMQ:        http://localhost:15672 (orchid_mq / dev_password_123)"
echo "   Redis Commander: http://localhost:8082"
echo "   Langfuse:        http://localhost:3033  (if enabled with --profile langfuse)"
echo ""
echo "Happy coding! 🌸"
