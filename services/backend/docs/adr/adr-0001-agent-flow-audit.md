# ADR-0001: Agent Flow Audit

**Status:** Implemented
**Date:** 2025-05-03
**Last Updated:** 2025-12-22
**Authors:** Engineering Team

> **Note:** This ADR is fully implemented with all endpoints, handlers, tests, and background workers operational.

---

## Context

The Agent module is a core component of the Orchid platform that enables users to create, manage, and interact with AI agents. An audit of the entire agent flow was conducted to ensure architectural consistency, proper security controls, and optimal performance.

The agent flow includes:
- Creating agents with plugins
- Listing agents with visibility rules
- Updating agent configurations
- Soft-delete with recycle bin functionality
- Permanent deletion
- Access verification for plugin requirements

---

## Decision

### 1. CQRS Pattern Implementation

All agent operations follow the CQRS pattern with MediatR:

| Operation | Type | File |
|-----------|------|------|
| CreateAgent | Command | `CreateAgentCommand.cs` / `CreateAgentHandler.cs` |
| UpdateAgent | Command | `UpdateAgentCommand.cs` / `UpdateAgentHandler.cs` |
| SoftDeleteAgent | Command | `SoftDeleteAgentCommand.cs` / `SoftDeleteAgentHandler.cs` |
| RestoreAgent | Command | `RestoreAgentCommand.cs` / `RestoreAgentHandler.cs` |
| PermanentDeleteAgent | Command | `PermanentDeleteAgentCommand.cs` / `PermanentDeleteAgentHandler.cs` |
| ListAgents | Query | `ListAgentsQuery.cs` / `ListAgentsHandler.cs` |
| ListRecycleBinAgents | Query | `ListRecycleBinAgentsQuery.cs` / `ListRecycleBinAgentsHandler.cs` |
| VerifyAgentAccess | Query | `VerifyAgentAccessQuery.cs` / `VerifyAgentAccessHandler.cs` |
| AddPluginToAgent | Command | `AddPluginToAgentCommand.cs` / `AddPluginToAgentHandler.cs` |
| RemovePluginFromAgent | Command | `RemovePluginFromAgentCommand.cs` / `RemovePluginFromAgentHandler.cs` |
| RateAgent | Command | `RateAgentCommand.cs` / `RateAgentHandler.cs` |
| GetAgentRatings | Query | `GetAgentRatingsQuery.cs` / `GetAgentRatingsHandler.cs` |

### 2. Repository Interface Design

The `IAgentRepository` interface provides optimized methods for common operations:

```csharp
public interface IAgentRepository
{
    Task<AgentEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<List<AgentEntity>> ListAsync(CancellationToken cancellationToken);
    Task<List<AgentEntity>> ListByUserAsync(Guid? userId, CancellationToken cancellationToken);
    Task CreateAsync(AgentEntity agent, CancellationToken cancellationToken);
    Task UpdateAsync(AgentEntity agent, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<List<AgentEntity>> ListRecycleBinAsync(CancellationToken cancellationToken);
    Task<List<AgentEntity>> ListRecycleBinByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<List<AgentEntity>> ListExpiredRecycleBinAsync(CancellationToken cancellationToken);
    Task<AgentEntity?> GetWithPluginsAsync(Guid id, CancellationToken cancellationToken);
}
```

**Key improvement (MED-001):** `ListByUserAsync` performs filtering at the database level instead of in-memory, improving performance for users with many agents.

### 3. Agent Stats with Real Data

The `IChatSessionRepository` was extended to provide real-time statistics:

```csharp
Task<int> CountByAgentIdAsync(Guid agentId, CancellationToken cancellationToken);
Task<DateTime?> GetLastUsedByAgentIdAsync(Guid agentId, CancellationToken cancellationToken);
Task<Dictionary<Guid, (int Count, DateTime? LastUsed)>> GetStatsForAgentsAsync(
    IEnumerable<Guid> agentIds,
    CancellationToken cancellationToken);
```

**Key improvement (MED-002):** `GetStatsForAgentsAsync` retrieves stats for all agents in a single query, eliminating N+1 query problems when listing agents.

### 4. Agent Rating System

Users can rate agents from 1 to 5 stars with an optional comment. Each user can only rate an agent once, but can update their rating.

#### Rating Entity

```csharp
public class AgentRatingEntity
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public Guid UserId { get; set; }
    public int Rating { get; set; }  // 1-5
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

#### Rating Repository Interface

```csharp
public interface IAgentRatingRepository
{
    Task<AgentRatingEntity?> GetByAgentAndUserAsync(Guid agentId, Guid userId, CancellationToken cancellationToken);
    Task<List<AgentRatingEntity>> ListByAgentAsync(Guid agentId, CancellationToken cancellationToken);
    Task<double?> GetAverageRatingAsync(Guid agentId, CancellationToken cancellationToken);
    Task<Dictionary<Guid, double?>> GetAverageRatingsForAgentsAsync(IEnumerable<Guid> agentIds, CancellationToken cancellationToken);
    Task CreateAsync(AgentRatingEntity rating, CancellationToken cancellationToken);
    Task UpdateAsync(AgentRatingEntity rating, CancellationToken cancellationToken);
}
```

**Key improvement (MED-003):** `GetAverageRatingsForAgentsAsync` retrieves ratings for all agents in a single query, following the same batch pattern as stats retrieval.

#### API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/agents/{id}/rate` | POST | Rate an agent (creates or updates user's rating) |
| `/api/agents/{id}/ratings` | GET | Get all ratings for an agent with average and count |
| `/api/agents/{id}/verify-access` | GET | Verify if user has access to use an agent |

### 5. Complete API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/agents` | GET | List all accessible agents (public + owned) |
| `/api/agents/my-agents` | GET | List only agents owned by current user |
| `/api/agents` | POST | Create a new agent |
| `/api/agents/{id}` | PUT | Update an agent (owner only) |
| `/api/agents/{id}` | DELETE | Soft delete an agent (owner only) |
| `/api/agents/recycle-bin` | GET | List agents in recycle bin |
| `/api/agents/{id}/restore` | POST | Restore an agent from recycle bin |
| `/api/agents/{id}/permanent` | DELETE | Permanently delete an agent |
| `/api/agents/{id}/rate` | POST | Rate an agent |
| `/api/agents/{id}/ratings` | GET | Get agent ratings |
| `/api/agents/{id}/verify-access` | GET | Verify user access to agent |

### 6. Visibility and Access Control

Agents have two visibility modes:

| Mode | Behavior |
|------|----------|
| `IsPublic = true` | Visible to all users |
| `IsPublic = false` | Visible only to owner (`UserId`) |

Access verification (`VerifyAgentAccessHandler`) checks:
1. Agent existence
2. Visibility rules (public or owned)
3. Plugin availability (user must own all required plugins)

### 7. Soft Delete with Recycle Bin

Agents are not permanently deleted immediately. Instead:

1. **SoftDeleteAgent:** Sets `IsDeleted = true`, `IsInRecycleBin = true`, and `RecycleBinExpiresAt = UtcNow + 30 days`
2. **RestoreAgent:** Clears deletion flags and expiration
3. **PermanentDeleteAgent:** Requires agent to be in recycle bin before permanent deletion
4. **ListExpiredRecycleBinAsync:** Supports automated cleanup of expired items

### 8. DTO Structure

The `AgentDto` provides a complete view:

```csharp
public record AgentDto
{
    public Guid Id { get; init; }
    public string Name { get; init; }
    public string? Description { get; init; }
    public string? AvatarUrl { get; init; }
    public List<PluginDto> Plugins { get; init; }
    public string? Personality { get; init; }
    public Guid? PersonalityTemplateId { get; init; }
    public string? Language { get; init; }
    public Guid? UserId { get; init; }
    public bool IsPublic { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public AgentStatsDto? Stats { get; init; }
}
```

### 9. Input Validation

Commands are validated using dedicated validator classes:
- `CreateAgentCommandValidator`
- `UpdateAgentCommandValidator`
- `RateAgentCommandValidator`

Validators are called at the start of each handler before any business logic executes.

---

## Consequences

### Positive

- **Performance:** Database-level filtering and batched stats queries reduce latency
- **Consistency:** All operations follow the same CQRS pattern
- **Security:** Access verification checks ownership and plugin availability
- **Data Safety:** Soft delete with recycle bin prevents accidental data loss
- **Testability:** Clear separation allows comprehensive unit testing

### Negative

- **Complexity:** Multiple handlers and repositories add some overhead

---

## Test Coverage

Unit tests cover:

| Test Class | Coverage |
|------------|----------|
| `CreateAgentHandlerTests` | Agent creation with/without owner, plugins, public/private |
| `UpdateAgentHandlerTests` | Agent updates, field-level changes, authorization, stats retrieval |
| `ListAgentsHandlerTests` | Visibility filtering, stats retrieval, anonymous users |
| `SoftDeleteAgentHandlerTests` | Soft delete behavior, authorization |
| `RestoreAgentHandlerTests` | Restore from recycle bin, authorization, timestamp updates |
| `PermanentDeleteAgentHandlerTests` | Permanent deletion, recycle bin requirement, authorization |
| `ListRecycleBinAgentsHandlerTests` | Recycle bin listing, plugin details, user filtering |
| `VerifyAgentAccessHandlerTests` | Access verification, plugin requirements |
| `AddPluginToAgentHandlerTests` | Plugin addition, ownership verification, duplicate prevention |
| `RemovePluginFromAgentHandlerTests` | Plugin removal, validation, preservation of other plugins |
| `RateAgentHandlerTests` | Rating creation, update, validation, agent existence check |
| `GetAgentRatingsHandlerTests` | Rating listing, average calculation, agent existence check |
| `RecycleBinCleanupWorkerTests` | Expired agent deletion, error handling, logging verification |

Integration tests:
- `AgentIntegrationTests` - End-to-end API testing
- `AgentAccessVerificationTests` - Access control integration
- `AgentOwnershipOperationsTests` - Ownership operations integration

---

## Related ADRs

- (Future) ADR-XXXX: Automated Recycle Bin Cleanup Worker

---

## Implementation Status

All components specified in this ADR have been fully implemented:

| Component | Status | Notes |
|-----------|--------|-------|
| CQRS Handlers (12) | Complete | All commands/queries implemented including rating |
| Repository Interfaces | Complete | With optimized batch queries (MED-001, MED-002, MED-003) |
| Stats Retrieval | Complete | Real-time stats via `GetStatsForAgentsAsync` |
| Rating System | Complete | Real-time ratings via `GetAverageRatingsForAgentsAsync` |
| Visibility Controls | Complete | Public/private agent support |
| Soft Delete | Complete | 30-day recycle bin with expiration |
| Input Validation | Complete | Validators for all commands |
| Unit Tests | Complete | 89+ tests covering all handlers and worker |
| Integration Tests | Complete | End-to-end API testing |
| Recycle Bin Cleanup | Complete | Worker running every 6 hours |

**All items from this ADR are now implemented.**

### Changelog

| Date | Change |
|------|--------|
| 2025-12-22 | Fixed bug in `RecycleBinCleanupWorker`: now uses `DeleteAsync` to permanently delete expired agents instead of just updating flags |
| 2025-12-22 | Added 5 unit tests for `RecycleBinCleanupWorker` covering deletion, error handling, and logging |
| 2025-12-22 | Updated test count to 89+ tests |

---

## References

- `src/Core/Application/UseCases/Agent/` - All agent use cases
- `src/Core/Application/Interfaces/IAgentRepository.cs` - Agent repository contract
- `src/Core/Application/Interfaces/IAgentRatingRepository.cs` - Rating repository contract
- `src/Core/Application/Interfaces/IChatSessionRepository.cs` - Stats methods
- `src/Core/Domain/Entities/AgentRatingEntity.cs` - Rating entity
- `src/Adapters/Infrastructure/Persistence/Repositories/AgentRatingRepository.cs` - Rating repository implementation
- `src/Adapters/Workers/RecycleBinCleanup/RecycleBinCleanupWorker.cs` - Cleanup worker
- `tests/Unit/Core/Application.Tests/UseCases/Agent/` - Unit tests for handlers
- `tests/Unit/Adapters/Infrastructure.Tests/Workers/RecycleBinCleanupWorkerTests.cs` - Unit tests for cleanup worker
