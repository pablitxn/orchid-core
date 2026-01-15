---
stepsCompleted: [1, 2, 3, 4, 5, 6]
workflowComplete: true
completedAt: 2026-01-04
inputDocuments:
  - docs/index.md
  - docs/project-overview.md
  - docs/architecture.md
  - docs/api-contracts.md
  - docs/data-models.md
  - docs/source-tree-analysis.md
  - docs/development-guide.md
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/epic-0000-stabilize-mvp.md
  - _bmad-output/planning-artifacts/epic-0001-subscriptions.md
  - _bmad-output/planning-artifacts/epic-0002-auth0-integration.md
  - _bmad-output/planning-artifacts/epic-0003-agent-framework.md
  - _bmad-output/planning-artifacts/epic-0004-marketplace.md
date: 2026-01-04
author: Pablo
---

# Product Brief: orchid-core

## Executive Summary

**orchid-core** is an AI agent orchestration platform that empowers users to create, customize, and deploy intelligent agents while enabling creators to build and monetize plugins through a unified marketplace. By leveraging open standards (MCP, OpenAPI), orchid-core offers a flexible alternative to closed AI ecosystems, serving both builders who want to create and monetize AI tools, and users who want powerful, customizable AI capabilities without technical complexity.

---

## Core Vision

### Problem Statement

Today's AI landscape forces users into frustrating trade-offs:

- **Closed ecosystems** (like ChatGPT) offer ease of use but limit customization and offer negligible creator monetization
- **Developer frameworks** (like LangChain) provide power but require significant technical expertise
- **Automation tools** (like Zapier) handle workflows but aren't AI-native
- **Custom builds** give full control but are expensive, slow, and isolated from community innovation

There is no unified platform where users can easily create custom AI agents, extend them with plugins using open standards, organize them into collaborative teams, and participate in a marketplace - all in one place.

### Problem Impact

- **Creators** can't monetize their AI expertise without building entire products
- **Users** can't access powerful, customized AI without coding skills or cobbling together multiple tools
- **Teams** can't leverage multi-agent collaboration without complex infrastructure
- **Innovation** is siloed in closed platforms that don't interoperate

### Why Existing Solutions Fall Short

| Solution | Limitation |
|----------|------------|
| ChatGPT + GPTs | Closed ecosystem, limited customization, minimal creator revenue |
| LangChain / LangGraph | Requires coding expertise, no marketplace, no monetization |
| Zapier / Make | Not AI-native, no agents, no multi-agent teams |
| Custom development | Expensive, slow, no community, no marketplace |

### Proposed Solution

**orchid-core** is an all-in-one AI agent platform that:

1. **Simplifies agent creation** - Build custom AI agents with personalities, knowledge bases, and capabilities without deep technical expertise
2. **Embraces open standards** - Extend agents using MCP and OpenAPI protocols for maximum flexibility and portability
3. **Enables monetization** - Creators can build, publish, and sell plugins through an integrated marketplace
4. **Supports multi-agent teams** - Organize agents into teams that collaborate to solve complex problems
5. **Provides integrated workflows** - Connect agents, tools, and workflows in a unified experience

### Key Differentiators

| Differentiator | Value |
|----------------|-------|
| **Protocol-native** | MCP + OpenAPI standards vs. proprietary plugin systems |
| **True monetization** | Real revenue opportunity for creators, not token payments |
| **Multi-agent teams** | Collaborative agent orchestration vs. single-agent tools |
| **All-in-one platform** | Build, use, monetize, collaborate without switching tools |
| **Open alternative** | Flexible option vs. locked-in closed ecosystems |

---

## Target Users

### Primary Users

#### 1. The Creator ("Alex the Builder")

**Profile:**
- **Role:** Developer, technical power user, or AI enthusiast
- **Background:** Comfortable with APIs, has built integrations before, familiar with MCP/OpenAPI concepts
- **Motivation:** Monetize expertise, build once and earn passively, showcase skills, solve their own problems then share solutions

**Current Frustration:**
- Builds custom AI tools but has no easy way to distribute or monetize them
- Creates MCP servers or OpenAPI plugins but they're stuck on their local machine
- Sees the GPT Store but knows creator revenue is negligible
- Wants to contribute to an ecosystem, not just build in isolation

**Success Looks Like:**
- Publishes a plugin in the marketplace and earns real revenue
- Sees adoption metrics - installs, ratings, usage stats
- Builds a reputation as a trusted plugin creator
- Can iterate on plugins based on user feedback

**Day in the Life:**
> Alex builds a custom MCP server for querying their company's internal docs. It works great locally. They think "others would pay for this." With orchid-core, they package it as a plugin, publish to the marketplace, and start earning from teams who have the same problem.

---

#### 2. The Consumer ("Sam the Solver")

**Profile:**
- **Role:** Knowledge worker, freelancer, small business owner, or team lead
- **Background:** Not deeply technical, but comfortable using AI tools (ChatGPT, etc.)
- **Motivation:** Get work done faster, automate repetitive tasks, access powerful AI without coding

**Current Frustration:**
- Uses ChatGPT but can't customize it enough for their specific workflows
- Tried building their own solution but it's too complex
- Cobbles together 5+ tools (Zapier, ChatGPT, Notion AI, etc.) that don't talk to each other
- Can't find the exact AI capability they need in closed ecosystems

**Success Looks Like:**
- Creates a custom agent tailored to their workflow in minutes
- Browses the marketplace and finds a plugin that solves their exact problem
- Builds a team of agents that collaborate on complex tasks
- Gets more done without switching between multiple tools

**Day in the Life:**
> Sam runs a small marketing agency. They create an "SEO Analyst" agent, add the "Keyword Research" plugin from the marketplace, connect their knowledge base of client briefs, and suddenly have a specialized AI assistant that understands their business. No code required.

---

### Secondary Users

#### 3. The Team Admin ("Jordan the Organizer")

**Profile:**
- **Role:** Team lead, ops manager, or IT admin in a small-to-medium business
- **Motivation:** Enable their team with AI tools while maintaining control and oversight

**Key Needs:**
- Manage which plugins and agents the team can access
- Monitor usage and costs (credits/subscriptions)
- Ensure consistency across team workflows
- Onboard new team members with pre-configured agents

---

#### 4. The Enterprise Evaluator ("Taylor the Buyer")

**Profile:**
- **Role:** Technology decision-maker evaluating AI platforms for their organization
- **Motivation:** Find a flexible, secure platform that won't lock them into a single vendor

**Key Needs:**
- Open standards (MCP, OpenAPI) for future flexibility
- Self-hosting or data sovereignty options
- Integration with existing enterprise systems
- Clear pricing and ROI visibility

---

### User Journey

| Stage | Creator (Alex) | Consumer (Sam) |
|-------|----------------|----------------|
| **Discovery** | Finds orchid via developer communities, Twitter/X, or searching for "monetize MCP plugins" | Finds orchid searching for "custom AI agents" or via creator's plugin promotion |
| **Onboarding** | Creates account, explores plugin SDK/docs, submits first plugin for review | Creates account, builds first agent, browses marketplace |
| **First Value** | Plugin approved, first install notification | Agent solves a real problem they couldn't solve before |
| **Aha Moment** | First revenue from plugin sales | Realizes they can combine multiple plugins into one powerful agent |
| **Long-term** | Builds portfolio of plugins, earns recurring revenue, becomes known creator | Has suite of custom agents, team workflows, relies on platform daily |

---

## Success Metrics

### User Success Metrics

#### Creator Success (Alex)

| Metric | Definition | MVP Target (3 months) |
|--------|------------|----------------------|
| **Plugins Published** | # of plugins successfully submitted and approved | 10+ plugins |
| **Creator Activation** | % of registered creators who publish at least 1 plugin | > 20% |
| **Plugin Installs** | Average installs per published plugin | 5+ installs/plugin |
| **Creator Revenue** | Total $ earned by creators | First $100 |

**Creator "Aha" Moment:** First notification that someone installed their plugin.

---

#### Consumer Success (Sam)

| Metric | Definition | MVP Target (3 months) |
|--------|------------|----------------------|
| **Agents Created** | # of custom agents built by users | 50+ agents |
| **Time to First Agent** | Time from signup to first working agent | < 10 minutes |
| **Plugins Installed** | Average plugins installed per user | 2+ plugins/user |
| **Weekly Active Users** | Users who interact with their agents weekly | 100 WAU |

**Consumer "Aha" Moment:** First time their custom agent solves a real problem they couldn't solve before.

---

### Business Objectives

| Timeframe | Objective | Key Result |
|-----------|-----------|------------|
| **Month 1** | Prove agent creation works | 25+ agents created, users returning |
| **Month 2** | Prove marketplace works | 10+ plugins published, first installs |
| **Month 3** | Prove monetization works | First creator revenue, healthy retention |
| **Month 6** | Healthy ecosystem | Both sides growing, repeat transactions |
| **Year 1** | Sustainable business | Revenue trajectory, clear product-market fit |

---

### Key Performance Indicators

#### Primary KPIs (MVP Focus)

| KPI | Target | Measurement |
|-----|--------|-------------|
| **Active Users (WAU)** | 100 | Weekly logins + agent interactions |
| **Plugins in Marketplace** | 10 | Published & approved plugins |
| **Creator Revenue** | $100 | First real monetization |
| **Retention (Week 1)** | > 40% | Users returning after first week |

#### Secondary KPIs (Monitor)

| KPI | Purpose |
|-----|---------|
| **Signup → Agent Created** | Conversion funnel health |
| **Signup → Plugin Published** | Creator funnel health |
| **Plugin Install Rate** | Marketplace discovery |
| **Support Tickets** | User friction signals |

---

### North Star Metric

**"Weekly Active Agent Interactions"**

This single metric captures value on both sides:
- Consumers are using agents (getting value)
- Agents use plugins (creators get installs/revenue)
- Platform is enabling real work to get done

---

## MVP Scope

### Core Features (Must Have)

#### For Consumers (Sam)

| Feature | Description | Success Criteria |
|---------|-------------|------------------|
| **Agent Creation** | Create custom AI agents with name, personality, and configuration | Agent created in < 5 minutes |
| **Agent Chat** | Conversational interface to interact with agents | Real-time responses, context retention |
| **Plugin Installation** | Browse marketplace and add plugins to agents | One-click install, immediate availability |
| **Knowledge Base** | Upload documents to give agents context | Supports common formats, RAG search works |
| **Account & Auth** | Sign up, log in, manage profile | Secure authentication, session management |

#### For Creators (Alex)

| Feature | Description | Success Criteria |
|---------|-------------|------------------|
| **Plugin Publishing** | Submit MCP/OpenAPI plugins to marketplace | Submission → review → published flow |
| **Plugin Management** | Update, version, and monitor plugins | Version history, install stats visible |
| **Marketplace Listing** | Plugin appears in searchable catalog | Discoverable by consumers |
| **Monetization** | Set pricing, receive revenue from sales | Payments processed, revenue visible |

#### Platform Foundation

| Feature | Description | Success Criteria |
|---------|-------------|------------------|
| **Marketplace** | Searchable catalog of plugins | Search, filter, categories work |
| **Credits System** | Usage-based consumption model | Credits deducted on AI usage |
| **Subscription Plans** | Tiered access and credit allocation | Plans selectable, billing works |

---

### Out of Scope for MVP

| Feature | Rationale | Target Phase |
|---------|-----------|--------------|
| **Multi-Agent Teams** | Adds complexity, core agent value comes first | v2 |
| **Workflows & Automation** | Nice-to-have, not essential for initial value | v2 |
| **Video Calls / Huddles** | Not core to agent platform value | v2+ |
| **Enterprise Features** | SSO, self-hosting, advanced admin - wait for traction | v2+ |
| **Advanced Analytics** | Basic metrics first, expand based on needs | v2 |
| **Mobile Apps** | Web-first, mobile can follow | v2+ |
| **API Access** | Platform UI first, API for power users later | v2 |

---

### MVP Success Criteria

The MVP is successful when:

| Criteria | Target | Validation |
|----------|--------|------------|
| **Agents are useful** | Users create agents that solve real problems | 50+ agents created, users returning |
| **Marketplace works** | Creators publish, consumers install | 10+ plugins, installs happening |
| **Monetization validates** | Revenue flows to creators | First $100 in creator earnings |
| **Retention is healthy** | Users come back | >40% week-1 retention |

**Go/No-Go Decision Point:** At 3 months, evaluate if core loop is working before investing in v2 features.

---

### Future Vision

#### Phase 2: Teams & Collaboration
- Multi-agent teams that work together
- Shared workspaces for organizations
- Agent-to-agent communication

#### Phase 3: Workflows & Automation
- Visual workflow builder
- Scheduled agent tasks
- Event-driven triggers
- Integration with external systems

#### Phase 4: Enterprise & Scale
- SSO / SAML authentication
- Self-hosting options
- Advanced admin controls
- Audit logs and compliance

#### Long-term Vision (2-3 years)
- **The "App Store" for AI agents** - largest marketplace of AI capabilities
- **AI-native productivity platform** - where work gets done with agent teams
- **Creator economy for AI** - developers earn meaningful revenue from their expertise
- **Enterprise AI orchestration** - companies deploy custom agent fleets
