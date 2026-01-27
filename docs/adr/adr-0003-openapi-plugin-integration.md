# ADR-0003: OpenAPI Plugin Integration for Agent Framework

**Date:** 2026-01-27
**Status:** Implemented
**Author:** Engineering Team
**Last Updated:** 2026-01-27

---

## Context

The Agent Framework needed a way to dynamically extend AI agent capabilities beyond the builtin tools (vector_store, web_search). Users and administrators should be able to add custom API integrations without requiring code changes or redeployment.

### Requirements

1. **Dynamic Tool Registration**: Add new tools at runtime via database configuration
2. **Standard API Description**: Use a widely-adopted specification format
3. **Caching & Performance**: Minimize API spec fetches while keeping tools current
4. **Seamless Integration**: Work alongside existing builtin tools without breaking changes
5. **Security**: Validate specs, handle errors gracefully, respect timeout limits

---

## Decision

Implement an **OpenAPI 3.0 plugin system** that:

1. Stores plugin configurations in the `PluginEntity` with OpenAPI-specific fields
2. Uses `OpenApiTool` to dynamically expose spec operations as `AIFunction` instances
3. Employs `OpenApiToolFactory` for spec fetching, parsing, and caching
4. Extends `ToolRegistry` with async resolution to support database-backed plugins

### Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                        AI Agent                                  │
│                                                                  │
│   Requests tool: "my_custom_api"                                │
└───────────────────────────┬─────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                      ToolRegistry                                │
│                                                                  │
│   1. Check builtin tools (fast sync path)                       │
│   2. If not found, query IPluginRepository (async path)         │
│   3. If OpenAPI plugin found, delegate to IOpenApiToolFactory   │
└───────────────────────────┬─────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                   OpenApiToolFactory                             │
│                                                                  │
│   1. Check spec cache validity (1-hour TTL)                     │
│   2. Fetch spec from BaseUrl + OpenApiSpecUrl if needed         │
│   3. Parse OpenAPI document                                     │
│   4. Update cache in database                                   │
│   5. Return OpenApiTool instance                                │
└───────────────────────────┬─────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                       OpenApiTool                                │
│                                                                  │
│   - Implements ITool interface                                  │
│   - Exposes each spec operation as AIFunction                   │
│   - Executes HTTP requests to target API                        │
│   - Publishes activity events for frontend                      │
└─────────────────────────────────────────────────────────────────┘
```

### Key Components

| Component | Location | Responsibility |
|-----------|----------|----------------|
| `PluginEntity` | Domain/Entities | Stores plugin config including OpenAPI fields |
| `OpenApiTool` | Infrastructure/Ai/Tools | Dynamic tool from OpenAPI spec |
| `OpenApiToolFactory` | Infrastructure/Ai/Tools | Creates tools with caching |
| `ToolRegistry` | Infrastructure/Ai/Services | Unified tool resolution |
| `AdminOpenApiPluginsController` | WebApi/Controllers | Admin CRUD endpoints |

### Database Schema

The `PluginEntity` includes the following OpenAPI-specific fields:

```csharp
public class PluginEntity
{
    // Existing fields...
    public string? PluginType { get; set; }        // "openapi" or "builtin"
    public string? BaseUrl { get; set; }           // API base URL
    public string? OpenApiSpecUrl { get; set; }    // Path to spec (default: /openapi.json)
    public string? OpenApiSpecCache { get; set; }  // Cached spec JSON
    public DateTime? SpecCachedAt { get; set; }    // Cache timestamp
    public int? TimeoutSeconds { get; set; }       // HTTP timeout (default: 30)
}
```

### Caching Strategy

1. **TTL-based caching**: Specs cached for 1 hour by default
2. **Stale fallback**: On fetch failure, uses expired cache if available
3. **Force refresh**: Admin endpoint to manually refresh spec
4. **Cache storage**: Persisted in database for cross-instance consistency

### Extension Points

1. **Custom spec URLs**: Configure `OpenApiSpecUrl` per plugin
2. **Custom timeouts**: Set `TimeoutSeconds` per plugin
3. **Credit pricing**: Set `PriceCredits` for tool invocations
4. **Subscription gating**: Set `IsSubscriptionBased` flag

---

## Consequences

### Benefits

1. **Extensibility**: Add new API integrations without code deployment
2. **Standard format**: OpenAPI 3.0 is widely supported and documented
3. **Performance**: Spec caching reduces external API calls
4. **Graceful degradation**: Uses expired cache on fetch failures
5. **Observability**: Activity events for tool invocations

### Trade-offs

1. **Complexity**: Additional database queries for plugin resolution
2. **Latency**: First invocation may require spec fetch
3. **Spec limitations**: Only supports standard OpenAPI operations
4. **Security surface**: External specs must be validated

### Mitigations

1. **Builtin priority**: Sync path for builtins avoids DB queries
2. **Aggressive caching**: 1-hour TTL reduces fetch frequency
3. **Timeout limits**: Configurable per-plugin timeouts
4. **Error handling**: Graceful fallbacks on all failure paths

---

## Test Coverage

### Unit Tests

| File | Coverage |
|------|----------|
| `OpenApiToolTests.cs` | 16 test cases |
| `OpenApiToolFactoryTests.cs` | 21 test cases |
| `ToolRegistryTests.cs` | 31 test cases |

### Integration Tests

| File | Scope |
|------|-------|
| `OpenApiPluginIntegrationTests.cs` | E2E with WireMock |

### Test Scenarios

1. **Happy path**: Register → Configure → Invoke
2. **Caching**: Valid cache, expired cache, empty cache
3. **Error handling**: Server unavailable, invalid spec
4. **Tool resolution**: Builtin priority, inactive plugins

---

## API Reference

### Admin Endpoints

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/api/admin/openapi-plugins` | List all plugins |
| GET | `/api/admin/openapi-plugins/{id}` | Get plugin + functions |
| POST | `/api/admin/openapi-plugins` | Create new plugin |
| PUT | `/api/admin/openapi-plugins/{id}` | Update plugin |
| DELETE | `/api/admin/openapi-plugins/{id}` | Delete plugin |
| POST | `/api/admin/openapi-plugins/{id}/refresh` | Force cache refresh |
| POST | `/api/admin/openapi-plugins/validate` | Validate spec URL |

### Example: Create Plugin

```http
POST /api/admin/openapi-plugins
Content-Type: application/json

{
  "name": "Weather API",
  "systemName": "weather_api",
  "description": "Real-time weather data",
  "baseUrl": "https://api.weather.com",
  "openApiSpecUrl": "/openapi.json",
  "priceCredits": 2,
  "isSubscriptionBased": false,
  "isActive": true
}
```

---

## Sample OpenAPI Spec

```yaml
openapi: 3.0.0
info:
  title: Sample Plugin
  version: 1.0.0
paths:
  /analyze:
    post:
      operationId: analyzeData
      summary: Analyze input data
      requestBody:
        content:
          application/json:
            schema:
              type: object
              properties:
                text:
                  type: string
      responses:
        '200':
          description: Analysis result
  /status:
    get:
      operationId: getStatus
      summary: Get service status
      responses:
        '200':
          description: Status response
```

---

## Files Reference

### Implementation

| File | Purpose |
|------|---------|
| `Domain/Entities/PluginEntity.cs` | Plugin entity with OpenAPI fields |
| `Infrastructure/Ai/Tools/OpenApiTool.cs` | Dynamic tool implementation |
| `Infrastructure/Ai/Tools/OpenApiToolFactory.cs` | Factory with caching |
| `Infrastructure/Ai/Services/ToolRegistry.cs` | Unified tool resolution |
| `Infrastructure/Ai/Abstractions/ITool.cs` | Tool interface |
| `Infrastructure/Ai/Abstractions/IOpenApiToolFactory.cs` | Factory interface |
| `Infrastructure/Ai/Abstractions/IToolRegistry.cs` | Registry interface |
| `WebApi/Controllers/AdminOpenApiPluginsController.cs` | Admin API |

### Application Layer

| File | Purpose |
|------|---------|
| `UseCases/OpenApiPlugin/CreateOpenApiPlugin/*` | Create use case |
| `UseCases/OpenApiPlugin/UpdateOpenApiPlugin/*` | Update use case |
| `UseCases/OpenApiPlugin/DeleteOpenApiPlugin/*` | Delete use case |
| `UseCases/OpenApiPlugin/ListOpenApiPlugins/*` | List query |
| `UseCases/OpenApiPlugin/GetOpenApiPlugin/*` | Get query |
| `UseCases/OpenApiPlugin/RefreshOpenApiPluginSpec/*` | Refresh spec |
| `UseCases/OpenApiPlugin/ValidateOpenApiSpec/*` | Validate spec |

### Tests

| File | Purpose |
|------|---------|
| `tests/Unit/.../OpenApiToolTests.cs` | Tool unit tests |
| `tests/Unit/.../OpenApiToolFactoryTests.cs` | Factory unit tests |
| `tests/Unit/.../ToolRegistryTests.cs` | Registry unit tests |
| `tests/Integration/.../OpenApiPluginIntegrationTests.cs` | E2E tests |

---

## Changelog

### 2026-01-27 - Initial Implementation

- Added `PluginEntity` OpenAPI fields
- Implemented `OpenApiTool` for dynamic operations
- Implemented `OpenApiToolFactory` with caching
- Extended `ToolRegistry` with async resolution
- Added admin API endpoints
- Added comprehensive test coverage

---

## Future Considerations

1. **Authentication**: Support API key and OAuth for external APIs
2. **Rate limiting**: Per-plugin rate limits
3. **Webhooks**: Callback support for long-running operations
4. **Metrics**: Track tool usage and error rates
5. **Versioning**: Support multiple spec versions per plugin
