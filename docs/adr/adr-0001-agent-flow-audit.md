# ADR-0001: Agent Creation Flow Audit

**Date:** 2025-12-22
**Status:** Complete (All Phases)
**Author:** Audit Report
**Last Updated:** 2025-12-22
**Completed:** 2025-12-22

---

## Context

Full audit of the Agent creation and management flow across backend (orchid-core) and frontend (orchid-webapp) to ensure production readiness.

---

## Findings Summary

| Category | Critical | High | Medium | Low | Fixed |
|----------|----------|------|--------|-----|-------|
| Security | ~~3~~ | ~~1~~ | 0 | 0 | 4/4 |
| Functionality | ~~1~~ | ~~2~~ | ~~3~~ | 0 | 6/6 |
| **Total** | **0** | **0** | **0** | **0** | **10/10** |

---

## Critical Issues

### SEC-001: Missing Authorization on Recycle Bin Endpoints

**Severity:** CRITICAL
**Location:** `src/Adapters/WebApi/Controllers/AgentsController.cs:101-120`

**Problem:**
Three endpoints lack the `[Authorize]` attribute, allowing unauthenticated access:

```csharp
// Line 101-106 - NO [Authorize]
[HttpGet("recycle-bin")]
public async Task<IActionResult> GetRecycleBinAgents()

// Line 108-113 - NO [Authorize]
[HttpPost("{id}/restore")]
public async Task<IActionResult> RestoreAgent(Guid id)

// Line 115-120 - NO [Authorize]
[HttpDelete("{id}/permanent")]
public async Task<IActionResult> PermanentDeleteAgent(Guid id)
```

**Impact:**
- Any anonymous user can view ALL agents in recycle bin
- Any anonymous user can restore ANY agent
- Any anonymous user can permanently delete ANY agent

**Fix:**
```csharp
[Authorize]
[HttpGet("recycle-bin")]
public async Task<IActionResult> GetRecycleBinAgents()
// ... same for other endpoints
```

---

### SEC-002: No Ownership Validation in RestoreAgent

**Severity:** CRITICAL
**Location:** `src/Core/Application/UseCases/Agent/RestoreAgent/RestoreAgentHandler.cs:15-40`

**Problem:**
Handler does not verify that the requesting user owns the agent being restored.

```csharp
public async Task<Unit> Handle(RestoreAgentCommand cmd, CancellationToken ct)
{
    var agent = await _agentRepository.GetByIdAsync(cmd.AgentId, ct);
    // NO CHECK: if (agent.UserId != requestingUserId) throw UnauthorizedAccessException

    agent.IsDeleted = false;
    // ...
}
```

**Impact:**
Any authenticated user can restore any other user's deleted agents.

**Fix:**
Add `UserId` to command and validate ownership:
```csharp
public record RestoreAgentCommand(Guid AgentId, Guid UserId) : IRequest<Unit>;

// In handler:
if (agent.UserId != cmd.UserId)
    throw new UnauthorizedAccessException("You don't own this agent");
```

---

### SEC-003: No Ownership Validation in PermanentDeleteAgent

**Severity:** CRITICAL
**Location:** `src/Core/Application/UseCases/Agent/PermanentDeleteAgent/PermanentDeleteAgentHandler.cs:15-37`

**Problem:**
Same as SEC-002 - no ownership check before permanent deletion.

**Impact:**
Any authenticated user can permanently delete any other user's agents.

**Fix:**
Same pattern as SEC-002.

---

### SEC-004: ListRecycleBin Returns All Users' Agents

**Severity:** HIGH
**Location:** `src/Core/Application/UseCases/Agent/ListRecycleBinAgents/ListRecycleBinAgentsHandler.cs:14-19`

**Problem:**
Query returns ALL agents in recycle bin, not filtered by current user.

```csharp
public async Task<List<AgentEntity>> Handle(ListRecycleBinAgentsQuery request, CancellationToken ct)
{
    return await _agentRepository.ListRecycleBinAsync(ct);
    // NO FILTER by UserId
}
```

**Impact:**
Users can see other users' deleted agents (information disclosure).

**Fix:**
Add UserId to query and filter:
```csharp
public record ListRecycleBinAgentsQuery(Guid UserId) : IRequest<List<AgentEntity>>;

// In repository:
.Where(a => a.IsInRecycleBin && a.UserId == userId)
```

---

## Functionality Issues

### FUNC-001: Missing Update Endpoint

**Severity:** CRITICAL
**Location:** `src/Adapters/WebApi/Controllers/AgentsController.cs`

**Problem:**
No `PUT /api/agents/{id}` endpoint exists. The frontend calls this endpoint but it doesn't exist in the backend.

**Frontend expects:**
```typescript
// src/services/agent.ts
async update(id: string, data: UpdateAgentDto) {
  return this.put(`/agents/${id}`, data);  // 404 - endpoint doesn't exist!
}
```

**Impact:**
Agent editing is completely broken - users cannot update their agents.

**Fix:**
Add UpdateAgent use case and endpoint:

```csharp
// New Command
public record UpdateAgentCommand(
    Guid AgentId,
    Guid UserId,
    string? Name,
    string? Description,
    string? AvatarUrl,
    string? Personality,
    Guid? PersonalityTemplateId,
    string? Language,
    Guid[]? PluginIds,
    bool? IsPublic
) : IRequest<AgentDto>;

// New Endpoint
[Authorize]
[HttpPut("{id}")]
public async Task<IActionResult> UpdateAgent(Guid id, [FromBody] UpdateAgentRequest request)
{
    var userId = GetUserId();
    if (userId == null) return Unauthorized();

    var command = new UpdateAgentCommand(id, userId.Value, ...);
    var result = await _mediator.Send(command);
    return Ok(result);
}
```

---

### FUNC-002: PermanentDelete Doesn't Actually Delete

**Severity:** HIGH
**Location:** `src/Core/Application/UseCases/Agent/PermanentDeleteAgent/PermanentDeleteAgentHandler.cs:30`

**Problem:**
"Permanent delete" only sets `IsInRecycleBin = false`, leaving the record as a zombie:

```csharp
agent.IsInRecycleBin = false;
// Result: IsDeleted=true, IsInRecycleBin=false
// Agent is neither visible nor truly deleted
```

**Impact:**
- Database accumulates zombie records
- No actual data cleanup happens
- GDPR compliance issues (data not truly deleted)

**Fix Options:**
1. **Hard delete:** Actually remove from database
2. **Archive table:** Move to `ArchivedAgents` table
3. **Anonymize:** Clear PII but keep record for analytics

---

### FUNC-003: RecycleBin Returns Entity Instead of DTO

**Severity:** HIGH
**Location:** `src/Adapters/WebApi/Controllers/AgentsController.cs:101-106`

**Problem:**
Returns raw `AgentEntity` instead of `AgentDto`, exposing internal structure.

```csharp
[HttpGet("recycle-bin")]
public async Task<IActionResult> GetRecycleBinAgents()
{
    var agents = await _mediator.Send(new ListRecycleBinAgentsQuery());
    return Ok(agents);  // Returns List<AgentEntity> - leaks internal model!
}
```

**Impact:**
- Exposes internal entity structure
- May leak sensitive fields
- Inconsistent API contract

**Fix:**
Map to DTO before returning.

---

## Medium Priority Issues (All Fixed)

### MED-001: ListAgents Filters in Memory - FIXED

**Location:** `src/Core/Application/UseCases/Agent/ListAgents/ListAgentsHandler.cs`

**Problem:**
Filters `IsDeleted` in memory after fetching all records.

**Solution:**
Added `ListByUserAsync` method to `IAgentRepository` that performs all filtering at the database level.

---

### MED-002: Stats Always Empty - FIXED

**Location:** `src/Core/Application/UseCases/Agent/ListAgents/ListAgentsHandler.cs`

**Problem:**
Agent stats were hardcoded to empty values.

**Solution:**
- Added stats methods to `IChatSessionRepository`: `CountByAgentIdAsync`, `GetLastUsedByAgentIdAsync`, `GetStatsForAgentsAsync`
- `ListAgentsHandler` now fetches real stats for all agents in one optimized query
- `UpdateAgentHandler` now returns real stats for the updated agent
- Note: `AvgRating` remains null as rating system is not yet implemented (future enhancement)

---

### MED-003: No Input Validation in CreateAgentHandler - FIXED

**Location:** `src/Core/Application/UseCases/Agent/CreateAgent/`

**Problem:**
No server-side validation of input data.

**Solution:**
Added `CreateAgentCommandValidator` and `UpdateAgentCommandValidator` following existing project patterns:
- Name: required, 3-100 characters
- Description: max 1000 characters
- AvatarUrl: valid URL format, max 500 characters
- Language: max 10 characters

---

## Architecture Overview

### Backend Flow (Working)
```
POST /api/agents
    → AgentsController.CreateAgent()
    → CreateAgentCommand via MediatR
    → CreateAgentHandler
    → IAgentRepository.CreateAsync()
    → AgentDto response
```

### Frontend Flow (Working)
```
/agents/new (NewAgentPage)
    → Form validation
    → useCreateAgent mutation
    → AgentService.create()
    → POST /api/agents
    → Invalidate queries
    → Navigate to /agents
```

### Missing Flow (BROKEN)
```
/agents/:id/edit (EditAgentPage)
    → useUpdateAgent mutation
    → AgentService.update()
    → PUT /api/agents/:id  ← 404 NOT FOUND
```

---

## Files Reference

### Backend
| File | Purpose |
|------|---------|
| `src/Core/Domain/Entities/AgentEntity.cs` | Domain entity |
| `src/Core/Application/UseCases/Agent/CreateAgent/*` | Create use case + validator |
| `src/Core/Application/UseCases/Agent/UpdateAgent/*` | Update use case + validator |
| `src/Core/Application/UseCases/Agent/ListAgents/*` | List use case (optimized) |
| `src/Core/Application/UseCases/Agent/SoftDeleteAgent/*` | Soft delete |
| `src/Core/Application/UseCases/Agent/RestoreAgent/*` | Restore from bin |
| `src/Core/Application/UseCases/Agent/PermanentDeleteAgent/*` | Permanent delete (hard delete) |
| `src/Core/Application/Interfaces/IAgentRepository.cs` | Repository port (with `ListByUserAsync`) |
| `src/Core/Application/Interfaces/IChatSessionRepository.cs` | Chat session port (with stats methods) |
| `src/Adapters/Infrastructure/Persistence/Repositories/AgentRepository.cs` | Repository adapter |
| `src/Adapters/Infrastructure/Persistence/Repositories/ChatSessionRepository.cs` | Chat session adapter (with stats) |
| `src/Adapters/WebApi/Controllers/AgentsController.cs` | REST endpoints (all secured) |

### Frontend
| File | Purpose |
|------|---------|
| `src/pages/agents/new.tsx` | Create page |
| `src/pages/agents/edit.tsx` | Edit page (BROKEN) |
| `src/pages/agents/page.tsx` | List page |
| `src/pages/agents/detail.tsx` | Detail page |
| `src/pages/agents/recycle-bin.tsx` | Recycle bin page |
| `src/services/agent.ts` | API service |
| `src/hooks/useAgents.ts` | TanStack Query hooks |

---

## Recommended Fix Priority

### Phase 1: Critical Security (Immediate) - COMPLETED
1. [x] SEC-001: Add `[Authorize]` to recycle bin endpoints
2. [x] SEC-002: Add UserId validation to RestoreAgentHandler
3. [x] SEC-003: Add UserId validation to PermanentDeleteAgentHandler
4. [x] SEC-004: Filter recycle bin by UserId

### Phase 2: Core Functionality (High Priority) - COMPLETED
5. [x] FUNC-001: Implement UpdateAgent use case and endpoint
6. [x] FUNC-002: Implement proper permanent deletion (now uses hard delete)
7. [x] FUNC-003: Return DTO instead of Entity

### Phase 3: Improvements (Medium Priority) - COMPLETED
8. [x] MED-001: Optimize ListAgents query (DB-level filtering via `ListByUserAsync`)
9. [x] MED-002: Implement stats tracking (conversation count and last used)
10. [x] MED-003: Add input validation (validators for Create and Update commands)

---

## Decision

All identified issues have been resolved across three phases:
1. Phase 1: Critical security fixes (authentication, authorization, data isolation)
2. Phase 2: Core functionality (UpdateAgent endpoint, proper deletion, DTO consistency)
3. Phase 3: Performance and quality improvements (DB-level filtering, stats tracking, validation)

---

## Consequences

**Resolved:**
- Security hardening complete - all endpoints require authentication and validate ownership
- Update endpoint now functional - users can edit their agents
- Database cleanup via hard delete - no zombie records, GDPR compliant
- Performance optimized - filtering at DB level, batch stats queries

**Future Enhancements (not critical):**
- Rating system (AvgRating) - requires new Rating entity and aggregation logic
- Additional validation rules as business requirements evolve

---

## Changelog

### 2025-12-22 - Phase 1 Security Fixes

**Files Modified:**

| File | Change |
|------|--------|
| `AgentsController.cs` | Added `[Authorize]` + UserId extraction to recycle-bin, restore, permanent-delete endpoints |
| `RestoreAgentCommand.cs` | Added `UserId` parameter |
| `RestoreAgentHandler.cs` | Added ownership validation |
| `PermanentDeleteAgentCommand.cs` | Added `UserId` parameter |
| `PermanentDeleteAgentHandler.cs` | Added ownership validation + changed to hard delete |
| `ListRecycleBinAgentsQuery.cs` | Added `UserId` parameter |
| `ListRecycleBinAgentsHandler.cs` | Now uses `ListRecycleBinByUserAsync` |
| `IAgentRepository.cs` | Added `ListRecycleBinByUserAsync` method |
| `AgentRepository.cs` | Implemented `ListRecycleBinByUserAsync` |

**Security Improvements:**
- All recycle bin endpoints now require authentication
- Users can only see/restore/delete their own agents in recycle bin
- Permanent delete now actually removes records from database (GDPR compliant)

### 2025-12-22 - Phase 2 Functionality Fixes

**Files Created:**

| File | Purpose |
|------|---------|
| `UpdateAgent/UpdateAgentCommand.cs` | Command with AgentId, UserId, and optional fields for partial updates |
| `UpdateAgent/UpdateAgentHandler.cs` | Handler with ownership validation and partial update logic |

**Files Modified:**

| File | Change |
|------|--------|
| `AgentsController.cs` | Added `PUT /api/agents/{id}` endpoint with `UpdateAgentRequest` DTO |
| `ListRecycleBinAgentsQuery.cs` | Changed return type from `List<AgentEntity>` to `List<AgentDto>` |
| `ListRecycleBinAgentsHandler.cs` | Added entity-to-DTO mapping with plugin details |

**Functionality Improvements:**
- Agent editing now fully functional via `PUT /api/agents/{id}`
- UpdateAgent validates ownership before allowing modifications
- UpdateAgent supports partial updates (only provided fields are updated)
- RecycleBin endpoint now returns proper DTOs with plugin information
- API contract is now consistent across all agent endpoints

### 2025-12-22 - Phase 3 Improvements

**Files Created:**

| File | Purpose |
|------|---------|
| `CreateAgent/CreateAgentCommandValidator.cs` | Input validation for agent creation |
| `UpdateAgent/UpdateAgentCommandValidator.cs` | Input validation for agent updates |

**Files Modified:**

| File | Change |
|------|--------|
| `IAgentRepository.cs` | Added `ListByUserAsync` method for optimized filtering |
| `AgentRepository.cs` | Implemented `ListByUserAsync` with DB-level filtering |
| `ListAgentsHandler.cs` | Now uses `ListByUserAsync` + fetches real stats via `GetStatsForAgentsAsync` |
| `CreateAgentHandler.cs` | Added validator call |
| `UpdateAgentHandler.cs` | Added validator call + real stats fetching |
| `IChatSessionRepository.cs` | Added `CountByAgentIdAsync`, `GetLastUsedByAgentIdAsync`, `GetStatsForAgentsAsync` |
| `ChatSessionRepository.cs` | Implemented stats methods with optimized GROUP BY query |

**Performance Improvements:**
- Agent listing now filters at database level instead of in-memory
- Stats for multiple agents fetched in single query using GROUP BY
- Reduced N+1 query patterns

**Data Quality Improvements:**
- Server-side validation prevents invalid data from entering the system
- Stats now show real conversation counts and last used timestamps
- Note: Rating system (AvgRating) deferred to future enhancement

---

## Final Verification

All implementations verified on 2025-12-22:

| Component | File | Status |
|-----------|------|--------|
| CreateAgentCommandValidator | `UseCases/Agent/CreateAgent/CreateAgentCommandValidator.cs` | ✅ |
| UpdateAgentCommandValidator | `UseCases/Agent/UpdateAgent/UpdateAgentCommandValidator.cs` | ✅ |
| CreateAgentHandler validation | `UseCases/Agent/CreateAgent/CreateAgentHandler.cs:17` | ✅ |
| UpdateAgentHandler validation + stats | `UseCases/Agent/UpdateAgent/UpdateAgentHandler.cs:18,71-72` | ✅ |
| ListAgentsHandler optimization | `UseCases/Agent/ListAgents/ListAgentsHandler.cs:19,26` | ✅ |
| IAgentRepository.ListByUserAsync | `Interfaces/IAgentRepository.cs:15` | ✅ |
| IChatSessionRepository stats methods | `Interfaces/IChatSessionRepository.cs:28-41` | ✅ |
| AgentRepository.ListByUserAsync | `Repositories/AgentRepository.cs:23-29` | ✅ |
| ChatSessionRepository stats | `Repositories/ChatSessionRepository.cs:92-129` | ✅ |

### Test Files Updated

| File | Change |
|------|--------|
| `Application.Tests/UseCases/Agent/ListAgentsHandlerTests.cs` | Added `IChatSessionRepository` mock, updated to use `ListByUserAsync`, added stats test |
| `CreditSystem.IntegrationTests/Mocks/ChatHubMocks.cs` | Implemented `CountByAgentIdAsync`, `GetLastUsedByAgentIdAsync`, `GetStatsForAgentsAsync` |

**Audit Complete - All issues resolved and verified. Build passes.**
