---
stepsCompleted: [1, 2, 3]
inputDocuments:
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/product-brief-orchid-core-2026-01-04.md
  - docs/architecture.md
  - docs/data-models.md
  - docs/api-contracts.md
  - docs/project-overview.md
  - docs/development-guide.md
  - docs/source-tree-analysis.md
  - docs/adr/adr-0001-agent-flow-audit.md
workflowType: 'architecture'
project_name: 'orchid-core'
user_name: 'Pablo'
date: '2026-01-15'
---

# Architecture Decision Document

_This document builds collaboratively through step-by-step discovery. Sections are appended as we work through each architectural decision together._

## Project Context Analysis

### Requirements Overview

**Functional Requirements:**

This roadmap addresses 5 epics that transform orchid-core from experimental to production-ready:

1. **Epic 0000 - Stabilize MVP**: Security hardening, dead code removal, 85%+ test coverage
2. **Epic 0001 - Subscriptions**: Complete Stripe integration, credit system, billing flows
3. **Epic 0002 - Auth0 Integration**: Migrate from custom JWT to Auth0 with session continuity
4. **Epic 0003 - Agent Framework**: Standardize agent patterns for marketplace consistency
5. **Epic 0004 - Marketplace**: Plugin discovery, purchase, and distribution backend

**Non-Functional Requirements:**

| Requirement | Target | Architecture Impact |
|-------------|--------|---------------------|
| Test Coverage | ≥85% | All components must be unit-testable via DI |
| Security | 0 Critical/High | OWASP compliance, secret management, input validation |
| Performance | p95 < 500ms | Caching, async patterns, optimized queries |
| Scalability | 1000 concurrent | Connection pooling, rate limiting, stateless design |
| AI Latency | First token < 2s | Streaming responses, efficient context management |
| Maintainability | < 10% tech debt | Clean patterns, no shortcuts, comprehensive docs |

**Scale & Complexity:**

- Primary domain: Backend API (SaaS B2B hybrid)
- Complexity level: HIGH
- Key complexity drivers:
  - Real-time AI chat with streaming
  - Payment/billing integration
  - Auth provider migration
  - Plugin marketplace dynamics
  - Vector search for RAG

### Technical Constraints & Dependencies

**Architecture Constraints (Must Preserve):**
- Hexagonal Architecture (Ports & Adapters)
- CQRS pattern via MediatR
- Domain Events + Integration Messages pattern
- .NET 9 runtime, no downgrade

**Technology Stack (Locked):**
- PostgreSQL 17 + pgvector for data + embeddings
- Redis 7+ for distributed cache
- RabbitMQ + MassTransit for messaging
- Semantic Kernel 1.54.0 for AI orchestration
- Langfuse for LLM telemetry

**External Dependencies:**
- OpenAI API (existing, runtime)
- Auth0 (new, Epic 0002)
- Stripe (completion, Epic 0001)

### Cross-Cutting Concerns Identified

| Concern | Architectural Approach |
|---------|----------------------|
| **Authentication** | Transitional: support both custom JWT and Auth0 during migration |
| **Authorization** | Extend ownership model for marketplace (creator permissions) |
| **Credit/Billing** | Event-driven usage tracking, idempotent Stripe webhooks |
| **Observability** | Langfuse spans for AI, Prometheus metrics, structured logging |
| **Caching** | Redis with invalidation strategies for marketplace listings |
| **Messaging** | RabbitMQ for async jobs, subscription events, marketplace notifications |
| **Error Handling** | Domain exceptions, graceful degradation, retry policies |

## Existing Technical Foundation

_Brownfield project - documenting established architectural decisions._

### Primary Technology Domain

Backend API (.NET 9) with Hexagonal Architecture

### Language & Runtime

- **Language:** C# with .NET 9.0
- **Type Safety:** Nullable reference types enabled (`#nullable enable`)
- **Async Model:** async/await for all I/O operations
- **Exception Strategy:** Domain-specific exceptions, never expose stack traces

### Architecture Pattern

- **Core Pattern:** Hexagonal Architecture (Ports & Adapters)
- **Command/Query:** CQRS via MediatR
- **Data Access:** Repository pattern with EF Core
- **Events:** Domain Events (in-memory) + Integration Messages (RabbitMQ)

### Data Layer

| Component | Technology | Purpose |
|-----------|------------|---------|
| Primary DB | PostgreSQL 17 | All domain entities |
| Vector Store | pgvector extension | Embeddings for RAG |
| ORM | EF Core 9.0.5 | Data access, migrations |
| Cache | Redis 7+ | Sessions, rate limits, chat history |

### Messaging Infrastructure

- **Broker:** RabbitMQ 3+
- **Client:** MassTransit 8.4.0
- **Pattern:** Event-driven async processing
- **Routing:** `<entity>.<action>` (e.g., `audio.normalized`)

### AI/ML Stack

| Component | Technology | Purpose |
|-----------|------------|---------|
| Orchestration | Semantic Kernel 1.54.0 | LLM orchestration, plugins |
| LLM Provider | OpenAI GPT-4o | Chat completions |
| Embeddings | OpenAI | Vector embeddings for RAG |
| Telemetry | Langfuse | LLM tracing, cost tracking |

### API Layer

- **Framework:** ASP.NET Core 9.0
- **Real-time:** SignalR hubs for chat streaming
- **Documentation:** Swagger/OpenAPI 7.3.1
- **Controllers:** 22 REST controllers

### Security (Current State)

| Aspect | Implementation |
|--------|----------------|
| Authentication | JWT Bearer tokens (migrating to Auth0) |
| Password Hashing | PBKDF2-SHA256, 100k iterations |
| Rate Limiting | IP + Account-based via Redis |
| Cookies | HttpOnly, Secure, SameSite=Strict |
| Token Management | Refresh token rotation, blacklisting |

### Project Structure

```
src/
├── Core/
│   ├── Domain/           # Entities, Events, Value Objects, Exceptions
│   └── Application/      # Use Cases, Interfaces (Ports), DTOs
└── Adapters/
    ├── Infrastructure/   # Repositories, Services, AI, Cache (Outbound)
    ├── WebApi/           # Controllers, Hubs, Middleware (Inbound)
    ├── Workers/          # Background job processors (Inbound)
    └── CLI/              # Command-line tools (Inbound)
```

### Development Tooling

- **Build:** `dotnet build` / `dotnet run`
- **Testing:** xUnit + Moq + TestContainers
- **Formatting:** `dotnet format` default profile
- **CI:** `scripts/ci.sh`
- **Containers:** Docker Compose for local services

