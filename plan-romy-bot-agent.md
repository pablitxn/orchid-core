# Plan de Integracion: romy-bot como plugin y agente en orchid-core

Fecha: 2026-01-30

## Objetivo
Integrar romy-bot como plugin OpenAPI en orchid-core, permitir su creacion desde el frontend, y crear un nuevo agente que lo use de forma predeterminada o seleccionable.

## Alcance
- Backend (orchid-core): registro de plugin OpenAPI romy-bot, uso en agentes, validacion de spec.
- Frontend (orchid-webapp): UI para crear plugins OpenAPI y agregar romy-bot a un nuevo agente.
- Servicio externo (romy-bot): asegurar OpenAPI disponible y accesible por orchid-core.

## Dependencias
- romy-bot expuesto con OpenAPI en `GET /openapi.json` (FastAPI).
- Acceso admin para usar `POST /api/admin/openapi-plugins`.
- Endpoint de compra para habilitar uso: `POST /api/plugin/{id}/purchase`.

## Plan de trabajo
### 1) Backend: plugin OpenAPI romy-bot
- Validar que `AdminOpenApiPluginsController` cubra todos los campos necesarios (systemName, baseUrl, openApiSpecUrl, timeout, precio).
- Crear plugin OpenAPI con:
  - name: "Romy Bot"
  - systemName: "romy_bot"
  - baseUrl: URL del servicio romy-bot
  - openApiSpecUrl: "/openapi.json"
  - pluginType: "openapi" (via endpoint admin)
  - isActive: true
- Verificar cache de spec y funciones generadas (OpenApiToolFactory).

### 2) Backend: agente nuevo con romy-bot
- Opcion A (seed): actualizar `AgentSeeder` para crear un agente "Romy" y asociar el plugin si existe (buscar por `SystemName == "romy_bot"`).
- Opcion B (manual): crear agente via API (`POST /api/agents`) con `pluginIds` incluyendo el plugin romy-bot.
- Revisar si se debe exigir propiedad del plugin en `CreateAgentHandler` (hoy no valida ownership).

### 3) Frontend: creacion de plugin OpenAPI
- Ampliar `src/pages/plugins/new.tsx` para soportar tipo OpenAPI:
  - campos: systemName, baseUrl, openApiSpecUrl, timeoutSeconds, priceCredits, isSubscriptionBased, isActive.
  - opcion de validar spec (endpoint `POST /api/admin/openapi-plugins/validate`).
  - envio de datos a `POST /api/admin/openapi-plugins`.
- Mantener compatibilidad con plugin "simple" actual (`POST /api/plugin`) si se necesita.

### 4) Frontend: agente con romy-bot
- Asegurar que el plugin romy-bot aparezca en `availablePlugins` (activo + userOwns).
- Si el creador necesita acceso inmediato, definir flujo:
  - comprar plugin despues de crearlo, o
  - auto-asignar ownership al creador (si se implementa en backend).
- Crear un agente desde UI con `pluginIds` que incluya romy-bot.

## Validacion
- Backend
  - `POST /api/admin/openapi-plugins/validate` con baseUrl romy-bot.
  - `POST /api/admin/openapi-plugins` crea plugin.
  - `GET /api/admin/openapi-plugins/{id}` devuelve funciones.
  - `POST /api/plugin/{id}/purchase` permite agregar al agente.
  - `POST /api/agents` con `pluginIds` incluye romy-bot.
- Frontend
  - Formulario de plugin OpenAPI crea romy-bot.
  - Plugin aparece como disponible cuando el usuario lo posee.
  - Agente nuevo muestra romy-bot en sus plugins.
- Integracion
  - Chat con agente invoca funciones del plugin y se ve actividad `tool_invocation`.

## Riesgos / pendientes
- Ownership del plugin en `CreateAgentHandler` no se valida (posible inconsistencia con `AddPluginToAgent`).
- Acceso admin requerido para crear plugins OpenAPI.
- Entorno de red: orchid-core debe alcanzar `baseUrl` de romy-bot (Docker/networking).

## Preguntas abiertas
- Preferimos seed automatico del agente "Romy" o creacion manual desde UI?
- El creador del plugin debe quedar como owner automaticamente?
- Cual es la URL base definitiva de romy-bot en dev/staging/prod?
