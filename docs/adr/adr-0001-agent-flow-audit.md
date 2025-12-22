# ADR-0001: Agent Creation Flow Audit

**Date:** 2025-12-22
**Status:** In Progress (Phase 1 & 2 Complete)
**Author:** Audit Report
**Last Updated:** 2025-12-22

---

## Context

Full audit of the Agent creation and management flow across backend (orchid-core) and frontend (orchid-webapp) to ensure production readiness.

---

## Findings Summary

| Category | Critical | High | Medium | Low | Fixed |
|----------|----------|------|--------|-----|-------|
| Security | ~~3~~ | ~~1~~ | 0 | 0 | 4/4 |
| Functionality | ~~1~~ | ~~2~~ | 3 | 0 | 3/3 |
| **Total** | **0** | **0** | **3** | **0** | **7/7** |

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

## Medium Priority Issues

### MED-001: ListAgents Filters in Memory

**Location:** `src/Core/Application/UseCases/Agent/ListAgents/ListAgentsHandler.cs:19-22`

**Problem:**
Filters `IsDeleted` in memory after fetching all records.

```csharp
var agents = await _agentRepository.ListAsync(ct);
// ListAsync already filters !IsDeleted, but then:
var filteredAgents = agents.Where(a => a.IsPublic || a.UserId == request.UserId);
```

**Impact:**
Performance degradation with large datasets.

**Recommendation:**
Move all filtering to database query.

---

### MED-002: Stats Always Empty

**Location:** `src/Core/Application/UseCases/Agent/CreateAgent/CreateAgentHandler.cs:68`

**Problem:**
Agent stats are hardcoded to empty values:

```csharp
Stats = new AgentStatsDto
{
    TotalConversations = 0,
    AvgRating = null
}
```

**Impact:**
Stats feature is non-functional.

**Recommendation:**
Implement conversation tracking and rating aggregation.

---

### MED-003: No Input Validation in CreateAgentHandler

**Location:** `src/Core/Application/UseCases/Agent/CreateAgent/CreateAgentHandler.cs`

**Problem:**
No server-side validation of input data. Relies entirely on EF constraints.

**Recommendation:**
Add FluentValidation:
```csharp
public class CreateAgentCommandValidator : AbstractValidator<CreateAgentCommand>
{
    public CreateAgentCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(3).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.AvatarUrl).Must(BeValidUrl).When(x => !string.IsNullOrEmpty(x.AvatarUrl));
    }
}
```

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
| `src/Core/Application/UseCases/Agent/CreateAgent/*` | Create use case |
| `src/Core/Application/UseCases/Agent/ListAgents/*` | List use case |
| `src/Core/Application/UseCases/Agent/SoftDeleteAgent/*` | Soft delete |
| `src/Core/Application/UseCases/Agent/RestoreAgent/*` | Restore from bin |
| `src/Core/Application/UseCases/Agent/PermanentDeleteAgent/*` | Permanent delete |
| `src/Core/Application/Interfaces/IAgentRepository.cs` | Repository port |
| `src/Adapters/Infrastructure/Persistence/Repositories/AgentRepository.cs` | Repository adapter |
| `src/Adapters/WebApi/Controllers/AgentsController.cs` | REST endpoints |

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

### Phase 3: Improvements (Medium Priority)
8. [ ] MED-001: Optimize ListAgents query
9. [ ] MED-002: Implement stats tracking
10. [ ] MED-003: Add input validation

---

## Decision

Proceed with fixing issues in priority order starting with Phase 1 (security).

---

## Consequences

- Immediate security hardening required before production
- Update endpoint is critical for user experience
- Database cleanup strategy needed for zombie records

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
