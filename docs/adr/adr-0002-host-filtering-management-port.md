# ADR-0002: Deshabilitar validacion estricta de AllowedHosts para puerto de management

**Date:** 2025-01-21
**Status:** Accepted
**Author:** Engineering Team
**Related Issue:** #81

---

## Context

Prometheus no puede scrapear el endpoint `/metrics` en produccion porque:

1. Prometheus utiliza la **IP del pod** (ej: `10.244.0.222`) para realizar el scraping
2. ASP.NET Core tiene `HostFilteringMiddleware` que valida el header `Host`
3. `AllowedHosts` en ASP.NET Core solo soporta:
   - `*` (cualquier host)
   - Hosts exactos (ej: `example.com;api.example.com`)
   - **NO soporta**: wildcards parciales (`10.244.*`) ni CIDR notation (`10.244.0.0/16`)
4. El codigo actual (linea 452-456 de `Program.cs`) lanza una excepcion si `AllowedHosts` es `*` o esta vacio en produccion

```csharp
// Codigo actual que bloquea el despliegue
if (!env.IsDevelopment() && (string.IsNullOrEmpty(allowedHosts) || allowedHosts == "*"))
{
    throw new InvalidOperationException(
        "AllowedHosts must be explicitly configured in production. " +
        "Set 'AllowedHosts' to your domain(s), e.g., 'example.com;api.example.com'.");
}
```

### Problema

El flujo de Prometheus en Kubernetes es:

```
Prometheus Pod ──(HTTP)──> Pod IP:9090/metrics
                           │
                           └── Host header: "10.244.0.222:9090"
                               │
                               └── HostFilteringMiddleware: REJECT (IP no en AllowedHosts)
```

Como las IPs de pods son dinamicas y cambian con cada reschedule, no es viable listarlas en `AllowedHosts`.

---

## Decision

Cambiar la validacion de `AllowedHosts` de **excepcion bloqueante** a **warning informativo** cuando el valor es `*`.

### Justificacion

La seguridad no depende exclusivamente de `HostFilteringMiddleware`. El sistema implementa **defensa en profundidad** con multiples capas:

| Capa | Proteccion | Alcance |
|------|------------|---------|
| **NetworkPolicies (k8s)** | Solo pods autorizados pueden conectarse al puerto 9090 | Cluster-level |
| **Puerto separado (9090)** | No expuesto a internet, solo accesible dentro del cluster | Network-level |
| **Traefik con SSO** | Todo trafico externo requiere autenticacion Google | Ingress-level |
| **JWT Middleware** | Endpoints de API (puerto 8080) requieren token valido | Application-level |

El `HostFilteringMiddleware` fue disenado para escenarios donde un servidor atiende multiples dominios y quiere prevenir ataques de DNS rebinding. En nuestro caso:

- El puerto 8080 (API publica) tiene dominios conocidos configurados
- El puerto 9090 (management) solo es accesible internamente via NetworkPolicies
- Bloquear `*` impide el correcto funcionamiento de observability sin aportar seguridad adicional

---

## Implementation

### Cambio en `Program.cs`

```csharp
// ANTES (bloqueante):
if (!env.IsDevelopment() && (string.IsNullOrEmpty(allowedHosts) || allowedHosts == "*"))
{
    throw new InvalidOperationException(
        "AllowedHosts must be explicitly configured in production.");
}

// DESPUES (warning):
if (!env.IsDevelopment() && (string.IsNullOrEmpty(allowedHosts) || allowedHosts == "*"))
{
    Log.Warning(
        "AllowedHosts is set to '{AllowedHosts}'. " +
        "Ensure NetworkPolicies restrict access to management port (9090). " +
        "See ADR-0002 for security considerations",
        allowedHosts ?? "(empty)");
}
```

### Configuracion recomendada

```yaml
# appsettings.production.json (via env vars)
AllowedHosts: "*"

# NetworkPolicy (ya existente en k8s manifests)
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: allow-prometheus-scraping
spec:
  podSelector:
    matchLabels:
      app: orchid-core
  ingress:
    - from:
        - namespaceSelector:
            matchLabels:
              name: infrastructure
          podSelector:
            matchLabels:
              app: prometheus
      ports:
        - port: 9090
```

---

## Alternatives Considered

### Alternativa 1: Deshabilitar HostFilteringMiddleware selectivamente por puerto

```csharp
app.UseWhen(
    context => context.Connection.LocalPort != managementPort,
    appBuilder => appBuilder.UseHostFiltering()
);
```

**Descartada porque:**
- `HostFilteringMiddleware` se registra automaticamente por el framework antes de `Build()`
- Requiere configuracion adicional en `WebHostBuilder` para deshabilitarlo globalmente primero
- Agrega complejidad innecesaria cuando NetworkPolicies ya proveen la proteccion necesaria

### Alternativa 2: Listar todas las IPs posibles del cluster

```json
{
  "AllowedHosts": "10.244.0.0;10.244.0.1;...;example.com"
}
```

**Descartada porque:**
- IPs de pods son dinamicas
- CIDR no es soportado
- Imposible de mantener

### Alternativa 3: Proxy reverso interno para Prometheus

Configurar un servicio interno que traduzca requests de Prometheus a un host conocido.

**Descartada porque:**
- Agrega latencia y punto de falla
- Overengineering para un problema resuelto por NetworkPolicies

---

## Consequences

### Positivas

- Prometheus puede scrapear metricas correctamente
- Health checks de Kubernetes funcionan sin modificaciones
- Observability stack (Grafana, alertas) operativo
- Configuracion mas simple sin hacks de networking

### Negativas

- El warning en logs puede generar ruido inicial (mitigado con documentacion)
- Requiere que NetworkPolicies esten correctamente configuradas (responsabilidad de infraestructura)

### Riesgos mitigados

| Riesgo | Mitigacion |
|--------|------------|
| Pod malicioso accede a `/metrics` | NetworkPolicy restringe origen |
| Puerto 9090 expuesto a internet | Ingress solo rutea al 8080, 9090 es ClusterIP |
| DNS rebinding attack | Trafico externo pasa por Traefik con validacion de dominio |

---

## Verification

Despues del deploy, verificar que Prometheus puede scrapear:

```bash
# Desde dentro del cluster (pod de debugging):
kubectl run curl --rm -it --image=curlimages/curl -- \
  curl -s http://<pod-ip>:9090/metrics | head -5

# Output esperado:
# # HELP process_cpu_seconds_total Total user and system CPU time spent in seconds.
# # TYPE process_cpu_seconds_total counter
# process_cpu_seconds_total 123.45
```

En Grafana, verificar que las metricas de `orchid_core` aparecen correctamente.

---

## References

- [ASP.NET Core Host Filtering Middleware](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/host-filtering)
- [Kubernetes NetworkPolicies](https://kubernetes.io/docs/concepts/services-networking/network-policies/)
- [OWASP DNS Rebinding](https://owasp.org/www-community/attacks/DNS_Rebinding)
- Issue #81: Deshabilitar HostFiltering para puerto de management
