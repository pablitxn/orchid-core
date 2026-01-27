# Orchid Labs Core – LLM Agent Context Guide

*Version 1.4 – 2026-01-27*

---

## 1  Purpose

This document equips an **LLM-driven engineering agent** with every rule, map, and checklist required to:

* **Add new business features** without breaking the Hexagonal contract.
* **Extend infrastructure** (e.g., swap OpenAI → Gemini) through proper Adapters.
* **Author tests** that preserve coverage and follow the AAD rule.
* **Write & update ADRs** so architectural knowledge never drifts.

Keep this file up-to-date—**it is the single source of truth** for automated contributors.

---

## 2  Architecture Recap (TL;DR)

1. **Hexagonal Layers**
    * *Domain* ➜ *Application* ➜ *Adapters* (Inbound + Outbound)
2. **Ports** live in `Core/Application/Interfaces`.
3. **Adapters** implement those ports under `src/Adapters/*`.
4. **WebApi** and **Worker** projects live under `src/Adapters` as inbound surfaces.
5. **Domain Events** and **RabbitMQ messages** carry all side-effects.

> 📚 Full details: see `architecture.md` and each ADR.

---

## 3  Directory Overview

A trimmed, tech-agnostic layout:

```plaintext
services/backend/
├── src/
│   ├── Core/
│   │   ├── Domain/           # Entities, Value Objects, Domain Events
│   │   └── Application/      # Use Cases, Interfaces (Ports), DTOs
│   └── Adapters/
│       ├── Infrastructure/   # Repositories, External Services, AI
│       ├── WebApi/           # Controllers, Hubs, Middleware
│       └── Workers/          # Background job processors
└── tests/                    # Unit and Integration tests
```

Any new code **must** fit one of these slots. If none apply, open an ADR first.

### 3.1 Key REST Endpoints

| Route | Purpose |
|-------|---------|
| `/api/auth/*` | Authentication (register, login, OAuth, tokens) |
| `/api/agents` | Agent CRUD, ratings, access verification |
| `/api/chatsessions` | Chat session management |
| `/api/v2/chat-sessions/{id}/messages` | V2 Messages with cursor pagination |
| `/api/knowledge-base` | Document upload and RAG store |
| `/api/teams` | Multi-agent team collaboration |
| `/api/plugins` | Plugin management |
| `/api/plugin` | User plugin operations |
| `/api/workflows` | Workflow CRUD and purchases |
| `/api/marketplace` | User marketplace items and purchases |
| `/api/public/marketplace` | Public plugin/workflow browsing |
| `/api/subscription` | Credits and subscription management |
| `/api/billingpreference` | Billing settings |
| `/api/credit-validation` | Pre-validate credit costs |
| `/api/credithistory` | Credit consumption history |
| `/api/dashboard` | Analytics and metrics |
| `/api/personality-templates` | Agent personality presets |
| `/api/features` | Feature flags |
| `/api/files` | File upload/download |
| `/api/media-center` | Media asset management |
| `/api/activities` | Backend activity history |
| `/api/user` | User profile |
| `/health` | Health checks (management port) |

> 📚 Full API documentation: see `docs/api-contracts.md`
---

## 4  Feature Implementation Playbook

### 4.1 Decision Tree

| Question                         | Action                                        |
|----------------------------------|-----------------------------------------------|
| *Is it pure business logic?*     | Add Entity/Value & Use-Case in **Core**       |
| *Needs external tech?*           | Define a **Port** → implement new **Adapter** |
| *Requires a new UI/API surface?* | Extend **WebApi** or **Worker** adapter       |

### 4.2 Step-by-Step Checklist

1. **Create Branch** – `feature/<ticket-id>-<short-slug>`.
2. **Design First** – Sketch CQRS objects & events.
3. **Add Port**: `IYourService` under `Core/Application/Interfaces`.
4. **Implement Use-Case** (§ 9 provides skeleton).
5. **Build Adapter**: `Adapters/Infrastructure/<Tech>/<Name>Service.cs`.
6. **Wire DI** via `InfrastructureServiceCollectionExtensions`.
7. **Emit Domain Events** where side-effects begin.
8. **Write Tests** (Unit + Integration as needed).
9. **Update Docs** – modify `architecture.md` if layout changed.
10. **Create/Update ADR** – drop file `docs/adr/adr-XXXX-*.md`.
11. **Run CI locally** (`./scripts/ci.sh`) ⇒ green.
12. **Open PR** with description template `docs/pull_request_template.md`.

---

## 5  Coding Conventions

| Area            | Rule                                                                    |
|-----------------|-------------------------------------------------------------------------|
| **Language**    | Code & comments **English only**.                                       |
| **Nullability** | `#nullable enable` enforced everywhere.                                 |
| **Exceptions**  | Throw domain-specific; never expose stack traces to clients.            |
| **Async**       | All I/O is `async`/`await`; never `Task.Result`.                        |
| **Security**    | Validate every external payload, apply OWASP Top-10 mitigations.        |
| **Style**       | Follow `dotnet format` default profile; CI fails on drift.              |
| **Patterns**    | Prefer **Mediator + CQRS**; use **Strategy** for interchangeable algos. |

---

## 5.1 Logging Standards (LLM Rules)

- Follow [docs/logging-standards.md](docs/logging-standards.md).
- Use structured logging with message templates; avoid string interpolation.
- Never log secrets, tokens, or PII.
- Use the correct log level for the situation (Information for business events).
- Guard expensive log data with `IsEnabled` checks.
- Use event IDs for important logs when available.

---

## 5.2 Pattern Consistency (CQRS & Hexagonal)

### 5.2.1 Command/Query Naming

Follow strict CQRS naming conventions:
- **Commands** (mutations): Use `*Command` suffix → `CreateAgentCommand`, `DeletePluginCommand`
- **Queries** (reads): Use `*Query` suffix → `GetUserQuery`, `ListAgentsQuery`

**Never** name a read operation as a Command. If it doesn't modify state, it's a Query.

### 5.2.2 DTO Naming Convention

All Data Transfer Objects must use the `*Dto` suffix:
- **Response DTOs**: `AgentDto`, `UserDto`, `PluginDto`
- **List DTOs**: `ListAgentsDto`, `ListPublicPluginsDto`
- **Result DTOs**: `AgentRatingsDto`, `AddDocumentDto`

**Avoid** using `*Result` suffix for DTOs. Reserve "Result" for internal service contracts and enums.

### 5.2.3 Domain Exception Hierarchy

Use domain-specific exceptions from `Domain.Exceptions` instead of generic framework exceptions:

| Instead of...                     | Use...                              |
|-----------------------------------|-------------------------------------|
| `InvalidOperationException`       | Specific domain exception           |
| `ArgumentException`               | `ValidationException`               |
| `KeyNotFoundException`            | `EntityNotFoundException` subclass  |
| `UnauthorizedAccessException`     | `UnauthorizedDomainAccessException` |

Available domain exceptions:
```
Domain/Exceptions/
├── EntityNotFoundException.cs      # Base for not-found errors
├── AgentNotFoundException.cs       # Agent-specific
├── PluginNotFoundException.cs      # Plugin-specific
├── UserNotFoundException.cs        # User-specific
├── SubscriptionNotFoundException.cs
├── WorkflowNotFoundException.cs
├── DocumentNotFoundException.cs
├── ValidationException.cs          # Input validation errors
├── UnauthorizedDomainAccessException.cs  # Access denied
├── InsufficientCreditsException.cs
├── CreditLimitExceededException.cs
├── AlreadySubscribedException.cs
├── AlreadyPurchasedException.cs
├── AgentDeletedException.cs
├── AgentNotInRecycleBinException.cs
├── OperationFailedException.cs
├── EmptyContentException.cs
└── ActiveSubscriptionRequiredException.cs
```

### 5.2.4 Handler Return Types

Queries should return DTOs, not entities, to maintain API contract isolation:

```csharp
// ✓ Correct - Returns DTO
public record ListAgentsQuery : IRequest<List<AgentDto>>;

// ✗ Avoid - Returns entity directly
public record ListAgentsQuery : IRequest<List<AgentEntity>>;
```

### 5.2.5 Exception Usage Example

```csharp
// ✓ Correct - Domain exceptions
if (agent == null)
    throw new AgentNotFoundException(request.AgentId);

if (agent.UserId != request.UserId)
    throw new UnauthorizedDomainAccessException("Agent", request.AgentId, request.UserId);

if (string.IsNullOrEmpty(content))
    throw new EmptyContentException();

// ✗ Avoid - Generic exceptions
throw new InvalidOperationException("Agent not found");
throw new UnauthorizedAccessException("Access denied");
```

---

## 6  Testing Matrix

| Layer       | Project Suffix      | Tools                 | Min Coverage    |
|-------------|---------------------|-----------------------|-----------------|
| Domain      | `.Tests`            | xUnit                 | 95 %            |
| Application | `.Tests`            | xUnit + Moq           | 90 %            |
| Adapters    | `.IntegrationTests` | TestContainers        | n/a (run in CI) |
| WebApi      | `.IntegrationTests` | WebApplicationFactory | —               |
| End-to-End  | `specs/`            | SpecFlow / Playwright | Best effort     |

> **AAD**: A pull request is rejected if overall coverage ↓.

---

## 7  Event & Messaging Guidelines

* **Domain Event** stays in-memory—publish an **Integration Message** only at Application edge.
* `RoutingKey = <entity>.<event>` (e.g., `project.created`).
* Messages are *immutable* records with explicit schema version.

---

## 8  AI & Agent Framework Rules

1. AI services are resolved from DI via `AddAgentFramework` (see `AgentFrameworkServiceCollectionExtensions`). Do **not** instantiate `IChatClient` or `OpenAIClient` manually.
2. Register tools in `Adapters/Infrastructure/Ai/AgentFramework/Tools/`.
3. Long-running AI calls require **cancellation tokens**.
4. Stream outputs when size > 50 tokens to reduce latency.

### 8.1 Creating New Tools

Tools implement the `ITool` interface and provide AI-callable functions:

```csharp
// Adapters/Infrastructure/Ai/AgentFramework/Tools/MyTool.cs
public sealed class MyTool : ITool
{
    public string SystemName => "my_tool";
    public string DisplayName => "My Tool";
    public string Description => "Does something useful.";
    public int CreditCost => 1;
    public bool RequiresSubscription => false;

    [Description("Describe what this function does for the AI")]
    public async Task<string> DoSomethingAsync(
        [Description("Parameter description")] string input,
        CancellationToken ct = default)
    {
        // Implementation here
        var result = /* ... */;

        // Emit activity for frontend
        await _activity.PublishAsync("tool_invocation",
            new { tool = "my_tool", parameters = new { input }, result }, ct);

        return result;
    }

    public IReadOnlyList<AIFunction> GetAIFunctions()
    {
        return new List<AIFunction>
        {
            AIFunctionFactory.Create(
                (string input, CancellationToken ct) => DoSomethingAsync(input, ct),
                "DoSomething",
                "Describe what this function does.")
        };
    }
}
```

### 8.2 Registering Tools

Register new tools in `AgentFrameworkServiceCollectionExtensions.AddAgentFrameworkTools()`:

```csharp
public static IServiceCollection AddAgentFrameworkTools(this IServiceCollection services)
{
    return services
        .AddTransient<Tools.VectorStoreTool>()
        .AddTransient<Tools.WebSearchTool>()
        .AddTransient<Tools.MyTool>(); // Add your tool here
}
```

### 8.3 Tool Invocation Activity Logging

Each tool method should emit an activity event so the frontend can render the call:

```csharp
await _activity.PublishAsync("tool_invocation",
    new { tool = "tool_name", parameters = new { /* params */ }, result = jsonResult }, ct);
```

The generic ActivityMessage component will detect `payload.tool` and render the tool name, parameters, and result automatically.

### 8.4 OpenAPI Plugin Integration

The Agent Framework supports **dynamic tool creation** via OpenAPI 3.0 specifications. This allows adding new API integrations without code changes.

> See ADR-0003 for architectural details: `docs/adr/adr-0003-openapi-plugin-integration.md`

#### Key Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `OpenApiTool` | `Infrastructure/Ai/Tools/` | Dynamic tool from OpenAPI spec |
| `OpenApiToolFactory` | `Infrastructure/Ai/Tools/` | Creates tools with spec caching |
| `IToolRegistry` | `Infrastructure/Ai/Abstractions/` | Unified tool resolution |
| `AdminOpenApiPluginsController` | `WebApi/Controllers/` | Admin CRUD endpoints |

#### Creating an OpenAPI Plugin

1. **Prepare your OpenAPI spec** (must be accessible via HTTP):

```yaml
openapi: 3.0.0
info:
  title: My Custom API
  version: 1.0.0
paths:
  /analyze:
    post:
      operationId: analyzeData
      summary: Analyze input data
      requestBody:
        content:
          application/json:
            schema:
              type: object
              properties:
                text:
                  type: string
      responses:
        '200':
          description: Analysis result
```

2. **Register the plugin via Admin API**:

```http
POST /api/admin/openapi-plugins
Content-Type: application/json

{
  "name": "My Custom API",
  "systemName": "my_custom_api",
  "description": "Performs custom analysis",
  "baseUrl": "https://api.myservice.com",
  "openApiSpecUrl": "/openapi.json",
  "priceCredits": 2,
  "isActive": true
}
```

3. **Assign to an agent** by adding the plugin ID to the agent's `PluginIds` array.

#### How It Works

1. **Tool Resolution**: `ToolRegistry` first checks builtin tools (sync), then queries the database for OpenAPI plugins (async)
2. **Spec Caching**: Specs are cached for 1 hour; expired cache is used as fallback on fetch failure
3. **Function Generation**: Each operation in the spec becomes an `AIFunction` the AI can invoke
4. **HTTP Execution**: The tool executes actual HTTP requests to the target API

#### Plugin Entity Fields

```csharp
public class PluginEntity
{
    // Standard fields...
    public string? PluginType { get; set; }        // "openapi" for OpenAPI plugins
    public string? BaseUrl { get; set; }           // API base URL
    public string? OpenApiSpecUrl { get; set; }    // Path to spec (default: /openapi.json)
    public string? OpenApiSpecCache { get; set; }  // Cached spec JSON
    public DateTime? SpecCachedAt { get; set; }    // Cache timestamp
    public int? TimeoutSeconds { get; set; }       // HTTP timeout (default: 30)
}
```

#### Admin API Endpoints

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/api/admin/openapi-plugins` | List all plugins |
| GET | `/api/admin/openapi-plugins/{id}` | Get plugin with functions |
| POST | `/api/admin/openapi-plugins` | Create plugin |
| PUT | `/api/admin/openapi-plugins/{id}` | Update plugin |
| DELETE | `/api/admin/openapi-plugins/{id}` | Delete plugin |
| POST | `/api/admin/openapi-plugins/{id}/refresh` | Force cache refresh |
| POST | `/api/admin/openapi-plugins/validate` | Validate spec URL |

#### Example: Python Plugin Server

See `docs/examples/openapi-plugin/` for a complete example using FastAPI:

```python
from fastapi import FastAPI
app = FastAPI()

@app.post("/analyze")
async def analyze(data: dict):
    return {"result": "analyzed", "sentiment": "positive"}

@app.get("/status")
async def status():
    return {"status": "healthy"}
```

---

## 9  Code Skeletons

### 9.1 Command + Handler

```csharp
// Core/Application/UseCases/Foo/BarBaz/BarBazCommand.cs
public sealed record BarBazCommand(Guid ProjectId) : IRequest<Unit>;

// Core/Application/UseCases/Foo/BarBaz/BarBazHandler.cs
public sealed class BarBazHandler : IRequestHandler<BarBazCommand, Unit>
{
    private readonly IFooService _foo;
    private readonly IEventPublisher _events;

    public BarBazHandler(IFooService foo, IEventPublisher events)
        => (_foo, _events) = (foo, events);

    public async Task<Unit> Handle(BarBazCommand cmd, CancellationToken ct)
    {
        var entity = await _foo.ProcessAsync(cmd.ProjectId, ct);
        await _events.PublishAsync(new BarBazCompletedEvent(entity.Id), ct);
        return Unit.Value;
    }
}
```

### 9.2 Adapter Test

```csharp
public class RedisCacheServiceTests : IAsyncLifetime
{
    private readonly TestcontainersContainer _redis =
        new TestcontainersBuilder<TestcontainersContainer>()
            .WithImage("redis:7-alpine").WithPortBinding(6379).Build();

    public async Task InitializeAsync() => await _redis.StartAsync();
    public async Task DisposeAsync() => await _redis.DisposeAsync();

    [Fact]
    public async Task SetAndGet_Works()
    {
        var svc = new RedisCacheService("localhost:6379");
        await svc.SetAsync("key", "value", TimeSpan.FromMinutes(1));
        var result = await svc.GetAsync<string>("key");
        Assert.Equal("value", result);
    }
}
```

---

## 10  Observability & Telemetry

* **Langfuse** spans wrap every Application handler and worker consumer. Register
  `AddLangfuseTelemetry` in all services so traces are recorded.
* **Serilog** sinks: Console (dev), Loki (k8s).
* **Prometheus** metrics from `/metrics` endpoint in WebApi.

---

## 11  Performance Guardrails

| Aspect       | Limit / Strategy                       |
|--------------|----------------------------------------|
| HTTP latency | p95 < 500 ms (non-AI)                  |
| Worker CPU   | 70 % average (HPA scales)              |
| AI tokens    | Stream + chunk to avoid memory spikes  |
| File uploads | ≤ 100 MB; reject larger via middleware |

---

## 12  Pull-Request Definition of Done

1. **Green CI**
2. **Coverage ≥ target**
3. **ADR updated** when architecture shifts
4. **Docs edited** (`architecture.md`, this file)
5. **CHANGELOG.md** entry added

---

## 13  Temporary Code & Pre-Production Checklist

When adding **temporary code** (migrations, feature flags, backwards-compatibility shims, startup seeders for retroactive fixes, etc.):

1. **Mark it clearly** with a `// TODO: Remove before production` comment in the code.
2. **Document it** in `docs/pre-production-checklist.md` with:
   - Date added
   - Purpose/context
   - Exact files and line numbers to modify/remove
   - Verification steps after removal

> **Important:** Before any production deployment, review `docs/pre-production-checklist.md` and complete all listed cleanup tasks.

Examples of temporary code that must be tracked:
- Database migration seeders (e.g., `UserPluginMigrationSeeder`)
- Feature flags for gradual rollouts
- Backwards-compatibility shims during API transitions
- Debug/diagnostic code for troubleshooting specific issues

---

## 14  Glossary (Agent Quick-Look)

| Term                    | Meaning                                         |
|-------------------------|-------------------------------------------------|
| **Port**                | Interface in Application layer                  |
| **Adapter**             | Tech-specific impl of a Port                    |
| **Inbound**             | WebApi and Worker projects under `src/Adapters` |
| **Domain Event**        | Internal business fact                          |
| **Integration Message** | Brokered event via RabbitMQ                     |
| **ADR**                 | Architectural Decision Record                   |

---

*Happy coding, dear LLM agent! Stick to this map and you'll never get lost.*
