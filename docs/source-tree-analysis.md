# Orchid Core - Source Tree Analysis

## Repository Structure

```
orchid-core/
├── .github/
│   └── agents/                    # BMad agent configurations (10 files)
├── _bmad/                         # BMad Method tooling
│   ├── bmm/                       # BMM module
│   └── core/                      # Core BMad workflows
├── _bmad-output/                  # Generated artifacts
│   ├── planning-artifacts/        # PRD, architecture docs
│   └── implementation-artifacts/  # Stories, sprint tracking
├── docs/                          # Project documentation
│   └── adr/                       # Architecture Decision Records
├── infrastructure/                # Deployment configuration
│   └── overlays/                  # Kubernetes Kustomize overlays
├── scripts/                       # Utility scripts
│   └── ci.sh                      # CI/CD script
├── services/
│   └── backend/                   # Main .NET backend service
│       ├── docs/                  # Backend-specific docs
│       │   └── adr/               # Backend ADRs
│       ├── scripts/               # Backend scripts
│       ├── src/                   # Source code
│       │   ├── Core/              # Business logic layer
│       │   │   ├── Domain/        # Entities, Events, Value Objects
│       │   │   └── Application/   # Use Cases, Interfaces
│       │   └── Adapters/          # External integrations
│       │       ├── Infrastructure/# Database, Cache, AI, etc.
│       │       ├── WebApi/        # REST API controllers
│       │       ├── Workers/       # Background processors
│       │       └── CLI/           # Command-line tools
│       └── tests/                 # Test projects
│           ├── Unit/              # Unit tests
│           └── Integration/       # Integration tests
├── AGENTS.md                      # LLM Agent context guide
├── CHANGELOG.md                   # Version history
├── CLAUDE.md                      # Same as AGENTS.md
├── docker-compose.dev.yaml        # Development services
├── docker-compose.test.yml        # Test environment
├── Dockerfile                     # Container image
├── init-db.sql                    # Database initialization
└── readme.md                      # Project README
```

---

## Core Layer (`services/backend/src/Core/`)

### Domain (`Core/Domain/`)
Pure business logic, no external dependencies.

```
Domain/
├── Common/
│   ├── EntityBase.cs              # Base entity class
│   └── IDomainEvent.cs            # Domain event interface
├── Entities/                      # 44+ business entities
│   ├── UserEntity.cs
│   ├── AgentEntity.cs
│   ├── TeamEntity.cs
│   ├── ChatSessionEntity.cs
│   ├── SubscriptionEntity.cs
│   ├── CreditConsumptionEntity.cs
│   ├── DocumentEntity.cs
│   ├── KnowledgeBaseFileEntity.cs
│   ├── PluginEntity.cs
│   ├── PersonalityTemplateEntity.cs
│   ├── RefreshTokenEntity.cs
│   ├── LoginHistoryEntity.cs
│   ├── AuditLogEntity.cs
│   ├── NotificationEntity.cs
│   └── Spreadsheet/               # Spreadsheet entities
│       ├── WorkbookEntity.cs
│       ├── WorksheetEntity.cs
│       ├── CellEntity.cs
│       └── ...
├── Enums/                         # Domain enumerations
│   ├── DocumentEnum.cs
│   ├── InteractionType.cs
│   ├── PaymentUnit.cs
│   ├── SignUpMethod.cs
│   ├── SubscriptionPlanEnum.cs
│   └── TeamInteractionPolicy.cs
├── Events/                        # Domain events
│   ├── UserCreatedEvent.cs
│   ├── CreditsConsumedEvent.cs
│   ├── ProjectCreatedEvent.cs
│   └── ...
├── Exceptions/                    # Domain exceptions
│   ├── EntityNotFoundException.cs
│   ├── CreditLimitExceededException.cs
│   └── ConcurrencyException.cs
└── ValueObjects/                  # Immutable value objects
    └── Spreadsheet/
        ├── CellAddress.cs
        ├── CellData.cs
        └── WorkbookContext.cs
```

### Application (`Core/Application/`)
Use cases, ports (interfaces), and orchestration.

```
Application/
├── Common/                        # Shared utilities
├── DTOs/                          # Data transfer objects
├── Interfaces/                    # 86+ port interfaces
│   ├── IUserRepository.cs
│   ├── IAgentRepository.cs
│   ├── IChatCompletionPort.cs
│   ├── IVectorStorePort.cs
│   ├── ICacheStore.cs
│   ├── IFileStorageService.cs
│   ├── ITokenService.cs
│   ├── IRateLimitService.cs
│   ├── ITelemetryClient.cs
│   └── Spreadsheet/               # Spreadsheet interfaces
│       ├── ISpreadsheetAnalysisService.cs
│       ├── ITableDetectionService.cs
│       └── ...
└── UseCases/                      # 100+ CQRS use cases
    ├── ActionCost/
    ├── Agent/
    │   ├── CreateAgent/
    │   ├── ListAgents/
    │   ├── SoftDeleteAgent/
    │   └── VerifyAgentAccess/
    ├── Audio/
    │   ├── GenerateSpeech/
    │   ├── NormalizeAudio/
    │   └── TranscribeAudio/
    ├── Auth/
    │   ├── ForgotPassword/
    │   ├── GoogleLogin/
    │   ├── Login/
    │   ├── Register/
    │   └── ResetPassword/
    ├── ChatSession/
    ├── Dashboard/
    ├── KnowledgeBase/
    ├── Marketplace/
    ├── MediaCenter/
    ├── Message/
    ├── PersonalityTemplate/
    ├── Plugin/
    ├── Project/
    ├── Spreadsheet/
    ├── Subscription/
    ├── Team/
    ├── User/
    └── Workflow/
```

---

## Adapters Layer (`services/backend/src/Adapters/`)

### Infrastructure (`Adapters/Infrastructure/`)
Outbound adapters implementing ports.

```
Infrastructure/
├── Ai/                            # AI/ML integrations
│   └── SemanticKernel/
│       ├── Plugins/               # SK plugins
│       └── SemanticKernelServiceCollectionExtensions.cs
├── Auth/                          # Authentication services
│   ├── JwtTokenService.cs
│   ├── PasswordHasher.cs
│   ├── PasswordValidator.cs
│   ├── JwtBlacklistService.cs
│   └── RateLimitService.cs
├── Cache/                         # Caching implementations
│   └── RedisCacheStore.cs
├── Data/                          # Database context
│   └── ApplicationDbContext.cs
├── Logging/                       # Logging infrastructure
│   └── SafeLogger.cs
├── Messaging/                     # Message bus
│   └── MassTransitConfiguration.cs
├── Migrations/                    # EF Core migrations
│   ├── 20250622153947_InitialCreate.cs
│   ├── 20250622162643_AddAuditLogs.cs
│   ├── 20251213192623_SyncModelChanges.cs
│   ├── 20251219000000_AddRefreshTokens.cs
│   ├── 20251222225554_PendingModelChanges.cs
│   └── ApplicationDbContextModelSnapshot.cs
├── Persistence/                   # EF configurations
├── Providers/                     # External providers
├── Repositories/                  # Repository implementations
│   ├── UserRepository.cs
│   ├── AgentRepository.cs
│   ├── ChatSessionRepository.cs
│   └── ...
├── Services/                      # Service implementations
│   ├── CreditTrackingService.cs
│   ├── FileStorageService.cs
│   └── ...
├── Storage/                       # File storage
│   ├── LocalFileStorage.cs
│   └── GcsFileStorage.cs
├── Telemetry/                     # Observability
│   └── LangfuseTelemetryClient.cs
├── VectorStore/                   # Vector embeddings
│   └── PgVectorStore.cs
└── InfrastructureServiceCollectionExtensions.cs
```

### WebApi (`Adapters/WebApi/`)
Inbound HTTP adapter.

```
WebApi/
├── Adapters/                      # Request/Response adapters
├── Configuration/                 # App configuration
├── Controllers/                   # 22 API controllers
│   ├── ActivitiesController.cs
│   ├── AgentsController.cs
│   ├── AuthController.cs
│   ├── BillingPreferenceController.cs
│   ├── ChatSessionsController.cs
│   ├── CreditHistoryController.cs
│   ├── CreditValidationController.cs
│   ├── DashboardController.cs
│   ├── FilesController.cs
│   ├── KnowledgeBaseController.cs
│   ├── MarketplaceController.cs
│   ├── MediaCenterController.cs
│   ├── PersonalityTemplatesController.cs
│   ├── PluginController.cs
│   ├── PluginsController.cs
│   ├── ProjectController.cs
│   ├── PublicMarketplaceController.cs
│   ├── SpreadsheetController.cs
│   ├── SubscriptionController.cs
│   ├── TeamsController.cs
│   ├── UserController.cs
│   └── WorkflowsController.cs
├── Extensions/                    # Extension methods
├── Hubs/                          # SignalR hubs
│   └── ChatHub.cs
├── Middleware/                    # HTTP middleware
│   └── JwtBlacklistMiddleware.cs
├── Models/                        # API models
├── Properties/                    # Launch settings
├── Resources/                     # Static resources
├── appsettings.json               # Base configuration
├── appsettings.Development.json   # Dev configuration
├── appsettings.Local.json         # Local configuration
├── appsettings.Testing.json       # Test configuration
├── Program.cs                     # Application entry point
├── ServiceCollectionExtensions.cs # DI registration
└── WebApi.csproj                  # Project file
```

### Workers (`Adapters/Workers/`)
Background job processors.

```
Workers/
├── AudioProcessing/               # Audio worker (separate project)
│   ├── AudioProcessingWorker.cs
│   └── AudioProcessing.csproj
├── RecycleBinCleanupWorker.cs     # Cleanup worker
└── Workers.csproj
```

### CLI (`Adapters/CLI/`)
Command-line tools.

```
CLI/
└── SpreadsheetCLI/                # Spreadsheet processing CLI
    ├── Tests/
    ├── Program.cs
    ├── README.md
    └── SpreadsheetCLI.csproj
```

---

## Tests (`services/backend/tests/`)

```
tests/
├── Unit/
│   ├── Core/
│   │   ├── Application.Tests/     # Use case tests
│   │   └── Domain.Tests/          # Entity tests
│   └── Adapters/
│       ├── Infrastructure.Tests/  # Adapter tests
│       └── WebApi.Tests/          # Controller tests
└── Integration/
    ├── Agent.IntegrationTests/
    ├── AgentPluginLoading.IntegrationTests/
    ├── AudioProcessing.IntegrationTests/
    ├── Cache.IntegrationTests/
    ├── CreditSystem.IntegrationTests/
    ├── CreditTracking.IntegrationTests/
    ├── Spreadsheet.IntegrationTests/
    ├── SpreadsheetCLI.IntegrationTests/
    ├── Subscription.IntegrationTests/
    ├── Telemetry.IntegrationTests/
    ├── VectorStore.IntegrationTests/
    └── WebApi.IntegrationTests/
```

---

## Key Entry Points

| Entry Point | Location | Purpose |
|-------------|----------|---------|
| WebApi | `WebApi/Program.cs` | HTTP API server |
| AudioWorker | `Workers/AudioProcessing/Program.cs` | Audio processing |
| SpreadsheetCLI | `CLI/SpreadsheetCLI/Program.cs` | CLI tool |

## Critical Directories

| Directory | Purpose |
|-----------|---------|
| `Core/Domain/Entities/` | Business entities |
| `Core/Application/Interfaces/` | Port definitions |
| `Core/Application/UseCases/` | Business logic |
| `Adapters/WebApi/Controllers/` | API endpoints |
| `Adapters/Infrastructure/Repositories/` | Data access |
| `Adapters/Infrastructure/Migrations/` | DB schema |
