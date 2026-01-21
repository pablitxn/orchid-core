# Observabilidad con Prometheus

Guía completa del sistema de métricas Prometheus para Orchid Core.

---

## Tabla de Contenidos

1. [Arquitectura](#arquitectura)
2. [Métricas Disponibles](#métricas-disponibles)
3. [Cómo Funciona](#cómo-funciona)
4. [Uso del Dashboard](#uso-del-dashboard)
5. [Agregar Nuevas Métricas](#agregar-nuevas-métricas)
6. [Constraints y Limitaciones](#constraints-y-limitaciones)
7. [Troubleshooting](#troubleshooting)
8. [Mejoras Futuras](#mejoras-futuras)

---

## Arquitectura

### Diagrama de Flujo

```
┌─────────────────────────────────────────────────────────────────────────┐
│                              Kubernetes                                  │
│                                                                         │
│   ┌──────────────────────────────┐    ┌──────────────────────────────┐  │
│   │     orchid-core-prod         │    │       infrastructure         │  │
│   │                              │    │                              │  │
│   │  ┌────────────────────────┐  │    │  ┌────────────────────────┐  │  │
│   │  │    orchid-core Pod     │  │    │  │      Prometheus        │  │  │
│   │  │                        │  │    │  │                        │  │  │
│   │  │  ┌──────────────────┐  │  │    │  │  - Scrape interval: 30s│  │  │
│   │  │  │  ASP.NET Core    │  │  │    │  │  - Retention: 15 days  │  │  │
│   │  │  │                  │  │  │    │  │                        │  │  │
│   │  │  │ GET /metrics ────┼──┼──┼────┼──┼─► ServiceMonitor       │  │  │
│   │  │  │ (prometheus-net) │  │  │    │  │                        │  │  │
│   │  │  └──────────────────┘  │  │    │  └───────────┬────────────┘  │  │
│   │  │                        │  │    │              │               │  │
│   │  └────────────────────────┘  │    │              ▼               │  │
│   │                              │    │  ┌────────────────────────┐  │  │
│   │  ┌────────────────────────┐  │    │  │       Grafana          │  │  │
│   │  │       Service          │  │    │  │                        │  │  │
│   │  │  - http: 80            │  │    │  │  Dashboard: Orchid     │  │  │
│   │  │  - metrics: 9090 ──────┼──┼────┼──┼─► Core Metrics         │  │  │
│   │  └────────────────────────┘  │    │  │                        │  │  │
│   │                              │    │  └────────────────────────┘  │  │
│   └──────────────────────────────┘    └──────────────────────────────┘  │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### Componentes

| Componente | Ubicación | Propósito |
|------------|-----------|-----------|
| **prometheus-net** | Backend | Librería que expone métricas en formato Prometheus |
| **ApplicationMetrics.cs** | Infrastructure | Definición centralizada de todas las métricas |
| **ServiceMonitor** | K8s CRD | Configura Prometheus para scrapear el backend |
| **Grafana Dashboard** | ConfigMap | Visualización de métricas |

### Archivos Clave

```
orchid-core/
├── services/backend/src/
│   └── Adapters/Infrastructure/Telemetry/
│       └── ApplicationMetrics.cs          # Definiciones de métricas
│
└── docs/
    └── observability-prometheus.md        # Esta documentación

playground-infrastructure/
└── hostinger/applications/
    ├── orchid-core/base/
    │   ├── service.yaml                   # Puerto metrics:9090
    │   └── servicemonitor.yaml            # Config de scraping
    │
    └── monitoring/grafana/dashboards/
        └── orchid-core-metrics.yaml       # Dashboard Grafana
```

---

## Métricas Disponibles

### Chat & Messaging

| Métrica | Tipo | Labels | Descripción |
|---------|------|--------|-------------|
| `app_chat_messages_total` | Counter | `agent_id`, `status` | Total de mensajes procesados |
| `app_chat_message_duration_seconds` | Histogram | `agent_id` | Duración del procesamiento |

**Status values:** `success`, `invalid_session_id`, `empty_message`, `message_too_long`, `user_not_recognized`, `rate_limit_exceeded`, `subscription_not_found`, `insufficient_credits`, `session_not_found`, `access_denied`, `no_agent_selected`, `agent_access_denied`, `ai_timeout`, `ai_error`, `cancelled`

### AI Operations

| Métrica | Tipo | Labels | Descripción |
|---------|------|--------|-------------|
| `app_ai_operations_total` | Counter | `operation_type`, `model`, `status` | Total de operaciones AI |
| `app_ai_operation_duration_seconds` | Histogram | `operation_type`, `model` | Latencia de operaciones AI |

**Operation types:** `chat_completion`, `agent_completion`, `streaming_completion`, `agent_streaming`

### Tool Invocations

| Métrica | Tipo | Labels | Descripción |
|---------|------|--------|-------------|
| `app_tool_invocations_total` | Counter | `tool_name`, `status` | Total de invocaciones de tools |
| `app_tool_execution_duration_seconds` | Histogram | `tool_name` | Duración de ejecución |

### Credits

| Métrica | Tipo | Labels | Descripción |
|---------|------|--------|-------------|
| `app_credits_consumed_total` | Counter | `operation_type`, `tool_name` | Créditos consumidos |

**Operation types:** `plugin`, `workflow`, `message`

### Knowledge Base

| Métrica | Tipo | Labels | Descripción |
|---------|------|--------|-------------|
| `app_embeddings_total` | Counter | `status` | Operaciones de embedding |
| `app_vector_searches_total` | Counter | `status` | Búsquedas vectoriales |
| `app_vector_search_duration_seconds` | Histogram | - | Latencia de búsqueda |

### Users

| Métrica | Tipo | Labels | Descripción |
|---------|------|--------|-------------|
| `app_user_registrations_total` | Counter | `provider` | Registros de usuarios |

**Providers:** `direct`, `google`

---

## Cómo Funciona

### 1. Exposición de Métricas (Backend)

```csharp
// Program.cs - Middleware y endpoint
app.UseHttpMetrics(options =>
{
    options.AddCustomLabel("host", context => context.Request.Host.Host);
});

app.MapMetrics();  // Expone GET /metrics
```

### 2. Instrumentación en Código

```csharp
// Ejemplo: Contador
ApplicationMetrics.ChatMessagesTotal
    .WithLabels(agentId, "success")
    .Inc();

// Ejemplo: Histograma con timer
using var timer = ApplicationMetrics.ChatMessageDuration
    .WithLabels(agentId)
    .NewTimer();
// ... código a medir ...
// timer se dispone automáticamente y registra la duración

// Ejemplo: Incrementar por cantidad
ApplicationMetrics.CreditsConsumedTotal
    .WithLabels("plugin", pluginName)
    .Inc(credits);  // Incrementa por 'credits' cantidad
```

### 3. Scraping (Prometheus)

El `ServiceMonitor` configura Prometheus para:

```yaml
endpoints:
  - port: metrics
    path: /metrics
    interval: 30s        # Frecuencia de scraping
    scrapeTimeout: 10s   # Timeout por request
```

### 4. Formato de Salida

```
# HELP app_chat_messages_total Total number of chat messages processed
# TYPE app_chat_messages_total counter
app_chat_messages_total{agent_id="abc-123",status="success"} 42
app_chat_messages_total{agent_id="abc-123",status="ai_error"} 3

# HELP app_chat_message_duration_seconds Duration of chat message processing
# TYPE app_chat_message_duration_seconds histogram
app_chat_message_duration_seconds_bucket{agent_id="abc-123",le="0.1"} 5
app_chat_message_duration_seconds_bucket{agent_id="abc-123",le="0.25"} 15
app_chat_message_duration_seconds_bucket{agent_id="abc-123",le="0.5"} 30
app_chat_message_duration_seconds_bucket{agent_id="abc-123",le="+Inf"} 42
app_chat_message_duration_seconds_sum{agent_id="abc-123"} 156.7
app_chat_message_duration_seconds_count{agent_id="abc-123"} 42
```

---

## Uso del Dashboard

### Acceso

- **URL:** https://grafana.orchid-labs.xyz
- **Dashboard:** "Orchid Core - Backend Metrics"
- **UID:** `orchid-core-metrics`

### Secciones del Dashboard

#### 1. Chat & Messaging
- **Messages (24h):** Total de mensajes en las últimas 24 horas
- **Chat Error Rate %:** Porcentaje de errores vs total
- **Chat Latency P95:** Percentil 95 de latencia
- **Chat Messages by Agent:** Timeline por agente y status
- **Chat Message Latency Distribution:** P50, P95, P99

#### 2. AI Operations
- **AI Operations (24h):** Total de operaciones AI
- **AI Latency P95:** Latencia de operaciones AI
- **AI Operations by Type:** Streaming vs non-streaming, etc.
- **AI Operations by Model:** Distribución por modelo (gpt-4, etc.)

#### 3. Tool Invocations
- **Tool Invocations (24h):** Total de invocaciones
- **Tool Latency P95:** Latencia de tools
- **Tool Invocations by Tool:** Por nombre de tool
- **Top 10 Tools:** Las herramientas más usadas

#### 4. Credits & Billing
- **Credits Consumed (24h):** Total de créditos
- **Credit Consumption by Type:** plugin/workflow/message
- **Top Credit Consumers:** Tools que más créditos consumen

#### 5. Knowledge Base
- **Embeddings (24h):** Operaciones de embedding
- **Vector Searches (24h):** Búsquedas realizadas
- **Search Latency P95:** Latencia de búsqueda

#### 6. Users
- **New Users (24h):** Registros nuevos
- **User Registrations by Provider:** direct vs google

### Queries Útiles (PromQL)

```promql
# Tasa de mensajes por minuto
sum(rate(app_chat_messages_total[1m])) * 60

# Error rate de chat
sum(rate(app_chat_messages_total{status!="success"}[5m]))
/ sum(rate(app_chat_messages_total[5m])) * 100

# Latencia P99 de AI operations
histogram_quantile(0.99,
  sum(rate(app_ai_operation_duration_seconds_bucket[5m])) by (le, operation_type)
)

# Top 5 tools por invocaciones
topk(5, sum(increase(app_tool_invocations_total[24h])) by (tool_name))

# Créditos consumidos por hora
sum(increase(app_credits_consumed_total[1h]))
```

---

## Agregar Nuevas Métricas

### Paso 1: Definir la Métrica

```csharp
// ApplicationMetrics.cs
public static readonly Counter MyNewMetric = Metrics.CreateCounter(
    "app_my_feature_total",           // Nombre (siempre con prefijo app_)
    "Description of what this measures",
    new CounterConfiguration
    {
        LabelNames = ["label1", "label2"]
    });

// Para histogramas
public static readonly Histogram MyDurationMetric = Metrics.CreateHistogram(
    "app_my_feature_duration_seconds",
    "Duration of my feature in seconds",
    new HistogramConfiguration
    {
        LabelNames = ["label1"],
        Buckets = [0.01, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10]
    });
```

### Paso 2: Instrumentar en el Código

```csharp
// En el servicio/handler correspondiente
using Infrastructure.Telemetry;

// Counter
ApplicationMetrics.MyNewMetric.WithLabels("value1", "value2").Inc();

// Histogram
using var timer = ApplicationMetrics.MyDurationMetric.WithLabels("value1").NewTimer();
// ... código a medir ...
```

### Paso 3: Agregar al Dashboard (Opcional)

Editar `orchid-core-metrics.yaml` y agregar un nuevo panel:

```json
{
  "datasource": { "type": "prometheus", "uid": "prometheus" },
  "gridPos": { "h": 4, "w": 6, "x": 0, "y": 0 },
  "id": 99,
  "targets": [{
    "expr": "sum(rate(app_my_feature_total[5m]))",
    "refId": "A"
  }],
  "title": "My Feature Rate",
  "type": "stat"
}
```

### Convención de Nombres

| Patrón | Uso | Ejemplo |
|--------|-----|---------|
| `app_{domain}_{metric}_total` | Counters | `app_chat_messages_total` |
| `app_{domain}_{metric}_seconds` | Histogramas de duración | `app_chat_message_duration_seconds` |
| `app_{domain}_{metric}` | Gauges | `app_active_users` |

---

## Constraints y Limitaciones

### Cardinalidad de Labels

**CRÍTICO:** Evitar labels de alta cardinalidad.

```csharp
// MAL - userId tiene cardinalidad ilimitada
.WithLabels(userId, status)  // ❌ Miles de series temporales

// BIEN - status tiene cardinalidad limitada
.WithLabels(agentId, status) // ✅ Cardinalidad controlada
```

**Regla:** Un label no debería tener más de ~100-1000 valores únicos.

### Labels Prohibidos

| Label | Razón | Alternativa |
|-------|-------|-------------|
| `user_id` | Cardinalidad ilimitada | Usar logs/traces |
| `request_id` | Cardinalidad ilimitada | Usar tracing |
| `timestamp` | Redundante | Prometheus ya lo tiene |
| `session_id` | Cardinalidad alta | Agregar a metadatos de trace |

### Retención de Datos

- **Prometheus retention:** 15 días
- **Sin persistencia:** Los datos se pierden si el pod reinicia
- **Recomendación:** Para históricos largos, usar Thanos o Cortex

### Límites de Recursos

```yaml
# Prometheus resources (kube-prometheus-stack)
resources:
  requests:
    cpu: 200m
    memory: 512Mi
  limits:
    cpu: 1000m
    memory: 2Gi
```

### Scrape Interval

- **Actual:** 30 segundos
- **Trade-off:** Menor intervalo = más resolución pero más carga
- **No bajar de:** 15 segundos sin justificación

### Métricas HTTP Automáticas

`prometheus-net.AspNetCore` ya expone métricas HTTP automáticamente:

```
http_requests_received_total
http_request_duration_seconds
```

No duplicar estas métricas manualmente.

---

## Troubleshooting

### Prometheus no puede scrapear metricas (Host Filtering)

**Sintoma:** Prometheus targets muestran estado `DOWN` con error "connection refused" o "host header rejected".

**Causa:** ASP.NET Core `HostFilteringMiddleware` rechaza requests con IPs de pod como Host header.

**Solucion:** El proyecto ya maneja esto (ver ADR-0002):
- `AllowedHosts: "*"` esta permitido en produccion
- La seguridad esta garantizada por NetworkPolicies en Kubernetes
- El puerto 9090 solo es accesible dentro del cluster

**Verificar configuracion:**
```bash
# Verificar que el pod responde
kubectl exec -n infrastructure prometheus-... -- \
  wget -qO- http://<pod-ip>:9090/metrics | head -5

# Verificar NetworkPolicy
kubectl get networkpolicy -n orchid-core-prod
```

### Metricas no aparecen en Prometheus

1. **Verificar endpoint local:**
   ```bash
   curl http://localhost:5000/metrics | grep app_
   ```

2. **Verificar ServiceMonitor:**
   ```bash
   kubectl get servicemonitor -n orchid-core-prod
   kubectl describe servicemonitor orchid-core -n orchid-core-prod
   ```

3. **Verificar targets en Prometheus:**
   - Ir a Prometheus UI → Status → Targets
   - Buscar `orchid-core`

4. **Verificar labels del Service:**
   ```bash
   kubectl get svc orchid-core -n orchid-core-prod -o yaml
   ```

### Dashboard no carga datos

1. **Verificar datasource:**
   - Grafana → Configuration → Data sources → Prometheus → Test

2. **Probar query directamente:**
   - Grafana → Explore → Prometheus
   - Ejecutar: `app_chat_messages_total`

3. **Verificar tiempo:**
   - Asegurar que el rango de tiempo incluye datos

### Métricas con valores incorrectos

1. **Verificar instrumentación:**
   - ¿Se está llamando `.Inc()` correctamente?
   - ¿Los labels coinciden con la definición?

2. **Counter vs Gauge:**
   - Counter: Solo incrementa (usar `Inc()`)
   - Gauge: Puede subir/bajar (usar `Set()`, `Inc()`, `Dec()`)

### Alta cardinalidad detectada

Síntomas:
- Prometheus usa mucha memoria
- Queries lentas
- Errores de "too many time series"

Solución:
```bash
# Encontrar métricas de alta cardinalidad
curl -s http://prometheus:9090/api/v1/status/tsdb | jq '.data.seriesCountByMetricName | to_entries | sort_by(-.value) | .[0:10]'
```

---

## Mejoras Futuras

### Fase 2: Métricas Pendientes

| Métrica | Descripción | Complejidad |
|---------|-------------|-------------|
| `app_ai_tokens_total` | Tokens consumidos por modelo | Media - Requiere parsing de response |
| `app_credit_balance` | Balance actual de créditos | Media - Requiere background job |
| `app_active_users` | Usuarios activos | Alta - Requiere tracking de sesiones |
| `app_workflow_executions_total` | Ejecuciones de workflow | Baja - Cuando se implemente workflows |

### Alerting

Agregar PrometheusRules para alertas:

```yaml
apiVersion: monitoring.coreos.com/v1
kind: PrometheusRule
metadata:
  name: orchid-core-alerts
spec:
  groups:
    - name: orchid-core
      rules:
        - alert: HighChatErrorRate
          expr: |
            sum(rate(app_chat_messages_total{status!="success"}[5m]))
            / sum(rate(app_chat_messages_total[5m])) > 0.1
          for: 5m
          labels:
            severity: warning
          annotations:
            summary: "High chat error rate detected"

        - alert: HighAILatency
          expr: |
            histogram_quantile(0.95,
              sum(rate(app_ai_operation_duration_seconds_bucket[5m])) by (le)
            ) > 60
          for: 5m
          labels:
            severity: warning
          annotations:
            summary: "AI operations taking too long"
```

### Recording Rules

Para queries pesadas, usar recording rules:

```yaml
apiVersion: monitoring.coreos.com/v1
kind: PrometheusRule
metadata:
  name: orchid-core-recording
spec:
  groups:
    - name: orchid-core-recording
      interval: 1m
      rules:
        - record: orchid:chat_error_rate:5m
          expr: |
            sum(rate(app_chat_messages_total{status!="success"}[5m]))
            / sum(rate(app_chat_messages_total[5m]))

        - record: orchid:ai_latency_p95:5m
          expr: |
            histogram_quantile(0.95,
              sum(rate(app_ai_operation_duration_seconds_bucket[5m])) by (le)
            )
```

### Integración con Tracing

Correlacionar métricas con traces usando exemplars:

```csharp
// Futuro: Agregar trace_id como exemplar
ApplicationMetrics.ChatMessageDuration
    .WithLabels(agentId)
    .Observe(duration, Exemplar.FromTraceId(traceId));
```

### Long-term Storage

Para retención > 15 días, considerar:

1. **Thanos:** Almacenamiento S3 + deduplicación
2. **Cortex:** Multi-tenant, horizontalmente escalable
3. **VictoriaMetrics:** Drop-in replacement, más eficiente

---

## Referencias

- [prometheus-net Documentation](https://github.com/prometheus-net/prometheus-net)
- [Prometheus Best Practices](https://prometheus.io/docs/practices/naming/)
- [Grafana Dashboard Best Practices](https://grafana.com/docs/grafana/latest/dashboards/build-dashboards/best-practices/)
- [kube-prometheus-stack](https://github.com/prometheus-community/helm-charts/tree/main/charts/kube-prometheus-stack)
