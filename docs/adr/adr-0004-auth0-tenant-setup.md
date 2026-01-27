# ADR-0004: Auth0 Tenant Setup

**Status:** Accepted
**Date:** 2026-01-27
**Authors:** Pablo
**Related Issue:** [#18](https://github.com/pablitxn/orchid-core/issues/18)
**Epic:** [Epic 0002 - Auth0 Integration](../_bmad-output/planning-artifacts/epic-0002-auth0-integration.md)

## Context

Orchid Core currently uses a custom JWT authentication system with:
- Manual token generation and validation (symmetric HMAC-SHA256)
- Custom refresh token rotation
- PBKDF2 password hashing
- Basic role-based authorization
- No MFA support
- Limited social login (Google only)

As part of Epic 0002 (Auth0 Integration), we need to set up Auth0 tenants with proper configuration for security, scalability, and enterprise features.

## Decision

We will configure Auth0 tenants using **Auth0 Deploy CLI** (YAML-based) instead of Terraform, aligning with our existing infrastructure management approach:

1. **Kustomize + ArgoCD** for Kubernetes resources
2. **Auth0 Deploy CLI** for Auth0 SaaS configuration
3. **SealedSecrets** for credential management

### Why Auth0 Deploy CLI over Terraform?

| Aspect | Auth0 Deploy CLI | Terraform |
|--------|------------------|-----------|
| Format | YAML (familiar) | HCL (new language) |
| Learning curve | Low | Medium |
| GitOps fit | Native YAML | Requires state management |
| State management | Stateless (declarative) | Requires remote state |
| Existing usage | None in org | None in org |

Since we don't use Terraform anywhere in our infrastructure, introducing it solely for Auth0 would add unnecessary complexity.

## Configuration Structure

```
playground-infrastructure/
└── auth0/
    ├── README.md              # Setup documentation
    ├── config.json            # Deploy CLI config
    ├── tenant.yaml            # Tenant settings
    ├── resource-servers.yaml  # API definitions
    ├── clients.yaml           # Applications
    ├── roles.yaml             # RBAC roles
    └── branding.yaml          # UI customization
```

## Auth0 Resources

### Tenants

| Environment | Tenant | Purpose |
|-------------|--------|---------|
| Development | `orchid-core-dev` | Development and staging |
| Production | `orchid-core-prod` | Production environment |

### API (Resource Server)

- **Identifier:** `https://api.orchid-core.com`
- **Signing Algorithm:** RS256 (asymmetric)
- **Token Lifetime:** 4 hours (access), 7 days (refresh)

### Scopes

| Scope | Description |
|-------|-------------|
| `read:agents` | Read agent data |
| `write:agents` | Create/modify agents |
| `read:knowledge` | Read knowledge base |
| `write:knowledge` | Modify knowledge base |
| `read:profile` | Read user profile |
| `write:profile` | Update profile |
| `read:chats` | Read chat sessions |
| `write:chats` | Manage chats |
| `read:plugins` | Read plugins |
| `write:plugins` | Manage plugins |
| `read:workflows` | Read workflows |
| `write:workflows` | Manage workflows |
| `admin` | Full administrative access |

### Applications

| Application | Type | Auth Method |
|-------------|------|-------------|
| Orchid Web App | SPA | PKCE (no secret) |
| Orchid Backend M2M | M2M | Client credentials |
| Auth0 Deploy CLI | M2M | Client credentials |

### Roles

| Role | Target Users | Key Permissions |
|------|--------------|-----------------|
| `user` | Free tier | Basic CRUD |
| `premium_user` | Paid subscribers | + plugins, workflows |
| `admin` | Administrators | All + admin scope |

## Security Hardening

The configuration implements these security measures:

1. **Token Security**
   - RS256 signing (asymmetric keys via JWKS)
   - Short-lived access tokens (4h)
   - Rotating refresh tokens with 1-day idle timeout

2. **Session Security**
   - 72-hour idle session lifetime
   - 168-hour maximum session lifetime
   - SSO enabled for seamless experience

3. **Application Security**
   - PKCE required for SPA (no client secrets exposed)
   - Strict callback URL validation
   - Clickjack protection enabled

4. **Future: MFA**
   - Will be required for admin role (Phase 2)
   - Optional for other users

## Secrets Management

Auth0 credentials are stored as SealedSecrets in Kubernetes:

```yaml
# Future: orchid-core sealed secret additions
Auth0__Domain: "orchid-core-prod.us.auth0.com"
Auth0__Audience: "https://api.orchid-core.com"
Auth0__ClientId: "<m2m-client-id>"
Auth0__ClientSecret: "<m2m-client-secret>"
```

## Consequences

### Positive

- **Declarative configuration** versioned in Git
- **No Terraform state** to manage
- **Familiar YAML format** for the team
- **Easy rollback** via Git history
- **CI/CD ready** for automated deployments

### Negative

- **Manual tenant creation** (Deploy CLI can't create tenants)
- **No drift detection** built-in (unlike Terraform plan)
- **Limited validation** compared to Terraform

### Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Accidental deletion | `AUTH0_ALLOW_DELETE: false` in config |
| Secret exposure | All secrets via env vars, never in YAML |
| Config drift | Regular exports to verify state |

## Implementation Checklist

- [x] Create configuration files in `playground-infrastructure/auth0/`
- [x] Document setup process in README
- [ ] Create Auth0 tenants manually (dev + prod)
- [ ] Create M2M app for Deploy CLI in each tenant
- [ ] Test import on dev tenant
- [ ] Set up CI/CD for automated deployments
- [ ] Add sealed secrets for Auth0 credentials

## Related ADRs

- **ADR-0005** (future): Auth0 JWT Validation - Backend integration
- **ADR-0006** (future): User Migration Strategy

## References

- [Auth0 Deploy CLI Documentation](https://auth0.com/docs/deploy-monitor/deploy-cli-tool)
- [Auth0 YAML Format](https://auth0.com/docs/deploy-monitor/deploy-cli-tool/yaml-format)
- [PKCE for SPAs](https://auth0.com/docs/get-started/authentication-and-authorization-flow/authorization-code-flow-with-proof-key-for-code-exchange-pkce)
