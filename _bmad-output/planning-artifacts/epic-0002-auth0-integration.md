---
epicId: '0002'
title: 'Auth0 Integration'
phase: 2
priority: P1
masterPrd: prd.md
status: draft
estimatedEffort: '3 weeks'
dependencies:
  - epic-0000
---

# Epic PRD: 0002 - Auth0 Integration

**Parent:** [Master PRD](./prd.md)
**Phase:** 2 - Complete
**Priority:** P1 (High)
**Author:** Pablo
**Date:** 2026-01-04

---

## Overview

Migrate from the current custom JWT authentication system to Auth0 for improved security, reduced maintenance burden, and access to enterprise-grade identity features. This is a critical security improvement that enables future features like SSO, MFA, and social login.

### Current State

Based on codebase analysis:
- Custom JWT generation and validation
- Manual refresh token rotation
- Password hashing with bcrypt
- Basic role-based authorization
- No MFA support
- No social login

### Target State

- Auth0 as sole identity provider
- Seamless token validation
- MFA enabled for users
- Social login options available
- Enterprise SSO ready
- Reduced auth-related code to maintain

---

## Goals

| Goal | Success Metric |
|------|----------------|
| Migrate to Auth0 | All auth flows use Auth0 |
| Zero downtime migration | No user re-registration required |
| Security improvement | MFA available for all users |
| Reduced complexity | 50% less auth code to maintain |
| Enterprise ready | SSO integration possible |

---

## Scope

### In Scope

1. **Auth0 Setup**
   - Tenant configuration
   - Application registration (backend API, frontend SPA)
   - API audience configuration
   - Role and permission setup
   - Branding customization

2. **Backend Integration**
   - JWT validation with Auth0 JWKS
   - User synchronization (Auth0 → local DB)
   - Role/permission mapping
   - API authorization middleware update
   - Refresh token handling

3. **User Migration**
   - Export existing users (without passwords)
   - Import to Auth0 with password migration
   - Automatic password migration on first login
   - Fallback for migration edge cases

4. **New Auth Features**
   - MFA enrollment flow
   - Social login (Google, GitHub)
   - Password reset via Auth0
   - Email verification via Auth0

5. **Cleanup**
   - Remove custom JWT generation
   - Remove password storage
   - Remove custom refresh token logic
   - Update all auth-related tests

### Out of Scope

- Frontend auth UI changes (separate PRD)
- Enterprise SSO configuration (future epic)
- Custom Auth0 rules/actions (keep simple)
- User management admin panel (use Auth0 dashboard)

---

## Requirements

### REQ-0002-01: Auth0 Tenant Setup

**Description:** Configure Auth0 tenant with appropriate security settings.

**Acceptance Criteria:**
- [ ] Auth0 tenant created (production + staging)
- [ ] API registered with appropriate scopes
- [ ] SPA application registered with PKCE
- [ ] Backend machine-to-machine app for management API
- [ ] Roles defined matching current system
- [ ] Branding matches application theme
- [ ] Security settings hardened (token expiration, etc.)

**Technical Notes:**
- Use Auth0 Terraform provider for infrastructure as code
- Store tenant config in source control (except secrets)

**Configuration:**
```
Auth0 Tenant: orchid-core-[env]
API Identifier: https://api.orchid-core.com
Scopes: read:agents, write:agents, read:knowledge, write:knowledge, admin
Roles: user, premium_user, admin
```

---

### REQ-0002-02: Backend JWT Validation

**Description:** Replace custom JWT validation with Auth0 JWKS validation.

**Acceptance Criteria:**
- [ ] JWT validation uses Auth0 JWKS endpoint
- [ ] Token audience and issuer validated
- [ ] Claims extracted and mapped to user context
- [ ] Invalid tokens rejected with 401
- [ ] Expired tokens rejected with 401
- [ ] JWKS cached with automatic refresh

**Technical Notes:**
- Use Microsoft.AspNetCore.Authentication.JwtBearer
- Configure JWKS caching to avoid constant fetches
- Support both old and new tokens during migration

**Code Pattern:**
```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://{auth0Domain}/";
        options.Audience = auth0Audience;
    });
```

---

### REQ-0002-03: User Synchronization

**Description:** Keep local user records in sync with Auth0.

**Acceptance Criteria:**
- [ ] Auth0 user ID stored in local user record
- [ ] First login creates local user if not exists
- [ ] Profile updates sync to local DB (via webhook or on-demand)
- [ ] User deletion handled (soft delete local)
- [ ] Roles/permissions sync from Auth0

**Technical Notes:**
- Use Auth0 Actions for post-login user sync
- Store `auth0_id` in Users table
- Keep email in sync for notifications

**Domain Events:**
- `UserCreatedFromAuth0`
- `UserProfileSynced`
- `UserRolesUpdated`

---

### REQ-0002-04: User Migration

**Description:** Migrate existing users to Auth0 without requiring password reset.

**Acceptance Criteria:**
- [ ] All existing users imported to Auth0
- [ ] Users can login with existing credentials
- [ ] Password automatically migrated to Auth0 on first login
- [ ] Migration status tracked per user
- [ ] Fallback: password reset for failed migrations

**Technical Notes:**
- Use Auth0 Custom Database Connection for lazy migration
- Implement login script that validates against old DB
- Implement get_user script for existing user lookup
- After migration, disable custom DB connection

**Migration Flow:**
```
1. User logs in with email/password
2. Auth0 calls custom login script
3. Script validates against old bcrypt hash
4. If valid, Auth0 creates user and migrates password
5. Local user record updated with auth0_id
6. Future logins go directly to Auth0
```

---

### REQ-0002-05: MFA and Social Login

**Description:** Enable optional MFA and social login providers.

**Acceptance Criteria:**
- [ ] MFA (TOTP) available for all users
- [ ] MFA can be enforced per role (e.g., admins required)
- [ ] Google social login enabled
- [ ] GitHub social login enabled
- [ ] Social login users get local user record
- [ ] Account linking for same email

**Technical Notes:**
- Enable Guardian MFA in Auth0
- Configure social connections in Auth0 dashboard
- Handle new user creation from social login

---

### REQ-0002-06: Legacy Code Removal

**Description:** Remove all custom authentication code after migration.

**Acceptance Criteria:**
- [ ] Custom JWT generation removed
- [ ] Password hashing code removed
- [ ] Refresh token management removed
- [ ] Custom login/register endpoints deprecated
- [ ] Auth-related tables archived (keep for audit)
- [ ] Tests updated for new auth flow

**Technical Notes:**
- Keep old code behind feature flag during transition
- Document removed code in ADR
- Archive but don't delete user password data immediately

---

## Technical Design Considerations

### Architecture Changes

**Before:**
```
Client → WebApi (JWT validation) → Custom AuthService → Database (users, passwords)
```

**After:**
```
Client → Auth0 → WebApi (Auth0 JWT validation) → UserSyncService → Database (users, no passwords)
```

### Data Model Updates

```sql
-- Add to Users table
ALTER TABLE Users ADD COLUMN auth0_id VARCHAR(255) UNIQUE;
ALTER TABLE Users ADD COLUMN auth_provider VARCHAR(50) DEFAULT 'auth0';
ALTER TABLE Users ADD COLUMN migration_status VARCHAR(20) DEFAULT 'pending';

-- Archive password data (don't delete immediately)
CREATE TABLE archived_user_credentials (
    user_id UUID PRIMARY KEY,
    password_hash VARCHAR(255),
    archived_at TIMESTAMP DEFAULT NOW()
);
```

### Configuration

```json
{
  "Auth0": {
    "Domain": "orchid-core.auth0.com",
    "Audience": "https://api.orchid-core.com",
    "ClientId": "xxx",
    "ClientSecret": "xxx (from secrets)",
    "ManagementApiClientId": "xxx",
    "ManagementApiClientSecret": "xxx (from secrets)"
  }
}
```

### API Changes

**Deprecated Endpoints (remove after migration):**
```
POST /api/auth/register     → Auth0 Universal Login
POST /api/auth/login        → Auth0 Universal Login
POST /api/auth/refresh      → Auth0 handles
POST /api/auth/logout       → Auth0 logout
POST /api/auth/forgot-password → Auth0 handles
```

**New/Updated Endpoints:**
```
GET  /api/auth/me           → Returns user from Auth0 token
POST /api/auth/sync-profile → Manual profile sync trigger
```

---

## Dependencies

### External
- Auth0 service (tenant, APIs)
- Auth0 Management API access

### Internal
- Epic 0000 must be complete (stable codebase)
- Frontend coordination for login UI changes

### Blocked By This
- Epic 0003 (Agent Framework) - needs reliable user identity
- Epic 0004 (Marketplace) - needs permissions system

---

## Migration Strategy

### Phase 1: Parallel Auth (Week 1-2)
- Deploy Auth0 integration alongside existing auth
- Feature flag to switch between systems
- Test with internal users

### Phase 2: User Import (Week 2)
- Import existing users to Auth0 (without passwords)
- Enable custom database connection for password migration
- Test login with migrated users

### Phase 3: Cutover (Week 3)
- Switch all traffic to Auth0
- Monitor for auth failures
- Keep fallback available for 1 week

### Phase 4: Cleanup (Week 3+)
- Remove legacy auth code
- Archive password data
- Disable custom database connection

### Rollback Plan
- Feature flag to revert to old auth
- Old JWT validation code available for 30 days
- Password data archived (not deleted) for recovery

---

## Success Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| Auth success rate | 99.9% | Auth0 logs |
| Migration completion | 100% users | Migration status tracking |
| Login latency | < 500ms p95 | Monitoring |
| Auth code reduction | 50% | Lines of code |

---

## Resolved Questions

| Question | Resolution | Date |
|----------|------------|------|
| What social login providers should be enabled initially? | **Google + GitHub** enabled at launch. | 2026-01-15 |
| Should MFA be required for admin users from day one? | **Yes, MFA required** for admin users from day one. | 2026-01-15 |
| What's the timeline to delete archived password data? | **6 months** retention post-migration, then delete. | 2026-01-15 |
| Should we allow username login or email only? | **Email only** for login identifier. | 2026-01-15 |
| How to handle users who registered but never verified email? | **Migrate and require verification** on first Auth0 login. | 2026-01-15 |

---

## Security Considerations

- Auth0 secrets stored in secret manager (not env vars)
- Webhook signatures validated
- Management API credentials rotated regularly
- Audit logs enabled in Auth0
- Password history enforced in Auth0

---

*This is an Epic PRD. See [Master PRD](./prd.md) for overall project context.*
