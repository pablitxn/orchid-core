# Plan de Migración: BMAD → GitHub Issues

**Fecha:** 2026-01-15
**Autor:** Pablo + Claude
**Status:** COMPLETADO

---

## Objetivo

Migrar toda la planificación de desarrollo (epics, stories, requirements) desde los archivos markdown de BMAD hacia GitHub Issues para habilitar:

- Colaboración multi-persona y multi-agente
- Tracking visual del progreso (GitHub Projects)
- Integración con CI/CD y PRs
- Notificaciones y asignaciones
- Automatización con GitHub Actions

---

## Inventario de Contenido a Migrar

### Epics (5 total)

| Epic ID | Nombre | Priority | Phase | Stories |
|---------|--------|----------|-------|---------|
| 0000 | Stabilize MVP | P0 | 1 | 5 REQs |
| 0001 | Subscriptions | P1 | 2 | 5 REQs |
| 0002 | Auth0 Integration | P1 | 2 | 6 REQs |
| 0003 | Agent Framework | P2 | 3 | 6 REQs |
| 0004 | Marketplace | P2 | 3 | 6 REQs |

**Total: 28 Requirements/Stories a migrar**

### Dependencias entre Epics

```
Epic 0000 (Stabilize)
    └── blocks → Epic 0001, 0002, 0003, 0004

Epic 0001 (Subscriptions) + Epic 0002 (Auth0)
    └── blocks → Epic 0003

Epic 0003 (Agent Framework)
    └── blocks → Epic 0004
```

---

## Estructura en GitHub

### 1. Labels

#### Por Tipo
| Label | Color | Descripción |
|-------|-------|-------------|
| `epic` | `#3E4B9E` | Parent epic issue |
| `story` | `#0E8A16` | User story / requirement |
| `task` | `#C2E0C6` | Implementation task |
| `bug` | `#D73A4A` | Bug fix |
| `spike` | `#FBCA04` | Research/investigation |

#### Por Prioridad
| Label | Color | Descripción |
|-------|-------|-------------|
| `priority: P0` | `#B60205` | Critical path - blocks everything |
| `priority: P1` | `#D93F0B` | High priority |
| `priority: P2` | `#FBCA04` | Medium priority |
| `priority: P3` | `#0E8A16` | Low priority |

#### Por Fase
| Label | Color | Descripción |
|-------|-------|-------------|
| `phase: 1-stabilize` | `#1D76DB` | Foundation work |
| `phase: 2-complete` | `#5319E7` | Feature completion |
| `phase: 3-extend` | `#006B75` | New capabilities |

#### Por Área
| Label | Color | Descripción |
|-------|-------|-------------|
| `area: security` | `#D73A4A` | Security related |
| `area: testing` | `#C5DEF5` | Testing coverage |
| `area: auth` | `#E99695` | Authentication |
| `area: billing` | `#F9D0C4` | Subscriptions/Credits |
| `area: ai` | `#BFD4F2` | AI/Agent framework |
| `area: marketplace` | `#D4C5F9` | Plugin marketplace |
| `area: docs` | `#0075CA` | Documentation |

#### Status
| Label | Color | Descripción |
|-------|-------|-------------|
| `status: blocked` | `#B60205` | Blocked by dependency |
| `status: ready` | `#0E8A16` | Ready for development |
| `status: in-progress` | `#FBCA04` | Currently being worked |
| `status: review` | `#5319E7` | In code review |

### 2. Milestones

| Milestone | Due Date | Descripción |
|-----------|----------|-------------|
| `Epic 0000: Stabilize MVP` | TBD | Security, tests, cleanup |
| `Epic 0001: Subscriptions` | TBD | Stripe + Credits |
| `Epic 0002: Auth0 Integration` | TBD | Auth migration |
| `Epic 0003: Agent Framework` | TBD | Plugin system |
| `Epic 0004: Marketplace` | TBD | Plugin store |

### 3. GitHub Project Board

Crear proyecto "Orchid Core Roadmap" con columnas:

```
| Backlog | Ready | In Progress | Review | Done |
```

---

## Plan de Ejecución

### Fase 1: Setup de GitHub (Hoy)

- [ ] **1.1** Crear todos los labels via `gh label create`
- [ ] **1.2** Crear los 5 milestones via `gh api`
- [ ] **1.3** Crear GitHub Project board

### Fase 2: Crear Epic Issues (Hoy)

Para cada epic crear un Issue "padre" con:

```markdown
## Epic: [Nombre]

**Phase:** X
**Priority:** PX
**Dependencies:** #issue, #issue

### Overview
[Resumen del epic]

### Goals
- [ ] Goal 1 - Metric
- [ ] Goal 2 - Metric

### Stories
- [ ] #XX REQ-XXXX-01: Nombre
- [ ] #XX REQ-XXXX-02: Nombre
...

### Success Metrics
| Metric | Target |
|--------|--------|
| ... | ... |
```

### Fase 3: Crear Story Issues (Hoy)

Para cada REQ-XXXX-XX crear un Issue con:

```markdown
## Story: [Nombre]

**Epic:** #XX
**Priority:** PX

### Description
[Descripción del requirement]

### Acceptance Criteria
- [ ] Criterio 1
- [ ] Criterio 2
- [ ] Criterio 3

### Technical Notes
[Notas técnicas si aplica]

### API Endpoints (si aplica)
```
GET /api/...
POST /api/...
```
```

### Fase 4: Establecer Relaciones

- [ ] **4.1** Vincular stories a epics (referencia en descripción)
- [ ] **4.2** Agregar issues al Project board
- [ ] **4.3** Marcar dependencias entre epics como bloqueadores

### Fase 5: Documentar Proceso

- [ ] **5.1** Crear template de issue para nuevas stories
- [ ] **5.2** Crear template de issue para bugs
- [ ] **5.3** Documentar flujo de trabajo en CONTRIBUTING.md

---

## Mapeo de Requirements a Issues

### Epic 0000: Stabilize MVP

| REQ ID | Título | Labels |
|--------|--------|--------|
| REQ-0000-01 | Security Audit and Fixes | `story`, `priority: P0`, `area: security` |
| REQ-0000-02 | Dead Code Elimination | `story`, `priority: P0`, `area: testing` |
| REQ-0000-03 | Test Coverage | `story`, `priority: P0`, `area: testing` |
| REQ-0000-04 | Pattern Consistency | `story`, `priority: P0`, `area: docs` |
| REQ-0000-05 | Documentation Update | `story`, `priority: P0`, `area: docs` |

### Epic 0001: Subscriptions

| REQ ID | Título | Labels |
|--------|--------|--------|
| REQ-0001-01 | Stripe Subscription Lifecycle | `story`, `priority: P1`, `area: billing` |
| REQ-0001-02 | Webhook Processing | `story`, `priority: P1`, `area: billing` |
| REQ-0001-03 | Credit System | `story`, `priority: P1`, `area: billing` |
| REQ-0001-04 | Billing Management API | `story`, `priority: P1`, `area: billing` |
| REQ-0001-05 | Admin Billing Tools | `story`, `priority: P1`, `area: billing` |

### Epic 0002: Auth0 Integration

| REQ ID | Título | Labels |
|--------|--------|--------|
| REQ-0002-01 | Auth0 Tenant Setup | `story`, `priority: P1`, `area: auth` |
| REQ-0002-02 | Backend JWT Validation | `story`, `priority: P1`, `area: auth` |
| REQ-0002-03 | User Synchronization | `story`, `priority: P1`, `area: auth` |
| REQ-0002-04 | User Migration | `story`, `priority: P1`, `area: auth` |
| REQ-0002-05 | MFA and Social Login | `story`, `priority: P1`, `area: auth` |
| REQ-0002-06 | Legacy Code Removal | `story`, `priority: P1`, `area: auth` |

### Epic 0003: Agent Framework

| REQ ID | Título | Labels |
|--------|--------|--------|
| REQ-0003-01 | Agent Abstraction Layer | `story`, `priority: P2`, `area: ai` |
| REQ-0003-02 | Plugin System | `story`, `priority: P2`, `area: ai` |
| REQ-0003-03 | Conversation Management | `story`, `priority: P2`, `area: ai` |
| REQ-0003-04 | Observability Integration | `story`, `priority: P2`, `area: ai` |
| REQ-0003-05 | Credit Integration | `story`, `priority: P2`, `area: ai`, `area: billing` |
| REQ-0003-06 | Migrate Existing Agents | `story`, `priority: P2`, `area: ai` |

### Epic 0004: Marketplace

| REQ ID | Título | Labels |
|--------|--------|--------|
| REQ-0004-01 | Plugin Catalog | `story`, `priority: P2`, `area: marketplace` |
| REQ-0004-02 | Plugin Installation | `story`, `priority: P2`, `area: marketplace` |
| REQ-0004-03 | Rating & Reviews | `story`, `priority: P2`, `area: marketplace` |
| REQ-0004-04 | Publisher System | `story`, `priority: P2`, `area: marketplace` |
| REQ-0004-05 | Plugin Versioning | `story`, `priority: P2`, `area: marketplace` |
| REQ-0004-06 | Usage Analytics | `story`, `priority: P2`, `area: marketplace` |

---

## Scripts de Automatización

### Script: Crear Labels

```bash
#!/bin/bash
# create-labels.sh

# Tipo
gh label create "epic" --color "3E4B9E" --description "Parent epic issue"
gh label create "story" --color "0E8A16" --description "User story / requirement"
gh label create "task" --color "C2E0C6" --description "Implementation task"
gh label create "bug" --color "D73A4A" --description "Bug fix"
gh label create "spike" --color "FBCA04" --description "Research/investigation"

# Prioridad
gh label create "priority: P0" --color "B60205" --description "Critical path"
gh label create "priority: P1" --color "D93F0B" --description "High priority"
gh label create "priority: P2" --color "FBCA04" --description "Medium priority"
gh label create "priority: P3" --color "0E8A16" --description "Low priority"

# Fase
gh label create "phase: 1-stabilize" --color "1D76DB" --description "Foundation work"
gh label create "phase: 2-complete" --color "5319E7" --description "Feature completion"
gh label create "phase: 3-extend" --color "006B75" --description "New capabilities"

# Area
gh label create "area: security" --color "D73A4A" --description "Security related"
gh label create "area: testing" --color "C5DEF5" --description "Testing coverage"
gh label create "area: auth" --color "E99695" --description "Authentication"
gh label create "area: billing" --color "F9D0C4" --description "Subscriptions/Credits"
gh label create "area: ai" --color "BFD4F2" --description "AI/Agent framework"
gh label create "area: marketplace" --color "D4C5F9" --description "Plugin marketplace"
gh label create "area: docs" --color "0075CA" --description "Documentation"

# Status
gh label create "status: blocked" --color "B60205" --description "Blocked by dependency"
gh label create "status: ready" --color "0E8A16" --description "Ready for development"
gh label create "status: in-progress" --color "FBCA04" --description "Currently being worked"
gh label create "status: review" --color "5319E7" --description "In code review"
```

### Script: Crear Milestones

```bash
#!/bin/bash
# create-milestones.sh

gh api repos/{owner}/{repo}/milestones -f title="Epic 0000: Stabilize MVP" -f description="Security, tests, cleanup - Phase 1" -f state="open"
gh api repos/{owner}/{repo}/milestones -f title="Epic 0001: Subscriptions" -f description="Stripe + Credits - Phase 2" -f state="open"
gh api repos/{owner}/{repo}/milestones -f title="Epic 0002: Auth0 Integration" -f description="Auth migration - Phase 2" -f state="open"
gh api repos/{owner}/{repo}/milestones -f title="Epic 0003: Agent Framework" -f description="Plugin system - Phase 3" -f state="open"
gh api repos/{owner}/{repo}/milestones -f title="Epic 0004: Marketplace" -f description="Plugin store - Phase 3" -f state="open"
```

---

## Siguiente Paso

Una vez aprobado este plan, ejecutar:

1. Crear labels en GitHub
2. Crear milestones
3. Crear issue para cada Epic (5 issues)
4. Crear issue para cada Story (28 issues)
5. Vincular todo al Project board

**Pregunta:** ¿Procedemos con la ejecución automática o prefieres revisar/ajustar el plan primero?

---

## Notas

- Los documentos de contexto (PRD, Architecture, Product Brief) permanecen en el repo como referencia
- Solo los items "ejecutables" (epics, stories, tasks) van a GitHub Issues
- El `epics.md` se puede archivar una vez migrado

---

## Resultado de la Migración (2026-01-15)

### Resumen

| Item | Cantidad | Estado |
|------|----------|--------|
| Labels creados | 21 | OK |
| Milestones creados | 5 | OK |
| Epic Issues | 5 | OK |
| Story Issues | 28 | OK |
| **Total Issues** | **33** | **OK** |

### Estructura de Issues

| Epic | Issue # | Stories |
|------|---------|---------|
| Epic 0000: Stabilize MVP | #3 | #8, #9, #10, #11, #12 |
| Epic 0001: Subscriptions | #4 | #13, #14, #15, #16, #17 |
| Epic 0002: Auth0 Integration | #5 | #18, #19, #20, #21, #22, #23 |
| Epic 0003: Agent Framework | #6 | #24, #25, #26, #27, #28, #29 |
| Epic 0004: Marketplace | #7 | #30, #31, #32, #33, #34, #35 |

### Status por Fase

| Fase | Issues | Estado |
|------|--------|--------|
| Phase 1: Stabilize (P0) | 6 issues | `status: ready` |
| Phase 2: Complete (P1) | 12 issues | `status: blocked` |
| Phase 3: Extend (P2) | 15 issues | `status: blocked` |

### Links

- [Project Board](https://github.com/users/pablitxn/projects/10)
- [Issues](https://github.com/pablitxn/orchid-core/issues)
- [Milestones](https://github.com/pablitxn/orchid-core/milestones)
- [Labels](https://github.com/pablitxn/orchid-core/labels)

### Próximos Pasos

1. ~~Crear GitHub Project board y agregar todos los issues~~ DONE
2. Comenzar a trabajar en Epic 0000 (las 5 stories están `status: ready`)
3. Una vez cerrado Epic 0000, cambiar status de Epic 0001 y 0002 a `ready`

