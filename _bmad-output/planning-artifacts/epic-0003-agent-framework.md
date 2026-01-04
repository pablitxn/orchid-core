---
epicId: '0003'
title: 'Agent Framework Migration'
phase: 3
priority: P2
masterPrd: prd.md
status: draft
estimatedEffort: '3 weeks'
dependencies:
  - epic-0001
  - epic-0002
---

# Epic PRD: 0003 - Agent Framework Migration

**Parent:** [Master PRD](./prd.md)
**Phase:** 3 - Extend
**Priority:** P2 (Medium)
**Author:** Pablo
**Date:** 2026-01-04

---

## Overview

Refactor the current ad-hoc AI agent implementation into a standardized, extensible agent framework. This will provide a consistent foundation for all AI-powered features, better observability, and enable the upcoming marketplace (Epic 0004).

### Current State

Based on codebase analysis:
- Semantic Kernel used for LLM orchestration
- Agent logic scattered across multiple services
- Inconsistent plugin loading patterns
- Mixed approaches to conversation management
- Limited agent lifecycle visibility
- Langfuse integration partial

### Target State

- Unified AgentRunner abstraction
- Standardized plugin system (compatible with marketplace)
- Consistent conversation/session management
- Full observability with Langfuse
- Agent lifecycle events for frontend updates
- Credit consumption tracking per agent operation

---

## Goals

| Goal | Success Metric |
|------|----------------|
| Unified agent abstraction | Single AgentRunner handles all agents |
| Plugin standardization | All plugins follow common interface |
| Full observability | 100% agent operations traced |
| Credit integration | Every AI call consumes credits |
| Extensibility | New agent types addable in < 1 day |

---

## Scope

### In Scope

1. **Agent Abstraction Layer**
   - AgentRunner interface definition
   - Base agent implementation
   - Agent configuration model
   - Agent factory pattern

2. **Plugin System Standardization**
   - Plugin interface definition
   - Plugin discovery mechanism
   - Plugin lifecycle (load, unload, update)
   - Plugin capability declarations
   - Sandboxing considerations

3. **Conversation Management**
   - Unified conversation context
   - Message history management
   - Context window handling
   - Multi-turn conversation support

4. **Observability Integration**
   - Langfuse span for every agent call
   - Token usage tracking
   - Latency metrics
   - Error classification
   - Plugin invocation logging

5. **Credit System Integration**
   - Per-operation credit costs
   - Pre-flight credit checks
   - Credit reservation pattern
   - Usage reporting

6. **Existing Agent Migration**
   - Chat agent refactor
   - Knowledge base agent refactor
   - Spreadsheet agent refactor
   - Deprecate old implementations

### Out of Scope

- New agent types (use framework for future agents)
- Frontend agent UI changes
- Plugin marketplace (Epic 0004)
- Multi-model support (focus on OpenAI for now)

---

## Requirements

### REQ-0003-01: Agent Abstraction Layer

**Description:** Define and implement the core agent abstraction.

**Acceptance Criteria:**
- [ ] `IAgentRunner` interface defined with clear contract
- [ ] `BaseAgent` provides common functionality
- [ ] Agents configurable via `AgentConfiguration`
- [ ] `AgentFactory` creates agents from configuration
- [ ] Agents are stateless (state in conversation context)

**Technical Notes:**
- Use Semantic Kernel's function calling capabilities
- Support streaming responses
- Handle cancellation tokens properly

**Interface Definition:**
```csharp
public interface IAgentRunner
{
    Task<AgentResponse> ExecuteAsync(
        AgentRequest request,
        ConversationContext context,
        CancellationToken ct);

    IAsyncEnumerable<AgentStreamChunk> ExecuteStreamingAsync(
        AgentRequest request,
        ConversationContext context,
        CancellationToken ct);

    AgentCapabilities GetCapabilities();
}

public record AgentConfiguration(
    string AgentId,
    string Name,
    string SystemPrompt,
    IReadOnlyList<string> EnabledPlugins,
    AgentModelSettings ModelSettings,
    Dictionary<string, object> CustomSettings);

public record AgentCapabilities(
    bool SupportsStreaming,
    bool SupportsImages,
    bool SupportsFiles,
    IReadOnlyList<string> AvailablePlugins);
```

---

### REQ-0003-02: Plugin System

**Description:** Standardize plugin definition, discovery, and loading.

**Acceptance Criteria:**
- [ ] `IAgentPlugin` interface defined
- [ ] Plugins declare their capabilities via attributes
- [ ] Plugin discovery from configured directories
- [ ] Hot-reload support for development
- [ ] Plugin dependency management
- [ ] Plugin validation on load

**Technical Notes:**
- Build on Semantic Kernel's native function system
- Add metadata layer for marketplace compatibility
- Consider AssemblyLoadContext for isolation

**Plugin Interface:**
```csharp
[AgentPlugin(
    Id = "knowledge-search",
    Name = "Knowledge Base Search",
    Version = "1.0.0",
    Description = "Search user's knowledge base",
    RequiredCapabilities = ["vector-search"])]
public class KnowledgeSearchPlugin : IAgentPlugin
{
    [KernelFunction("search_knowledge")]
    [Description("Search the knowledge base for relevant documents")]
    public async Task<SearchResult> SearchAsync(
        [Description("Search query")] string query,
        [Description("Max results")] int limit = 5)
    {
        // Implementation
    }
}
```

**Plugin Manifest (for marketplace):**
```json
{
  "id": "knowledge-search",
  "version": "1.0.0",
  "name": "Knowledge Base Search",
  "description": "Search user's knowledge base",
  "author": "orchid-core",
  "entry": "Orchid.Plugins.KnowledgeSearch.dll",
  "dependencies": [],
  "requiredCapabilities": ["vector-search"],
  "creditCost": {
    "perCall": 1,
    "perToken": 0
  }
}
```

---

### REQ-0003-03: Conversation Management

**Description:** Unified conversation context for all agent interactions.

**Acceptance Criteria:**
- [ ] `ConversationContext` holds full conversation state
- [ ] Message history with proper truncation
- [ ] Token counting for context window management
- [ ] Conversation persistence between requests
- [ ] Support for conversation branching

**Technical Notes:**
- Store conversation in Redis for fast access
- Persist to PostgreSQL for durability
- Handle context window limits gracefully

**Data Model:**
```csharp
public class ConversationContext
{
    public Guid ConversationId { get; }
    public Guid UserId { get; }
    public string AgentId { get; }
    public IReadOnlyList<ChatMessage> Messages { get; }
    public Dictionary<string, object> Metadata { get; }
    public int TokenCount { get; }
    public DateTime CreatedAt { get; }
    public DateTime UpdatedAt { get; }

    public ConversationContext WithMessage(ChatMessage message);
    public ConversationContext TruncatedToFit(int maxTokens);
}
```

---

### REQ-0003-04: Observability Integration

**Description:** Full tracing and metrics for agent operations.

**Acceptance Criteria:**
- [ ] Every agent execution creates Langfuse trace
- [ ] Token usage (prompt + completion) recorded
- [ ] Plugin invocations logged as spans
- [ ] Errors captured with classification
- [ ] Latency percentiles available
- [ ] Cost estimation per operation

**Technical Notes:**
- Use existing Langfuse integration, extend coverage
- Add custom attributes for filtering
- Integrate with Prometheus for metrics

**Trace Structure:**
```
Agent Execution (trace)
├── Pre-flight checks (span)
│   ├── Credit check
│   └── Rate limit check
├── Context preparation (span)
│   ├── Load conversation
│   └── Token counting
├── LLM Call (span)
│   ├── Model: gpt-4
│   ├── Prompt tokens: 1500
│   └── Completion tokens: 300
├── Plugin: knowledge-search (span)
│   ├── Query: "user query"
│   └── Results: 5
└── Response formatting (span)
```

---

### REQ-0003-05: Credit Integration

**Description:** Integrate agent operations with credit system from Epic 0001.

**Acceptance Criteria:**
- [ ] Credit cost calculated before execution
- [ ] Credits reserved before LLM call
- [ ] Credits consumed on success, released on failure
- [ ] Insufficient credits prevents execution (402)
- [ ] Usage reported per agent, per plugin

**Technical Notes:**
- Use pessimistic credit reservation
- Different costs per model, per plugin
- Consider token-based pricing model

**Credit Flow:**
```
1. Calculate estimated cost (based on model + plugins)
2. Reserve credits (atomic operation)
3. Execute agent
4. If success: consume reserved credits
5. If failure: release reserved credits
6. Record actual usage for billing
```

---

### REQ-0003-06: Migrate Existing Agents

**Description:** Refactor existing agent implementations to use new framework.

**Acceptance Criteria:**
- [ ] Chat agent migrated to AgentRunner
- [ ] Knowledge base agent migrated
- [ ] Spreadsheet agent migrated
- [ ] Old implementations deprecated
- [ ] No regression in functionality
- [ ] Performance maintained or improved

**Migration Checklist:**
| Agent | Current Location | Status |
|-------|------------------|--------|
| ChatAgent | Ai/Agents/ChatAgent.cs | Pending |
| KnowledgeAgent | Ai/Agents/KnowledgeAgent.cs | Pending |
| SpreadsheetAgent | Ai/Agents/SpreadsheetAgent.cs | Pending |

---

## Technical Design Considerations

### Architecture

```
Application Layer
└── UseCases/
    └── Agents/
        ├── ExecuteAgent/
        │   ├── ExecuteAgentCommand.cs
        │   └── ExecuteAgentHandler.cs
        └── Queries/
            └── GetAgentCapabilities/

Domain Layer
└── Agents/
    ├── AgentConfiguration.cs
    ├── ConversationContext.cs
    └── AgentEvents/
        ├── AgentExecutionStarted.cs
        ├── AgentExecutionCompleted.cs
        └── AgentExecutionFailed.cs

Infrastructure Layer
└── Ai/
    ├── AgentFramework/
    │   ├── AgentRunner.cs
    │   ├── AgentFactory.cs
    │   ├── BaseAgent.cs
    │   └── ConversationContextStore.cs
    ├── Plugins/
    │   ├── IAgentPlugin.cs
    │   ├── PluginLoader.cs
    │   └── BuiltIn/
    │       ├── KnowledgeSearchPlugin.cs
    │       ├── SpreadsheetPlugin.cs
    │       └── WebSearchPlugin.cs
    └── Observability/
        └── AgentTelemetry.cs
```

### Configuration

```json
{
  "AgentFramework": {
    "DefaultModel": "gpt-4",
    "MaxConversationTokens": 8000,
    "PluginDirectories": ["./plugins", "./custom-plugins"],
    "DefaultPlugins": ["knowledge-search", "web-search"],
    "CreditCosts": {
      "gpt-4": { "perPromptToken": 0.00003, "perCompletionToken": 0.00006 },
      "gpt-3.5-turbo": { "perPromptToken": 0.000001, "perCompletionToken": 0.000002 }
    }
  }
}
```

### Error Handling

| Error | Response | Recovery |
|-------|----------|----------|
| Insufficient credits | 402 Payment Required | User purchases credits |
| Rate limited | 429 Too Many Requests | Exponential backoff |
| Context too long | 400 Bad Request | Truncate conversation |
| Plugin failure | Graceful degradation | Continue without plugin |
| LLM timeout | 504 Gateway Timeout | Retry with backoff |

---

## Dependencies

### External
- OpenAI API
- Langfuse for observability
- Semantic Kernel library

### Internal
- Epic 0001 (Subscriptions) - credit system
- Epic 0002 (Auth0) - user identity for context

### Blocked By This
- Epic 0004 (Marketplace) - needs plugin system

---

## Migration Strategy

### Phase 1: Framework Implementation (Week 1)
- Implement core abstractions
- Build plugin system
- Set up observability

### Phase 2: Agent Migration (Week 2)
- Migrate chat agent
- Migrate knowledge agent
- Migrate spreadsheet agent

### Phase 3: Integration & Testing (Week 3)
- Credit system integration
- Performance testing
- Documentation

### Rollback Plan
- Feature flag per agent type
- Old implementations kept for 2 weeks
- Gradual traffic shift

---

## Success Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| Agent response time | p95 < 3s | Langfuse traces |
| Plugin load time | < 100ms | Startup logs |
| Trace coverage | 100% operations | Langfuse dashboard |
| Credit accuracy | 100% | Billing reconciliation |

---

## Open Questions

1. Should plugins run in isolated contexts for security?
2. What's the plugin versioning strategy?
3. How to handle plugin deprecation?
4. Should conversation history be encrypted at rest?
5. What's the maximum conversation length before forced truncation?

---

*This is an Epic PRD. See [Master PRD](./prd.md) for overall project context.*
