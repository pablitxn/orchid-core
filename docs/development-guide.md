# Orchid Core - Development Guide

## Prerequisites

| Requirement | Version | Purpose |
|-------------|---------|---------|
| .NET SDK | 10.0+ | Build and run |
| Node.js | 22+ | Frontend (apps/web) |
| Docker | Latest | Local services |
| PostgreSQL | 17+ | Database (via Docker) |
| Redis | 7+ | Cache (via Docker) |
| RabbitMQ | 3+ | Messaging (via Docker) |

## Quick Start

### 1. Clone Repository
```bash
git clone https://github.com/pablitxn/orchid-core.git
cd orchid-core
```

### 2. Start Infrastructure Services
```bash
# Copy environment template and configure secrets
cp .env.example .env
# Edit .env with your values

# Start PostgreSQL, Redis, RabbitMQ, Langfuse
docker-compose -f docker-compose.dev.yaml up -d
```

### 3. Configure User Secrets
```bash
cd services/backend/src/Adapters/WebApi

# Set required secrets
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5433;Database=orchid_core;Username=orchid_admin;Password=your_password"
dotnet user-secrets set "OpenAI:ApiKey" "sk-your-openai-key"
dotnet user-secrets set "Jwt:Key" "your-32-char-minimum-secret-key!!"
dotnet user-secrets set "Redis:Configuration" "localhost:6379,password=your_redis_password"
```

### 4. Apply Database Migrations
```bash
cd services/backend
dotnet ef database update --project src/Adapters/Infrastructure --startup-project src/Adapters/WebApi
```

### 5. Run the Backend
```bash
dotnet run --project services/backend/src/Adapters/WebApi
```

API available at: `http://localhost:8080` (main port)
Management endpoints at: `http://localhost:9090` (metrics, health checks)

### 6. Verify Health
```bash
curl http://localhost:9090/health
```

### 7. Access API Documentation
Open your browser to `http://localhost:8080/swagger` for interactive API documentation.

---

## Development Workflow

### Branch Naming
```
feature/<ticket-id>-<short-slug>
bugfix/<ticket-id>-<short-slug>
hotfix/<description>
```

### Making Changes

1. **Create feature branch**
   ```bash
   git checkout -b feature/ABC-123-add-agent-rating
   ```

2. **Follow Hexagonal Architecture**
   - Pure business logic → `Core/Domain`
   - Use cases → `Core/Application/UseCases`
   - External integrations → `Adapters/Infrastructure`
   - API endpoints → `Adapters/WebApi/Controllers`

3. **Run tests locally**
   ```bash
   bash scripts/ci.sh
   ```

4. **Open PR** following template in `docs/pull_request_template.md`

---

## Adding a New Feature

### Step-by-Step Checklist

1. **Design First** - Sketch CQRS commands/queries and events

2. **Create Domain Entity** (if needed)
   ```csharp
   // Core/Domain/Entities/MyEntity.cs
   public class MyEntity
   {
       public Guid Id { get; set; }
       // Properties...
   }
   ```

3. **Define Port (Interface)**
   ```csharp
   // Core/Application/Interfaces/IMyRepository.cs
   public interface IMyRepository
   {
       Task<MyEntity?> GetByIdAsync(Guid id, CancellationToken ct);
       Task CreateAsync(MyEntity entity, CancellationToken ct);
   }
   ```

4. **Create Use Case**
   ```csharp
   // Core/Application/UseCases/My/Create/CreateMyCommand.cs
   public sealed record CreateMyCommand(string Name) : IRequest<MyDto>;

   // Core/Application/UseCases/My/Create/CreateMyHandler.cs
   public sealed class CreateMyHandler : IRequestHandler<CreateMyCommand, MyDto>
   {
       private readonly IMyRepository _repo;

       public async Task<MyDto> Handle(CreateMyCommand cmd, CancellationToken ct)
       {
           var entity = new MyEntity { Name = cmd.Name };
           await _repo.CreateAsync(entity, ct);
           return entity.ToDto();
       }
   }
   ```

5. **Implement Repository**
   ```csharp
   // Adapters/Infrastructure/Repositories/MyRepository.cs
   public class MyRepository(ApplicationDbContext db) : IMyRepository
   {
       public async Task<MyEntity?> GetByIdAsync(Guid id, CancellationToken ct)
           => await db.MyEntities.FindAsync([id], ct);

       public async Task CreateAsync(MyEntity entity, CancellationToken ct)
       {
           await db.MyEntities.AddAsync(entity, ct);
           await db.SaveChangesAsync(ct);
       }
   }
   ```

6. **Register in DI**
   ```csharp
   // Adapters/Infrastructure/InfrastructureServiceCollectionExtensions.cs
   services.AddScoped<IMyRepository, MyRepository>();
   ```

7. **Add Controller Endpoint**
   ```csharp
   // Adapters/WebApi/Controllers/MyController.cs
   [ApiController]
   [Route("api/[controller]")]
   public class MyController(IMediator mediator) : ControllerBase
   {
       [HttpPost]
       public async Task<IActionResult> Create([FromBody] CreateMyRequest req)
       {
           var result = await mediator.Send(new CreateMyCommand(req.Name));
           return Ok(result);
       }
   }
   ```

8. **Add Migration** (if DB changes)
   ```bash
   dotnet ef migrations add AddMyEntity --project src/Adapters/Infrastructure --startup-project src/Adapters/WebApi
   ```

9. **Write Tests**
   - Unit test for handler logic
   - Integration test for repository
   - API test for controller

10. **Update Documentation**
    - Add to `api-contracts.md`
    - Update `data-models.md`
    - Create ADR if architectural change

---

## Testing

### Running Tests

```bash
# All tests
dotnet test services/backend/Backend.sln

# Unit tests only
dotnet test services/backend/tests/Unit/**/*.csproj

# Integration tests (requires Docker)
dotnet test services/backend/tests/Integration/**/*.csproj

# Specific test project
dotnet test services/backend/tests/Unit/Core/Application.Tests

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Test Categories

| Type | Location | Purpose |
|------|----------|---------|
| Unit | `tests/Unit/Core/Domain.Tests` | Entity logic |
| Unit | `tests/Unit/Core/Application.Tests` | Use case handlers |
| Unit | `tests/Unit/Adapters/WebApi.Tests` | Controllers |
| Integration | `tests/Integration/` | Full stack with DB |

### Writing Tests

```csharp
// Unit test example
public class CreateMyHandlerTests
{
    private readonly Mock<IMyRepository> _repoMock = new();

    [Fact]
    public async Task Handle_ValidCommand_CreatesEntity()
    {
        // Arrange
        var handler = new CreateMyHandler(_repoMock.Object);
        var command = new CreateMyCommand("Test");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("Test", result.Name);
        _repoMock.Verify(r => r.CreateAsync(It.IsAny<MyEntity>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

---

## Database

### Running Migrations

```bash
cd services/backend

# Create migration
dotnet ef migrations add MigrationName --project src/Adapters/Infrastructure --startup-project src/Adapters/WebApi

# Apply migrations
dotnet ef database update --project src/Adapters/Infrastructure --startup-project src/Adapters/WebApi

# Revert migration
dotnet ef database update PreviousMigrationName --project src/Adapters/Infrastructure --startup-project src/Adapters/WebApi
```

### Database Connection

Default connection (development):
```
Host=localhost;Port=5433;Database=orchid_core;Username=orchid_admin;Password=<password>
```

### pgAdmin Access
- URL: `http://localhost:8888`
- Default: `admin@admin.com` / password from .env

---

## Docker Services

### Start All Services
```bash
docker-compose -f docker-compose.dev.yaml up -d
```

### Service Ports

| Service | Port | UI Port |
|---------|------|---------|
| PostgreSQL | 5433 | - |
| pgAdmin | - | 8888 |
| Redis | 6379 | 8082 (Commander) |
| RabbitMQ | 5672 | 15672 |
| Langfuse | 3033 | 3033 |
| ClickHouse | 8123 | - |
| MinIO | 9090 | 9091 |

### Stop Services
```bash
docker-compose -f docker-compose.dev.yaml down

# With data cleanup
docker-compose -f docker-compose.dev.yaml down -v
```

---

## Debugging

### Visual Studio / Rider
1. Open `services/backend/Backend.sln`
2. Set `WebApi` as startup project
3. Press F5

### VS Code
```json
// .vscode/launch.json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": ".NET Core Launch (web)",
      "type": "coreclr",
      "request": "launch",
      "program": "${workspaceFolder}/services/backend/src/Adapters/WebApi/bin/Debug/net9.0/WebApi.dll",
      "cwd": "${workspaceFolder}/services/backend/src/Adapters/WebApi"
    }
  ]
}
```

### Logging

Follow the [Logging Standards](./logging-standards.md) for log levels, structure, and event IDs.

Logs output to console in development. Key namespaces:
- `Microsoft.AspNetCore.SignalR` - SignalR debugging
- `Application.*` - Use case logging  
- `Infrastructure.*` - Adapter logging
- `WebApi.*` - Controller logging

**Enable Debug Logging:**

Add to `appsettings.local.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore.Database.Command": "Information",
      "Microsoft.AspNetCore.SignalR": "Debug"
    }
  }
}
```

For complete logging guidelines, see [Logging Standards](./logging-standards.md).

---

## Environment Variables

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `ConnectionStrings__DefaultConnection` | Yes | - | PostgreSQL connection |
| `OpenAI__ApiKey` | Yes | - | OpenAI API key |
| `Jwt__Key` | Yes | - | JWT signing key (32+ chars) |
| `Redis__Configuration` | Yes | - | Redis connection |
| `ASPNETCORE_ENVIRONMENT` | No | Production | Runtime environment |
| `FrontendUrl` | Yes | - | CORS allowed origin |
| `Langfuse__Enabled` | No | false | Enable LLM telemetry |

---

## Code Style

### Formatting
```bash
dotnet format services/backend/Backend.sln
```

### Conventions
- Nullable enabled everywhere (`#nullable enable`)
- Async/await for all I/O
- Domain-specific exceptions
- English code and comments
- MediatR for CQRS
- Repository pattern for data access

### File Organization
- One class per file
- Match namespace to folder structure
- Group by feature, not by type

---

## Troubleshooting

### Common Issues

| Issue | Solution |
|-------|----------|
| Database connection failed | Check PostgreSQL is running, verify connection string |
| Redis connection failed | Ensure Redis is up, check password |
| JWT validation errors | Verify `Jwt:Key` matches, check token expiration |
| Migration errors | Delete `Migrations` folder, regenerate |
| Port conflicts | Check no other services on 5433, 6379, 5672 |

### Logs Location
- Development: Console output
- Container logs: `docker logs <container_name>`

### Reset Database
```bash
# Drop and recreate
docker-compose -f docker-compose.dev.yaml down -v
docker-compose -f docker-compose.dev.yaml up -d database
dotnet ef database update --project src/Adapters/Infrastructure --startup-project src/Adapters/WebApi
```

---

## API Documentation

### OpenAPI/Swagger

When running in development mode, interactive API documentation is available at:

```
http://localhost:8080/swagger
```

The API documentation includes:
- All REST endpoints with request/response schemas
- JWT Bearer authentication configuration
- Try-it-out functionality for testing endpoints

### API Contracts

For offline reference, see `docs/api-contracts.md` which contains the complete API specification including:
- All controller endpoints
- Request/response formats
- SignalR hub documentation
- Rate limiting information

### Generating OpenAPI Spec

The OpenAPI JSON specification is available at `/swagger/v1/swagger.json` and can be used with tools like:
- Postman for API testing
- OpenAPI Generator for client SDK generation
- Redoc for alternative documentation rendering
