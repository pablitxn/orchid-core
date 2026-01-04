# Orchid Core - Architecture Documentation

## Architecture Overview

Orchid Core implements **Hexagonal Architecture** (also known as Ports & Adapters or Clean Architecture), ensuring the core business logic is isolated from external dependencies and infrastructure concerns.

## Layer Structure

### 1. Domain Layer (`Core/Domain`)

The innermost layer containing pure business logic with no external dependencies.

**Components:**
- **Entities** (44+): Business objects with identity
  - `UserEntity`, `AgentEntity`, `TeamEntity`
  - `SubscriptionEntity`, `CreditConsumptionEntity`
  - `ChatSessionEntity`, `DocumentEntity`
  - `WorkbookEntity`, `WorksheetEntity`, `CellEntity`
  - `PluginEntity`, `PersonalityTemplateEntity`

- **Value Objects**: Immutable domain concepts
  - `CellAddress`, `CellData`, `WorkbookContext`

- **Domain Events**: Business facts
  - `UserCreatedEvent`, `CreditsConsumedEvent`
  - `ProjectCreatedEvent`, `SubscriptionUpdatedEvent`
  - `AudioNormalizedEvent`, `TranscriptionCompletedEvent`

- **Enums**: Domain-specific enumerations
  - `InteractionType`, `PaymentUnit`, `SignUpMethod`
  - `SubscriptionPlanEnum`, `TeamInteractionPolicy`

- **Exceptions**: Domain-specific errors
  - `EntityNotFoundException`, `CreditLimitExceededException`
  - `ConcurrencyException`

### 2. Application Layer (`Core/Application`)

Contains use cases and orchestrates domain logic.

**Components:**
- **Use Cases** (100+): CQRS Commands and Queries
  - Organized by feature: Agent, Auth, Chat, Dashboard, KnowledgeBase, Plugin, Spreadsheet, etc.
  - Each use case has: Command/Query + Handler + (optional) Result DTO

- **Interfaces (Ports)** (86+): Contracts for external dependencies
  - Repository interfaces: `IUserRepository`, `IAgentRepository`, etc.
  - Service interfaces: `IChatCompletionPort`, `IVectorStorePort`
  - Infrastructure interfaces: `ICacheStore`, `IFileStorageService`

- **DTOs**: Data transfer objects for API responses

**Example Use Case Structure:**
```
UseCases/
├── Agent/
│   ├── CreateAgent/
│   │   ├── CreateAgentCommand.cs
│   │   └── CreateAgentHandler.cs
│   ├── ListAgents/
│   │   ├── ListAgentsQuery.cs
│   │   └── ListAgentsHandler.cs
│   └── Common/
│       └── AgentDto.cs
```

### 3. Adapters Layer (`Adapters/`)

External-facing implementations organized by direction (inbound/outbound).

#### Inbound Adapters (Entry Points)

**WebApi** - REST API surface
- 22 Controllers handling HTTP requests
- SignalR Hubs for real-time communication
- Middleware for auth, error handling, logging
- Swagger/OpenAPI documentation

**Workers** - Background job processors
- `AudioProcessing` - Audio normalization and transcription
- `RecycleBinCleanupWorker` - Expired item cleanup

**CLI** - Command-line tools
- `SpreadsheetCLI` - Batch spreadsheet processing

#### Outbound Adapters (Infrastructure)

**Infrastructure** - External service implementations
- **Persistence**: EF Core DbContext, Repositories
- **Ai**: Semantic Kernel integration, plugins
- **Auth**: JWT service, password hashing, rate limiting
- **Cache**: Redis distributed cache
- **Messaging**: RabbitMQ via MassTransit
- **Storage**: Local and GCS file storage
- **Telemetry**: Langfuse LLM tracing
- **VectorStore**: pgvector embeddings

## Dependency Flow

```
Inbound Adapters → Application Layer → Domain Layer
                          ↓
                   Outbound Adapters (via Ports)
```

**Key Rules:**
1. Domain has NO external dependencies
2. Application depends only on Domain
3. Adapters depend on Application (and Domain)
4. Dependencies flow inward only

## Key Ports (Interfaces)

### Repository Ports
| Port | Purpose |
|------|---------|
| `IUserRepository` | User data access |
| `IAgentRepository` | Agent CRUD operations |
| `IChatSessionRepository` | Chat session management |
| `ISubscriptionRepository` | Subscription handling |
| `IKnowledgeBaseFileRepository` | Document storage |
| `IPluginRepository` | Plugin management |

### Service Ports
| Port | Purpose |
|------|---------|
| `IChatCompletionPort` | LLM chat completions |
| `IEmbeddingGeneratorPort` | Vector embeddings |
| `IVectorStorePort` | Semantic search |
| `IFileStorageService` | File upload/download |
| `ICacheStore` | Distributed caching |
| `ITokenService` | JWT generation |
| `IPasswordHasher` | Secure password hashing |
| `IRateLimitService` | Request rate limiting |

### AI/ML Ports
| Port | Purpose |
|------|---------|
| `IAskToExcelService` | Spreadsheet NL queries |
| `ISpreadsheetService` | Spreadsheet processing |
| `IFormulaTranslator` | NL to formula translation |
| `IFormulaExecutor` | Safe formula execution |
| `ITableDetectionService` | Table structure detection |

## CQRS Implementation

Commands and Queries are handled via **MediatR**:

```csharp
// Command example
public sealed record CreateAgentCommand(
    string Name,
    string? Description,
    Guid UserId
) : IRequest<AgentDto>;

// Handler example
public sealed class CreateAgentHandler : IRequestHandler<CreateAgentCommand, AgentDto>
{
    private readonly IAgentRepository _repo;
    private readonly IEventPublisher _events;

    public async Task<AgentDto> Handle(CreateAgentCommand cmd, CancellationToken ct)
    {
        var agent = new AgentEntity { /* ... */ };
        await _repo.CreateAsync(agent, ct);
        await _events.PublishAsync(new AgentCreatedEvent(agent.Id), ct);
        return agent.ToDto();
    }
}
```

## Event System

### Domain Events
- Raised within domain operations
- Processed synchronously in-memory
- Examples: `CreditsConsumedEvent`, `UserCreatedEvent`

### Integration Messages
- Published to RabbitMQ via MassTransit
- Consumed by Workers asynchronously
- Routing: `<entity>.<action>` (e.g., `audio.normalized`)

## Security Architecture

### Authentication Flow
1. User registers/logs in via `AuthController`
2. Server generates JWT + Refresh Token
3. Tokens set as HttpOnly cookies
4. Subsequent requests validated via JWT middleware
5. Refresh token rotation on renewal

### Security Layers
| Layer | Protection |
|-------|------------|
| Rate Limiting | IP + Account-based limits |
| Password | PBKDF2-SHA256, 100k iterations |
| JWT | Signature validation, blacklisting |
| Cookies | HttpOnly, Secure, SameSite=Strict |
| Input | Validation on all external payloads |

## AI Integration Architecture

```
┌─────────────────────────────────────────┐
│            Semantic Kernel               │
│  ┌─────────────────────────────────────┐│
│  │          Plugins                     ││
│  │  - Spreadsheet Plugin                ││
│  │  - Knowledge Base Plugin             ││
│  │  - Custom User Plugins               ││
│  └─────────────────────────────────────┘│
│  ┌─────────────────────────────────────┐│
│  │        Connectors                    ││
│  │  - OpenAI Connector                  ││
│  │  - pgvector Connector                ││
│  └─────────────────────────────────────┘│
└─────────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────┐
│           Langfuse Telemetry             │
│    (Traces, Spans, Token Usage)          │
└─────────────────────────────────────────┘
```

## Data Architecture

### Primary Database (PostgreSQL)
- All domain entities
- pgvector extension for embeddings
- EF Core migrations

### Cache Layer (Redis)
- Session data
- Rate limit counters
- Chat history
- Distributed locks

### Message Queue (RabbitMQ)
- Async job processing
- Event-driven workflows
- Worker communication

### File Storage
- Local storage (development)
- GCS/S3 (production)
- Knowledge base documents
- Audio files
- Media assets

## Configuration

Configuration is managed via:
- `appsettings.json` - Base configuration
- `appsettings.{Environment}.json` - Environment overrides
- Environment variables - Secrets and deployment config
- User secrets - Local development secrets

Key configuration sections:
- `ConnectionStrings` - Database connection
- `Jwt` - Token settings
- `Redis` - Cache configuration
- `OpenAI` - LLM provider
- `Langfuse` - Telemetry
- `FileStorage` - Storage provider
