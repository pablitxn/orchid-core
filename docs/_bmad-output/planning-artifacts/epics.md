---
stepsCompleted: [1, 2]
inputDocuments:
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/epic-0000-stabilize-mvp.md
  - _bmad-output/planning-artifacts/epic-0001-subscriptions.md
  - _bmad-output/planning-artifacts/epic-0002-auth0-integration.md
  - _bmad-output/planning-artifacts/epic-0003-agent-framework.md
  - _bmad-output/planning-artifacts/epic-0004-marketplace.md
---

# orchid-core - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for orchid-core, decomposing the requirements from the PRD, and Architecture requirements into implementable stories.

## Requirements Inventory

### Functional Requirements

**Epic 0000 - Stabilize MVP (Phase 1 - P0):**

FR1: Conduct comprehensive security audit and fix all identified vulnerabilities including input validation, SQL injection prevention, XSS protection, CSRF tokens, secure headers, dependency updates, and secrets management
FR2: Configure static analysis and remove all unused public members, orphaned use cases, unused API endpoints, deprecated adapters, and commented-out code
FR3: Achieve 95% domain layer, 90% application layer, and 80% adapter layer test coverage with all critical paths having happy + error path tests
FR4: Ensure all use cases follow Command/Query pattern, external dependencies accessed via ports, no direct infrastructure references from domain/application, and consistent error handling
FR5: Update API documentation, create/update ADRs, developer onboarding guide, and architecture diagrams

**Epic 0001 - Subscriptions (Phase 2 - P1):**

FR6: Implement complete Stripe subscription lifecycle including subscribe, plan changes, cancellation, reactivation, and failed payment handling
FR7: Process all Stripe webhook events (checkout.session.completed, customer.subscription.*, invoice.*, payment_intent.*) with signature verification, idempotency, retry logic, and dead letter queue
FR8: Implement credit system with purchase flows, balance tracking, consumption on AI operations, low credit warnings, and optional expiration
FR9: Create billing management API for users to view subscription, billing history, payment methods, and credit balance
FR10: Build admin billing tools for viewing subscriptions, manual credit adjustments, webhook logs, and retry capabilities

**Epic 0002 - Auth0 Integration (Phase 2 - P1):**

FR11: Configure Auth0 tenant with API, SPA application, machine-to-machine app, roles, branding, and hardened security settings
FR12: Replace custom JWT validation with Auth0 JWKS validation including audience/issuer validation, claims mapping, and JWKS caching
FR13: Implement user synchronization between Auth0 and local DB including first login creation, profile sync, role sync, and soft delete handling
FR14: Migrate existing users to Auth0 without requiring password reset using custom database connection for lazy migration
FR15: Enable MFA (TOTP) and social login (Google, GitHub) with account linking for same email
FR16: Remove legacy auth code including custom JWT generation, password storage, refresh token management, and update all tests

**Epic 0003 - Agent Framework Migration (Phase 3 - P2):**

FR17: Define and implement IAgentRunner interface with AgentConfiguration, AgentFactory, and stateless agent pattern supporting streaming responses
FR18: Standardize plugin system with IAgentPlugin interface, plugin discovery, hot-reload, dependency management, and validation
FR19: Implement unified ConversationContext for message history, token counting, context window management, and conversation persistence
FR20: Integrate full observability with Langfuse traces, token usage, plugin invocation spans, error classification, and latency metrics
FR21: Integrate agent operations with credit system including cost calculation, credit reservation, consumption on success, and usage reporting
FR22: Migrate existing Chat, Knowledge Base, and Spreadsheet agents to new framework with no regression

**Epic 0004 - Marketplace (Phase 3 - P2):**

FR23: Build plugin catalog API with search, filtering by category/rating/compatibility, sorting, featured plugins, and detailed plugin info
FR24: Implement plugin installation system with user preferences, workspace settings, per-agent enable/disable, and configuration storage
FR25: Create rating and review system with star ratings, text reviews, one review per user per plugin, moderation, and aggregate calculations
FR26: Build publisher system with registration, plugin submission, review/approval workflow, dashboard stats, and verification
FR27: Implement plugin versioning with semantic versions, update notifications, changelogs, breaking change flags, and rollback capability
FR28: Track plugin usage analytics including install counts, active usage (DAU/MAU), invocations, error rates, and publisher-visible stats

### NonFunctional Requirements

NFR1: Test Coverage - Achieve ≥85% overall test coverage with 95% domain, 90% application, 80% adapter layers
NFR2: Security - Zero critical/high security vulnerabilities per OWASP ZAP scan and dependency audit
NFR3: Performance - HTTP latency p95 < 500ms for non-AI endpoints
NFR4: Scalability - Support 1000 concurrent users with connection pooling, rate limiting, stateless design
NFR5: AI Latency - First token delivery < 2s for streaming AI responses
NFR6: Maintainability - Technical debt ratio < 10% per SonarQube analysis
NFR7: Dead Code - 0% unused exports per static analysis
NFR8: Build Time - CI pipeline < 5 minutes
NFR9: Migration Success - 100% EF migrations apply cleanly
NFR10: API Documentation - 100% endpoint documentation coverage
NFR11: Pattern Consistency - 100% features follow Hexagonal Architecture patterns
NFR12: Onboarding Time - New contributors productive in < 1 day (8 hours)
NFR13: Subscription Operations - 99.9% success rate
NFR14: Webhook Processing - 99.9% success rate
NFR15: Credit Operations - 100% accuracy with balance reconciliation
NFR16: Auth Success Rate - 99.9% authentication success
NFR17: Auth Migration - 100% users migrated without re-registration
NFR18: Login Latency - < 500ms p95
NFR19: Agent Response Time - p95 < 3s
NFR20: Plugin Load Time - < 100ms
NFR21: Trace Coverage - 100% agent operations traced
NFR22: Plugin Search Latency - < 200ms p95
NFR23: Plugin Installation Success - 99.9%

### Additional Requirements

**Architecture Constraints (Must Preserve):**
- Hexagonal Architecture (Ports & Adapters) pattern for all new code
- CQRS via MediatR for command/query separation
- Domain Events (in-memory) + Integration Messages (RabbitMQ) for side effects
- .NET 9 runtime - no framework downgrade

**Technology Stack (Locked):**
- PostgreSQL 17 + pgvector for data and embeddings
- Redis 7+ for distributed cache, sessions, rate limits
- RabbitMQ + MassTransit 8.4.0 for messaging
- Semantic Kernel 1.54.0 for AI orchestration
- Langfuse for LLM telemetry
- Entity Framework Core 9.0.5 for data access

**Cross-Cutting Concerns:**
- Authentication: Transitional support for both custom JWT and Auth0 during migration
- Authorization: Extend ownership model for marketplace (creator permissions)
- Credit/Billing: Event-driven usage tracking, idempotent Stripe webhooks
- Observability: Langfuse spans for AI, Prometheus metrics, structured logging
- Caching: Redis with invalidation strategies for marketplace listings
- Messaging: RabbitMQ for async jobs, subscription events, marketplace notifications
- Error Handling: Domain exceptions, graceful degradation, retry policies

**External Service Dependencies:**
- OpenAI API (existing) - runtime dependency for AI features
- Auth0 (new in Epic 0002) - managed identity provider
- Stripe (completion in Epic 0001) - payment processing

**Epic Dependencies:**
- Epic 0000 (Stabilize) blocks Epics 0001, 0002, 0003, 0004
- Epic 0001 (Subscriptions) blocks Epics 0003, 0004
- Epic 0002 (Auth0) blocks Epics 0003, 0004
- Epic 0003 (Agent Framework) blocks Epic 0004

**No Starter Template Required:**
- This is a brownfield project extending existing Hexagonal architecture
- All new code must integrate with existing project structure

### FR Coverage Map

| FR | Epic | Description |
|----|------|-------------|
| FR1 | Epic 1 | Security audit and vulnerability fixes |
| FR2 | Epic 1 | Dead code elimination via static analysis |
| FR3 | Epic 1 | Test coverage achievement (85%+) |
| FR4 | Epic 1 | Pattern consistency enforcement |
| FR5 | Epic 1 | Documentation updates |
| FR6 | Epic 2 | Stripe subscription lifecycle |
| FR7 | Epic 2 | Webhook processing with idempotency |
| FR8 | Epic 2 | Credit system implementation |
| FR9 | Epic 2 | Billing management API |
| FR10 | Epic 2 | Admin billing tools |
| FR11 | Epic 3 | Auth0 tenant configuration |
| FR12 | Epic 3 | JWT validation with Auth0 JWKS |
| FR13 | Epic 3 | User synchronization Auth0 ↔ local DB |
| FR14 | Epic 3 | User migration without password reset |
| FR15 | Epic 3 | MFA and social login enablement |
| FR16 | Epic 3 | Legacy auth code removal |
| FR17 | Epic 4 | Agent abstraction layer (IAgentRunner) |
| FR18 | Epic 4 | Plugin system standardization |
| FR19 | Epic 4 | Conversation management |
| FR20 | Epic 4 | Observability integration (Langfuse) |
| FR21 | Epic 4 | Credit system integration for agents |
| FR22 | Epic 4 | Existing agent migration |
| FR23 | Epic 5 | Plugin catalog API |
| FR24 | Epic 5 | Plugin installation system |
| FR25 | Epic 5 | Rating and review system |
| FR26 | Epic 5 | Publisher system |
| FR27 | Epic 5 | Plugin versioning |
| FR28 | Epic 5 | Usage analytics |

## Epic List

### Epic 1: Platform Stabilization & Security Foundation

**Goal:** Developers and operators gain confidence in a secure, well-tested, maintainable codebase that follows consistent patterns and has comprehensive documentation.

**FRs Covered:** FR1, FR2, FR3, FR4, FR5
**NFRs Addressed:** NFR1-12 (coverage, security, performance, maintainability, documentation)
**Priority:** P0 (Critical Path)
**Phase:** 1 - Stabilize

---

### Epic 2: Subscription & Billing System

**Goal:** Users can subscribe to plans, purchase credits, manage their billing, and consume AI services with proper usage tracking. Admins can oversee billing operations.

**FRs Covered:** FR6, FR7, FR8, FR9, FR10
**NFRs Addressed:** NFR13, NFR14, NFR15
**Priority:** P1 (High)
**Phase:** 2 - Complete
**Dependencies:** Epic 1

---

### Epic 3: Auth0 Identity Migration

**Goal:** Users benefit from enterprise-grade authentication with MFA, social login options, and seamless session continuity during migration. Reduced security maintenance burden for the platform.

**FRs Covered:** FR11, FR12, FR13, FR14, FR15, FR16
**NFRs Addressed:** NFR16, NFR17, NFR18
**Priority:** P1 (High)
**Phase:** 2 - Complete
**Dependencies:** Epic 1

---

### Epic 4: Agent Framework Standardization

**Goal:** Users experience consistent, observable, credit-tracked AI interactions across all agent types. Developers can extend agents using a standardized plugin architecture.

**FRs Covered:** FR17, FR18, FR19, FR20, FR21, FR22
**NFRs Addressed:** NFR19, NFR20, NFR21
**Priority:** P2 (Medium)
**Phase:** 3 - Extend
**Dependencies:** Epic 1, Epic 2, Epic 3

---

### Epic 5: Plugin Marketplace

**Goal:** Users can discover, install, rate, and manage plugins to extend their AI agents. Publishers can submit and distribute plugins through a curated ecosystem.

**FRs Covered:** FR23, FR24, FR25, FR26, FR27, FR28
**NFRs Addressed:** NFR22, NFR23
**Priority:** P2 (Medium)
**Phase:** 3 - Extend
**Dependencies:** Epic 4
