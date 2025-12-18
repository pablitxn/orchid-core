# Orchid Labs Core

**Orchid Labs Core** is a full‑stack sandbox demonstrating Hexagonal architecture with .NET, RabbitMQ and a React
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

## Security

The authentication system implements industry-standard security practices:

### Authentication & Authorization
| Feature | Description |
|---------|-------------|
| **JWT Validation** | Full signature, issuer, audience, and expiration validation |
| **PBKDF2 Password Hashing** | 100,000 iterations with SHA-256 and random salt |
| **Timing-Attack Protection** | Constant-time password comparison via `CryptographicOperations.FixedTimeEquals` |

### Protection Mechanisms
| Mechanism | Configuration |
|-----------|---------------|
| **Rate Limiting** | Login: 5 attempts/15min, Register: 3/hour, Password Reset: 3/hour |
| **Account Lockout** | Temporary lock after 5 failed attempts within 15 minutes |
| **Login History** | All authentication attempts logged with IP, User-Agent, and timestamp |

### Cookie Security
| Flag | Value |
|------|-------|
| `HttpOnly` | `true` - prevents XSS token theft |
| `Secure` | `true` in production - HTTPS only |
| `SameSite` | `Strict` - prevents CSRF attacks |

### Input Validation
- Email format validation on registration
- Password complexity requirements: 8+ characters, uppercase, lowercase, digit, special character
- Generic error messages to prevent user enumeration

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

## Deployment

This section covers deploying Orchid Core to a Kubernetes cluster using ArgoCD and GitHub Container Registry.

### Architecture Overview

```text
┌─────────────┐     push      ┌──────────────┐     pull      ┌─────────────┐
│   GitHub    │ ───────────► │    GHCR      │ ◄─────────── │   ArgoCD    │
│   Actions   │               │   Registry   │               │   (k8s)     │
└─────────────┘               └──────────────────────────────└─────────────┘
      │                                                            │
      └── updates kustomization.yaml ──────────────────────────────┘
```

### 1. Prerequisites

#### Cluster Requirements
- Kubernetes 1.28+
- ArgoCD installed and configured
- Ingress controller (nginx-ingress or traefik)
- cert-manager (optional, for TLS)

#### External Services
| Service | Purpose | Required |
|---------|---------|----------|
| PostgreSQL 16+ with pgvector | Main database | Yes |
| Redis 7+ | Cache & session storage | Yes |
| RabbitMQ 3+ | Message broker for workers | Optional |
| Langfuse | LLM telemetry & tracing | Optional |
| S3-compatible storage | File uploads (Minio, AWS S3) | Production |

### 2. GitHub Container Registry Setup

The image is automatically published to `ghcr.io/pablitxn/orchid-core` on push to `main` or `dev`:

- `main` branch → `ghcr.io/pablitxn/orchid-core:prod`
- `dev` branch → `ghcr.io/pablitxn/orchid-core:dev`

The workflow updates `infrastructure/overlays/{env}/kustomization.yaml` with the new image digest.

### 3. Kubernetes Resources

#### 3.1 Create Namespace

```bash
kubectl create namespace orchid-core
```

#### 3.2 Create Secrets

Create a secret with all required configuration:

```bash
kubectl create secret generic orchid-core-secrets \
  --namespace orchid-core \
  --from-literal=ConnectionStrings__DefaultConnection="Host=postgres;Port=5432;Database=orchid_core;Username=admin;Password=<DB_PASSWORD>" \
  --from-literal=OpenAI__ApiKey="<OPENAI_API_KEY>" \
  --from-literal=OpenAI__Endpoint="https://api.openai.com/v1/chat/completions" \
  --from-literal=OpenAI__Model="gpt-4o" \
  --from-literal=Jwt__Key="<JWT_SECRET_KEY_MIN_32_CHARS>" \
  --from-literal=Jwt__Issuer="orchid-core" \
  --from-literal=Jwt__Audience="orchid-core" \
  --from-literal=Jwt__ExpireHours="72" \
  --from-literal=Redis__Configuration="redis:6379,password=<REDIS_PASSWORD>" \
  --from-literal=Langfuse__BaseUrl="https://langfuse.example.com/api/public" \
  --from-literal=Langfuse__PublicKey="<LANGFUSE_PUBLIC_KEY>" \
  --from-literal=Langfuse__SecretKey="<LANGFUSE_SECRET_KEY>" \
  --from-literal=Langfuse__Enabled="true"
```

#### 3.3 Create ConfigMap

```bash
kubectl create configmap orchid-core-config \
  --namespace orchid-core \
  --from-literal=ASPNETCORE_ENVIRONMENT="Production" \
  --from-literal=FrontendUrl="https://app.example.com" \
  --from-literal=AllowedHosts="*" \
  --from-literal=FileStorage__Provider="S3" \
  --from-literal=FileStorage__Bucket="orchid-uploads" \
  --from-literal=FileStorage__Path="uploads"
```

### 4. Database Setup

#### 4.1 PostgreSQL with pgvector

Deploy PostgreSQL with pgvector extension enabled:

```yaml
# Example using Bitnami PostgreSQL Helm chart
helm install postgres bitnami/postgresql \
  --namespace orchid-core \
  --set auth.postgresPassword=<POSTGRES_PASSWORD> \
  --set auth.database=orchid_core \
  --set primary.extendedConfiguration="shared_preload_libraries='vector'"
```

Or use a managed service (AWS RDS, Azure Database, etc.) with pgvector extension.

#### 4.2 Apply Migrations

Run migrations from a pod or locally with cluster access:

```bash
# Option 1: From local machine with kubectl port-forward
kubectl port-forward svc/postgres 5432:5432 -n orchid-core
dotnet ef database update --project services/backend/src/Adapters/Infrastructure

# Option 2: Using a Job
kubectl apply -f - <<EOF
apiVersion: batch/v1
kind: Job
metadata:
  name: orchid-migrations
  namespace: orchid-core
spec:
  template:
    spec:
      containers:
      - name: migrations
        image: ghcr.io/pablitxn/orchid-core:prod
        command: ["dotnet", "ef", "database", "update"]
        envFrom:
        - secretRef:
            name: orchid-core-secrets
      restartPolicy: Never
EOF
```

### 5. ArgoCD Application

#### 5.1 Private Infrastructure Repository

Create a private repo with your base Kustomize configuration:

```text
infrastructure-private/
├── base/
│   ├── deployment.yaml
│   ├── service.yaml
│   ├── ingress.yaml
│   └── kustomization.yaml
└── overlays/
    ├── dev/
    │   └── kustomization.yaml
    └── prod/
        └── kustomization.yaml
```

Example `base/deployment.yaml`:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: orchid-core
spec:
  replicas: 2
  selector:
    matchLabels:
      app: orchid-core
  template:
    metadata:
      labels:
        app: orchid-core
    spec:
      containers:
      - name: orchid-core
        image: ghcr.io/pablitxn/orchid-core:prod
        ports:
        - containerPort: 8080
        envFrom:
        - secretRef:
            name: orchid-core-secrets
        - configMapRef:
            name: orchid-core-config
        resources:
          requests:
            memory: "256Mi"
            cpu: "100m"
          limits:
            memory: "1Gi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 30
        readinessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 10
```

#### 5.2 ArgoCD Multi-Source Application

This repo provides image tags while your private repo has the base configuration:

```yaml
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: orchid-core
  namespace: argocd
spec:
  project: default
  sources:
  # Source 1: Private infrastructure repo (base config)
  - repoURL: https://github.com/your-org/infrastructure-private.git
    targetRevision: main
    path: overlays/prod
  # Source 2: This repo (image tags only)
  - repoURL: https://github.com/pablitxn/orchid-core.git
    targetRevision: main
    path: infrastructure/overlays/prod
  destination:
    server: https://kubernetes.default.svc
    namespace: orchid-core
  syncPolicy:
    automated:
      prune: true
      selfHeal: true
```

### 6. Environment Variables Reference

| Variable | Description | Default | Required |
|----------|-------------|---------|----------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string | - | Yes |
| `OpenAI__ApiKey` | OpenAI API key | - | Yes |
| `OpenAI__Endpoint` | OpenAI API endpoint | `https://api.openai.com/v1/chat/completions` | No |
| `OpenAI__Model` | Model to use | `gpt-4o` | No |
| `Jwt__Key` | JWT signing key (min 32 chars) | - | Yes |
| `Jwt__Issuer` | JWT issuer | `playground` | No |
| `Jwt__Audience` | JWT audience | `playground` | No |
| `Jwt__ExpireHours` | Token expiration in hours | `72` | No |
| `Redis__Configuration` | Redis connection string | - | Yes |
| `Redis__InstanceName` | Redis key prefix | `ChatHistory:` | No |
| `FrontendUrl` | CORS allowed origin | - | Yes |
| `Langfuse__Enabled` | Enable Langfuse telemetry | `false` | No |
| `Langfuse__BaseUrl` | Langfuse API URL | - | If enabled |
| `Langfuse__PublicKey` | Langfuse public key | - | If enabled |
| `Langfuse__SecretKey` | Langfuse secret key | - | If enabled |
| `FileStorage__Provider` | `Local` or `S3` | `Local` | No |
| `FileStorage__Bucket` | S3 bucket name | - | If S3 |
| `FileStorage__Path` | Upload path prefix | `data` | No |
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | `Production` | No |

### 7. Verifying the Deployment

```bash
# Check pods are running
kubectl get pods -n orchid-core

# Check logs
kubectl logs -f deployment/orchid-core -n orchid-core

# Test health endpoint
kubectl port-forward svc/orchid-core 8080:8080 -n orchid-core
curl http://localhost:8080/health
```

### 8. Scaling & High Availability

```yaml
# HPA for auto-scaling
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: orchid-core-hpa
  namespace: orchid-core
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: orchid-core
  minReplicas: 2
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
```

### 9. Troubleshooting

| Issue | Solution |
|-------|----------|
| Pod crashes on startup | Check `ConnectionStrings__DefaultConnection` format and DB connectivity |
| 401 Unauthorized | Verify `Jwt__Key` matches between API and clients |
| Redis connection failed | Ensure Redis password in connection string matches |
| File uploads fail | Check `FileStorage__Provider` and S3 credentials |
| AI features not working | Verify `OpenAI__ApiKey` is set correctly |

---

## Further Reading

* [Architecture](docs/architecture.md)
* [Frontend Guidelines](docs/frontend.md)
* [ADR Index](docs/addr)

Contributions follow the guidelines in [AGENTS.md](AGENTS.md). Pull requests should run the CI script and update the
[CHANGELOG](CHANGELOG.md).
