# Orchid Core - Project Overview

## Executive Summary

**Orchid Core** is a full-stack backend platform built with .NET 9 following Hexagonal (Ports & Adapters) architecture. It serves as the core API for an ecosystem of applications, providing AI-powered chat, agent management, knowledge base, spreadsheet processing, and team collaboration features.

## Project Classification

| Attribute | Value |
|-----------|-------|
| **Project Type** | Backend API |
| **Architecture** | Hexagonal (Clean Architecture) |
| **Language** | C# / .NET 9.0 |
| **Database** | PostgreSQL 17 with pgvector |
| **Repository Type** | Monolith |

## Core Capabilities

### 1. Agent Management
- Create and manage AI agents with custom personalities
- Plugin marketplace for extending agent capabilities
- Agent ratings and public/private visibility
- Soft delete with recycle bin functionality

### 2. AI & Chat
- Real-time chat sessions with AI agents
- Semantic Kernel integration for LLM orchestration
- OpenAI gpt-5-nano-2025-08-07 as primary model
- RAG (Retrieval-Augmented Generation) via pgvector
- Streaming responses for low latency

### 3. Knowledge Base
- Document upload and processing
- Vector embeddings for semantic search
- Text extraction from multiple formats
- Per-user document management

### 4. Spreadsheet Processing
- Excel file upload and analysis
- Natural language Q&A on spreadsheet data
- Formula execution and validation
- Compression pipeline for large files
- CLI tool for batch processing

### 5. Team Collaboration
- Multi-agent team creation
- Team interaction policies
- Huddle video calls via SignalR

### 6. Credit System
- Token-based credit consumption
- Subscription plans and billing
- Usage tracking and limits
- Cost configuration per action

### 7. Authentication & Security
- JWT Bearer tokens with refresh token rotation
- Google OAuth integration
- Rate limiting (IP + account-based)
- PBKDF2 password hashing
- Secure cookie handling

## Technology Stack

| Category | Technology | Version |
|----------|------------|---------|
| Runtime | .NET | 9.0 |
| Web Framework | ASP.NET Core | 9.0 |
| Database | PostgreSQL + pgvector | 17 |
| ORM | Entity Framework Core | 9.0.5 |
| Cache | Redis | 7+ |
| Message Broker | RabbitMQ + MassTransit | 3 / 8.4.0 |
| AI Framework | Semantic Kernel | 1.54.0 |
| Real-time | SignalR | - |
| Telemetry | Langfuse | 3 |
| API Docs | Swagger/OpenAPI | 7.3.1 |

## Architecture Pattern

The solution follows **Hexagonal Architecture** (Ports & Adapters):

```
┌─────────────────────────────────────────────────────────────┐
│                        ADAPTERS                              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐       │
│  │   WebApi     │  │   Workers    │  │     CLI      │       │
│  │ (Inbound)    │  │  (Inbound)   │  │  (Inbound)   │       │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘       │
│         │                 │                 │                │
│         ▼                 ▼                 ▼                │
│  ┌─────────────────────────────────────────────────────┐    │
│  │                 APPLICATION LAYER                    │    │
│  │          (Use Cases, Commands, Queries)              │    │
│  │                    via MediatR                       │    │
│  └─────────────────────────────────────────────────────┘    │
│                            │                                 │
│                            ▼                                 │
│  ┌─────────────────────────────────────────────────────┐    │
│  │                   DOMAIN LAYER                       │    │
│  │        (Entities, Value Objects, Events)             │    │
│  └─────────────────────────────────────────────────────┘    │
│                            │                                 │
│                            ▼                                 │
│  ┌─────────────────────────────────────────────────────┐    │
│  │               INFRASTRUCTURE ADAPTER                 │    │
│  │   (Database, Redis, RabbitMQ, AI, Storage)          │    │
│  │                    (Outbound)                        │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

## Key Design Patterns

| Pattern | Usage |
|---------|-------|
| **CQRS** | Commands and Queries via MediatR |
| **Mediator** | Request/Response handling |
| **Repository** | Data access abstraction |
| **Domain Events** | Internal event propagation |
| **Integration Messages** | RabbitMQ-based async communication |
| **Strategy** | Interchangeable algorithms |

## Quick Links

- [Architecture Details](./architecture.md)
- [API Contracts](./api-contracts.md)
- [Data Models](./data-models.md)
- [Development Guide](./development-guide.md)
- [Source Tree Analysis](./source-tree-analysis.md)
