# Orchid Labs Core – LLM Agent Context Guide

*Version 1.0 – 2025-05-03*

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
src/
├── Core/
│   ├── Domain/
│   └── Application/
├── Adapters/
│   ├── Infrastructure/
│   ├── WebApi/
│   └── Workers/
└── tests/
```

Any new code **must** fit one of these slots. If none apply, open an ADR first.

### 3.1 Key REST Endpoints

| Route             | Purpose                                       |
|-------------------|-----------------------------------------------|
| `/knowledge-base` | Upload documents to build the retrieval store |

| `/auth/register` | Create a new user with roles |
| `/auth/login` | Obtain JWT token |
---

## 4  Feature Implementation Playbook

### 4.1 Decision Tree

| Question                         | Action                                        |
|----------------------------------|-----------------------------------------------|
| *Is it pure business logic?*     | Add Entity/Value & Use-Case in **Core**       |
| *Needs external tech?*           | Define a **Port** → implement new **Adapter** |
| *Requires a new UI/API surface?* | Extend **WebApi** or **Worker** adapter  |

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

## 8  AI & Semantic Kernel Rules

1. Kernel instances are resolved from DI via `AddSemanticKernel` (see `SemanticKernelServiceCollectionExtensions`). Do *
   *not** instantiate `Kernel` manually.
2. Register plugins in `Adapters.Infrastructure.Ai.SemanticKernel.Plugins`.
3. Long-running AI calls require **cancellation tokens**.
4. Stream outputs when size > 50 tokens to reduce latency.

## 8.1 Tool Invocation Activity Logging

When registering new plugins or tooling functions, ensure that each exposed method emits an activity event so the
frontend can render the call, its parameters, and the result. Follow these steps:

- In your plugin class (e.g. under `Infrastructure.Ai.SemanticKernel.Plugins`), annotate the method with
  `[KernelFunction("your_tool_name")]`.
- After computing the result (typically serialized to JSON), call the helper:
  ```csharp
  PublishTool("your_tool_name", new { /* parameter1, parameter2, ... */ }, resultJson);
  ```
  The `PublishTool` implementation uses
  `IActivityPublisher.PublishAsync("tool_invocation", new { tool, parameters, result })` under the hood.
- Ensure that your `parameters` object and `result` are serializable to JSON; strings or POCOs are supported.
- No frontend changes are required once the generic ActivityMessage component is in place: it will detect
  `payload.tool`, render the tool name, parameters, and result automatically.

This pattern enables any new tooling (not just Excel plugins) to surface invocation details in the chat UI without
additional customization.

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

## 13  Glossary (Agent Quick-Look)

| Term                    | Meaning                                     |
|-------------------------|---------------------------------------------|
| **Port**                | Interface in Application layer              |
| **Adapter**             | Tech-specific impl of a Port                |
| **Inbound**             | WebApi and Worker projects under `src/Adapters` |
| **Domain Event**        | Internal business fact                      |
| **Integration Message** | Brokered event via RabbitMQ                 |
| **ADR**                 | Architectural Decision Record               |

---

*Happy coding, dear LLM agent! Stick to this map and you'll never get lost.*

