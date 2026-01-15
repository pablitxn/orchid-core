---
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11]
inputDocuments:
  - docs/project-overview.md
  - docs/architecture.md
  - docs/api-contracts.md
  - docs/data-models.md
  - docs/source-tree-analysis.md
  - docs/development-guide.md
  - docs/index.md
  - docs/adr/adr-0001-agent-flow-audit.md
  - backlog (user-provided)
documentCounts:
  briefs: 0
  research: 0
  brainstorming: 0
  projectDocs: 8
  backlog: 1
workflowType: 'prd'
scope: 'backend-only'
documentType: 'master-prd'
epicPrds:
  - epic-0000-stabilize-mvp.md
  - epic-0001-subscriptions.md
  - epic-0002-auth0-integration.md
  - epic-0003-agent-framework.md
  - epic-0004-marketplace.md
lastStep: 11
---

# Master PRD - orchid-core

**Author:** Pablo
**Date:** 2026-01-04
**Scope:** Backend Only
**Document Type:** Master PRD (links to individual Epic PRDs)

---

## Executive Summary

**orchid-core** is the backend API for an ecosystem of AI-powered applications, built with .NET 9 following Hexagonal Architecture. This PRD defines a 3-month roadmap focused on stabilization, feature completion, and selective new capabilities.

### Vision

Transform orchid-core from an experimental platform into a production-ready, secure, and maintainable backend that enables sustainable development velocity.

### Strategic Phases

| Phase | Focus | Goal |
|-------|-------|------|
| **1. Stabilize** | Security, cleanup, MVP hardening | Rock-solid foundation |
| **2. Complete** | Finish half-done features (subscriptions, auth) | No more WIP |
| **3. Extend** | Add select new capabilities | Controlled growth |

### What Makes This Special

This is an **infrastructure investment**, not a feature sprint. The outcome is a backend that:
- Passes security review
- Has no dead code or abandoned features
- Enables faster future development
- Provides clear patterns for new contributors

---

## Project Classification

**Technical Type:** api_backend / saas_b2b hybrid
**Domain:** General (with AI/ML elements)
**Complexity:** Medium-High
**Project Context:** Brownfield - extending existing Hexagonal architecture
**Timeline:** 3 months
**Milestone Type:** Internal - stabilization for continued development

---

## Success Criteria

### Technical Success
| Metric | Target | Measurement |
|--------|--------|-------------|
| Test Coverage | ≥85% | CI coverage reports |
| Security Vulnerabilities | 0 Critical/High | OWASP ZAP + dependency audit |
| Dead Code | 0% | Static analysis, no unused exports |
| API Response Time | p95 < 500ms | Prometheus metrics |
| Build Time | < 5 min | CI pipeline |
| Migration Success | 100% | All EF migrations apply cleanly |

### User/Developer Success
| Metric | Target | Measurement |
|--------|--------|-------------|
| Onboarding Time | < 1 day | New contributor productive in 8 hours |
| Documentation Coverage | 100% API | All endpoints documented |
| Pattern Consistency | 100% | All features follow Hexagonal patterns |
| Breaking Changes | 0 unannounced | API versioning in place |

### Business Success
| Metric | Target | Measurement |
|--------|--------|-------------|
| Feature Completion | 100% WIP | No half-done features remain |
| Subscription System | Operational | Stripe integration complete |
| Auth System | Production-ready | Auth0 fully integrated |
| Technical Debt Ratio | < 10% | SonarQube analysis |

---

## Epic Overview

This roadmap consists of **5 backend-focused epics** organized across 3 phases:

### Phase 1: Stabilize (Month 1)

| Epic | PRD | Priority | Description |
|------|-----|----------|-------------|
| **0000** | [epic-0000-stabilize-mvp.md](./epic-0000-stabilize-mvp.md) | P0 | Security hardening, dead code removal, test coverage |

### Phase 2: Complete (Month 2)

| Epic | PRD | Priority | Description |
|------|-----|----------|-------------|
| **0001** | [epic-0001-subscriptions.md](./epic-0001-subscriptions.md) | P1 | Complete Stripe integration, credit system |
| **0002** | [epic-0002-auth0-integration.md](./epic-0002-auth0-integration.md) | P1 | Migrate from custom JWT to Auth0 |

### Phase 3: Extend (Month 3)

| Epic | PRD | Priority | Description |
|------|-----|----------|-------------|
| **0003** | [epic-0003-agent-framework.md](./epic-0003-agent-framework.md) | P2 | Migrate to standardized agent framework |
| **0004** | [epic-0004-marketplace.md](./epic-0004-marketplace.md) | P2 | Agent plugin marketplace backend |

---

## Technical Constraints

### Must Preserve
- **Hexagonal Architecture** - All changes follow ports & adapters pattern
- **.NET 9** - No framework downgrade
- **PostgreSQL 17 + pgvector** - Vector capabilities required for AI features
- **Existing API Contracts** - Breaking changes require versioning

### Technology Stack (Locked)
| Component | Technology | Version |
|-----------|------------|---------|
| Runtime | .NET | 9.0 |
| ORM | Entity Framework Core | 9.0.5 |
| Cache | Redis | 7+ |
| Message Queue | RabbitMQ + MassTransit | 3+ |
| AI Orchestration | Semantic Kernel | 1.54.0 |
| Telemetry | Langfuse | Latest |

### Performance Requirements
| Aspect | Requirement |
|--------|-------------|
| HTTP latency | p95 < 500ms (non-AI endpoints) |
| AI streaming | First token < 2s |
| File uploads | ≤ 100 MB |
| Concurrent users | Support 1000 simultaneous |

---

## Dependencies

### External Services
| Service | Dependency Type | Risk |
|---------|-----------------|------|
| OpenAI API | Runtime | Medium - rate limits, cost |
| Auth0 | Runtime (Phase 2) | Low - managed service |
| Stripe | Runtime (Phase 2) | Low - established provider |
| PostgreSQL | Infrastructure | Low - self-hosted |
| Redis | Infrastructure | Low - self-hosted |

### Internal Dependencies
| Epic | Depends On | Notes |
|------|------------|-------|
| 0001 (Subscriptions) | 0000 (Stabilize) | Need clean codebase first |
| 0002 (Auth0) | 0000 (Stabilize) | Security foundation required |
| 0003 (Agent Framework) | 0001, 0002 | User identity and credits needed |
| 0004 (Marketplace) | 0003 | Agent system must be stable |

---

## Risk Assessment

### High Risk
| Risk | Impact | Mitigation |
|------|--------|------------|
| Auth0 migration breaks existing sessions | User disruption | Parallel auth systems during transition |
| Stripe webhook reliability | Lost payments | Idempotent handlers, reconciliation jobs |
| Dead code removal breaks hidden dependencies | Runtime errors | Extensive test coverage first |

### Medium Risk
| Risk | Impact | Mitigation |
|------|--------|------------|
| Agent framework migration complexity | Delays | Incremental migration, feature flags |
| Test coverage gaps | Regressions | Coverage gates in CI |
| OpenAI API changes | Feature breakage | Abstract behind ports |

### Low Risk
| Risk | Impact | Mitigation |
|------|--------|------------|
| Performance regression | User experience | Prometheus alerts, load testing |
| Documentation drift | Developer confusion | Automated doc generation |

---

## Timeline Overview

```
Month 1                    Month 2                    Month 3
├─────────────────────────┼─────────────────────────┼─────────────────────────┤
│                         │                         │                         │
│  EPIC 0000              │  EPIC 0001              │  EPIC 0003              │
│  Stabilize MVP          │  Subscriptions          │  Agent Framework        │
│  ████████████████████   │  ████████████████████   │  ████████████████████   │
│                         │                         │                         │
│                         │  EPIC 0002              │  EPIC 0004              │
│                         │  Auth0 Integration      │  Marketplace            │
│                         │  ████████████████████   │  ████████████████████   │
│                         │                         │                         │
└─────────────────────────┴─────────────────────────┴─────────────────────────┘
```

---

## Out of Scope (This Roadmap)

### Explicitly Excluded
- **Frontend changes** - Separate frontend PRD needed
- **Mobile applications** - Not in scope
- **Kubernetes infrastructure changes** - Infrastructure team owns
- **New AI model integrations** - Focus on stabilizing current OpenAI integration
- **Video call implementation** - Deferred to future roadmap
- **Canvas feature** - Deferred to future roadmap
- **Teams collaboration** - Deferred to future roadmap

### Deferred to Future Roadmap
| Feature | Reason |
|---------|--------|
| Chat interface improvements | Requires frontend coordination |
| Home improvements | UX-driven, needs design first |
| UX/UI overhaul | Separate design sprint needed |
| Videocalls | Complex, needs dedicated epic |
| Canvas | Experimental, needs validation |
| Teams | Multi-tenant complexity |

---

## Appendix: Epic PRD Index

| Document | Status | Description |
|----------|--------|-------------|
| [epic-0000-stabilize-mvp.md](./epic-0000-stabilize-mvp.md) | Draft | Security, cleanup, testing |
| [epic-0001-subscriptions.md](./epic-0001-subscriptions.md) | Draft | Stripe, credits, billing |
| [epic-0002-auth0-integration.md](./epic-0002-auth0-integration.md) | Draft | Auth migration |
| [epic-0003-agent-framework.md](./epic-0003-agent-framework.md) | Draft | Agent system refactor |
| [epic-0004-marketplace.md](./epic-0004-marketplace.md) | Draft | Plugin marketplace |

---

## Approval

| Role | Name | Date | Status |
|------|------|------|--------|
| Product Owner | Pablo | 2026-01-04 | Pending |
| Tech Lead | - | - | Pending |
| Architect | - | - | Pending |

---

*This is a Master PRD. Detailed requirements for each epic are in their respective PRD documents.*

