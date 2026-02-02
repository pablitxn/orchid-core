# Romy-Bot Integration Plan

*Created: 2026-01-30*

---

## Executive Summary

Integrar **romy-bot** (content management assistant) como plugin OpenAPI dentro de **orchid-core**, permitiendo que los agentes de Orchid utilicen las capacidades de Romy para gestionar contenido, calendario editorial, y estrategia de contenidos.

---

## Current State Analysis

### romy-bot
| Aspect | Status |
|--------|--------|
| API Framework | FastAPI (Python 3.11+) |
| API Port | 8000 (default) |
| OpenAPI Spec | Auto-generated at `/openapi.json` |
| Docs | Swagger UI at `/docs`, ReDoc at `/redoc` |
| Key Endpoints | `/api/v1/publications`, `/ideas`, `/metrics`, `/analysis/*`, `/strategy`, `/knowledge-base/*` |

### orchid-core (Backend)
| Aspect | Status |
|--------|--------|
| OpenAPI Plugin Support | **Complete** (ADR-0003) |
| Admin Endpoints | `/api/admin/openapi-plugins/*` |
| ToolRegistry | Supports builtin + OpenAPI plugins |
| Spec Caching | 1-hour TTL with fallback |

### orchid-webapp (Frontend)
| Aspect | Status |
|--------|--------|
| Basic Plugin UI | `/plugins` page exists |
| OpenAPI Plugin Creation | **NOT IMPLEMENTED** |
| Admin Plugin Panel | **NOT IMPLEMENTED** |

---

## Work Breakdown

### Phase 1: Backend Validation (orchid-core)
**Goal:** Verify romy-bot can be registered and invoked as OpenAPI plugin

- [ ] **1.1** Start romy-bot locally (`python -m romy_bot.main --api`)
- [ ] **1.2** Validate OpenAPI spec accessibility at `http://localhost:8000/openapi.json`
- [ ] **1.3** Use Admin API to register romy-bot plugin:
  ```json
  POST /api/admin/openapi-plugins
  {
    "name": "Romy Bot",
    "systemName": "romy_bot",
    "description": "Content management and strategy assistant",
    "baseUrl": "http://localhost:8000",
    "openApiSpecUrl": "/openapi.json",
    "priceCredits": 5,
    "isActive": true
  }
  ```
- [ ] **1.4** Verify spec caching works via `/api/admin/openapi-plugins/{id}`
- [ ] **1.5** Test tool invocation through an agent chat session
- [ ] **1.6** Document any API compatibility issues

### Phase 2: Frontend - OpenAPI Plugin Creation UI (orchid-webapp)
**Goal:** Allow users/admins to create OpenAPI plugins from the UI

#### 2.1 API Service Layer
- [ ] Add admin OpenAPI plugin endpoints to `services/constants.ts`:
  ```typescript
  ADMIN_OPENAPI_PLUGINS: '/api/admin/openapi-plugins',
  ADMIN_OPENAPI_PLUGIN: (id: string) => `/api/admin/openapi-plugins/${id}`,
  ADMIN_OPENAPI_PLUGIN_REFRESH: (id: string) => `/api/admin/openapi-plugins/${id}/refresh`,
  ADMIN_OPENAPI_PLUGIN_VALIDATE: '/api/admin/openapi-plugins/validate',
  ```
- [ ] Create `services/admin-plugins.ts` with:
  - `listOpenApiPlugins()`
  - `getOpenApiPlugin(id)`
  - `createOpenApiPlugin(data)`
  - `updateOpenApiPlugin(id, data)`
  - `deleteOpenApiPlugin(id)`
  - `refreshPluginSpec(id)`
  - `validatePluginSpec(baseUrl, specUrl)`

#### 2.2 TypeScript Types
- [ ] Add types to `types/plugin.ts`:
  ```typescript
  interface OpenApiPluginDto {
    id: string;
    name: string;
    systemName: string;
    description?: string;
    baseUrl: string;
    openApiSpecUrl: string;
    priceCredits: number;
    isSubscriptionBased: boolean;
    isActive: boolean;
    specCachedAt?: string;
    timeoutSeconds?: number;
    availableFunctions?: OpenApiFunction[];
  }

  interface OpenApiFunction {
    name: string;
    description?: string;
    method: string;
    path: string;
  }

  interface CreateOpenApiPluginRequest {
    name: string;
    systemName: string;
    description?: string;
    baseUrl: string;
    openApiSpecUrl?: string; // defaults to /openapi.json
    priceCredits?: number;
    isSubscriptionBased?: boolean;
    timeoutSeconds?: number;
  }
  ```

#### 2.3 Admin Pages
- [ ] Create `/pages/admin/plugins/page.tsx` - List all OpenAPI plugins
- [ ] Create `/pages/admin/plugins/new.tsx` - Create OpenAPI plugin form
- [ ] Create `/pages/admin/plugins/[id]/edit.tsx` - Edit existing plugin

#### 2.4 Plugin Creation Form Components
- [ ] `components/admin/openapi-plugin-form.tsx`:
  - Name input (required)
  - System name input (auto-generated from name, editable)
  - Description textarea
  - Base URL input (required)
  - OpenAPI Spec URL input (default: `/openapi.json`)
  - Price credits input (default: 10)
  - Subscription-based toggle
  - Timeout seconds input (default: 30)
  - **Validate Spec** button - calls validate endpoint, shows available functions
  - **Create/Update** button

#### 2.5 Plugin List Component
- [ ] `components/admin/openapi-plugin-list.tsx`:
  - Table with columns: Name, System Name, Base URL, Status, Actions
  - Actions: Edit, Refresh Cache, Delete
  - Status indicator (active/inactive)
  - Last cached timestamp

#### 2.6 Router Updates
- [ ] Add admin routes in `config/router.tsx`:
  ```typescript
  { path: '/admin/plugins', element: <AdminPluginsPage /> },
  { path: '/admin/plugins/new', element: <AdminPluginNewPage /> },
  { path: '/admin/plugins/:id/edit', element: <AdminPluginEditPage /> },
  ```

#### 2.7 Navigation
- [ ] Add "Plugins" link to admin sidebar/menu

### Phase 3: User Plugin Assignment
**Goal:** Users can assign OpenAPI plugins to their agents

- [ ] **3.1** Update agent edit page to show available OpenAPI plugins
- [ ] **3.2** Allow selecting/deselecting plugins for agent
- [ ] **3.3** Store plugin assignments in agent's `PluginIds`

### Phase 4: Testing & Documentation
- [ ] **4.1** Integration test: Create plugin via UI → Invoke via agent
- [ ] **4.2** E2E test with Playwright (optional)
- [ ] **4.3** Update `docs/api-contracts.md` with new frontend flows
- [ ] **4.4** Add usage example to CLAUDE.md

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                         orchid-webapp                                │
│  ┌─────────────────┐    ┌──────────────────┐    ┌────────────────┐  │
│  │  Admin Plugin   │───▶│ admin-plugins.ts │───▶│ API Calls      │  │
│  │  Pages          │    │ (service)        │    │ /api/admin/... │  │
│  └─────────────────┘    └──────────────────┘    └────────────────┘  │
└──────────────────────────────────────────────────────────────────────
                                   │
                                   ▼
┌──────────────────────────────────────────────────────────────────────┐
│                         orchid-core                                   │
│  ┌─────────────────┐    ┌──────────────────┐    ┌────────────────┐  │
│  │ AdminOpenApi    │───▶│ CreateOpenApi    │───▶│ PluginEntity   │  │
│  │ PluginsController│   │ PluginHandler    │    │ (database)     │  │
│  └─────────────────┘    └──────────────────┘    └────────────────┘  │
│                                                          │           │
│  ┌─────────────────┐    ┌──────────────────┐            │           │
│  │  ToolRegistry   │◀───│ OpenApiTool      │◀───────────┘           │
│  │                 │    │ Factory          │                        │
│  └────────┬────────┘    └──────────────────┘                        │
│           │                                                          │
│           ▼                                                          │
│  ┌─────────────────┐                                                │
│  │  Agent Chat     │─────────────────────────────────────┐          │
│  │  Session        │                                     │          │
│  └─────────────────┘                                     │          │
└──────────────────────────────────────────────────────────┼──────────┘
                                                           │
                                                           ▼ HTTP
┌──────────────────────────────────────────────────────────────────────┐
│                          romy-bot                                     │
│  ┌─────────────────┐    ┌──────────────────┐    ┌────────────────┐  │
│  │  FastAPI        │───▶│ /openapi.json    │    │ SQLite DB      │  │
│  │  :8000          │    │ (auto-generated) │    │ (publications, │  │
│  │                 │    └──────────────────┘    │  ideas, etc.)  │  │
│  │  Endpoints:     │                            └────────────────┘  │
│  │  /publications  │                                                │
│  │  /ideas         │                                                │
│  │  /analysis/*    │                                                │
│  │  /strategy      │                                                │
│  └─────────────────┘                                                │
└──────────────────────────────────────────────────────────────────────┘
```

---

## Priority Order

| Priority | Task | Effort | Dependencies |
|----------|------|--------|--------------|
| P0 | Phase 1 (Backend Validation) | 1h | romy-bot running |
| P1 | Phase 2.1-2.2 (API Service + Types) | 2h | None |
| P1 | Phase 2.3-2.4 (Admin Pages + Form) | 4h | 2.1-2.2 |
| P2 | Phase 2.5-2.7 (List + Router + Nav) | 2h | 2.3-2.4 |
| P2 | Phase 3 (User Plugin Assignment) | 2h | Phase 2 |
| P3 | Phase 4 (Testing & Docs) | 2h | All above |

---

## Open Questions

1. **Auth for romy-bot**: Does romy-bot require API keys? How should credentials be stored/passed?
2. **Multi-tenant**: Should each user have their own romy-bot instance, or share one?
3. **Deployment**: Where will romy-bot be deployed? (localhost for dev, k8s for prod?)
4. **Rate limiting**: Should we add rate limits to prevent abuse of external APIs?

---

## Success Criteria

- [ ] Admin can create an OpenAPI plugin for romy-bot via UI
- [ ] Plugin spec is fetched and cached successfully
- [ ] Agent can invoke romy-bot functions (e.g., list publications, create ideas)
- [ ] Activity events show tool invocations in chat UI
- [ ] No regressions in existing plugin functionality

---

## Next Steps

1. Review this plan and clarify open questions
2. Start with Phase 1 to validate backend compatibility
3. Proceed to Phase 2 for frontend implementation
