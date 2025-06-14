# Playground Dotnet

**Playground Dotnet** is a full‑stack sandbox demonstrating Hexagonal architecture with .NET, RabbitMQ and a React
frontend. The project bundles a suite of experimental modules—AI chat, agent management and real‑time video calls—into
a single repository.

---

## Overview

The backend follows a *Ports & Adapters* approach where the business logic is isolated from infrastructure concerns.
Frontend code lives under `apps/web` and uses Vite with a Neo‑Brutalism Tailwind theme. Key capabilities include:

* **Agent Management** with a [Plugin Marketplace](docs/agent-management.md) to install custom tools.
* **Teams** feature for multiagent collaboration, described in [team-multiagent](docs/team-multiagent.md).
* **Huddle** video calls built on SignalR, outlined in [huddle.md](docs/huddle.md).
* **Knowledge Base** for document uploads and retrieval‑augmented chat.
* **Spreadsheet Processing** with compression and natural language Q&A capabilities.
* Localized UI supporting English, Spanish, French, Russian and Japanese.
* Light and dark modes via the `ThemeProvider` component.

---

## Directory Layout

```text
services/backend/   # .NET solution and tests
apps/web/           # React frontend application
docs/               # Architecture notes and ADRs
database/           # Migrations and seed data
```

The high‑level architecture is summarized in `docs/architecture.md`.

---

## Getting Started

### Prerequisites

* .NET 9 SDK
* Node.js 22 with Yarn
* Local PostgreSQL and RabbitMQ instances

### Backend

```bash
dotnet build services/backend/Backend.sln
dotnet run --project services/backend/src/Adapters/WebApi
```

### CLI Tools

```bash
# Build the spreadsheet CLI tool
cd services/backend/src/Adapters/CLI/SpreadsheetCLI
dotnet publish -c Release -r linux-x64 -p:PublishSingleFile=true

# Use the CLI for spreadsheet processing
./bin/Release/net8.0/linux-x64/publish/ssllm run data.xlsx --recipe full --question "What is the total?"
```

### Frontend

```bash
yarn --cwd apps/web dev
```

### Tests

Run the full test suite and format checks via:

```bash
bash ./scripts/ci.sh
```

---

## Further Reading

* [Architecture](docs/architecture.md)
* [Frontend Guidelines](docs/frontend.md)
* [ADR Index](docs/addr)

Contributions follow the guidelines in [AGENTS.md](AGENTS.md). Pull requests should run the CI script and update the
[CHANGELOG](CHANGELOG.md).
