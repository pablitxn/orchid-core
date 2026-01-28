# ADR-0005: Auth0 User Synchronization

**Status:** Accepted
**Date:** 2026-01-28
**Authors:** Claude (AI-assisted)
**Related Issue:** [#20](https://github.com/pablitxn/orchid-core/issues/20)
**Epic:** [Epic 0002 - Auth0 Integration](../_bmad-output/planning-artifacts/epic-0002-auth0-integration.md)

## Context

With Auth0 integration (ADR-0004), we need to synchronize user data between Auth0 and our local database. This includes:

1. Creating local user records when new Auth0 users authenticate
2. Linking existing legacy users to their Auth0 accounts
3. Keeping profile data (name, avatar) in sync
4. Synchronizing roles and permissions

### Requirements from Issue #20

- Auth0 user ID stored in local user record
- First login creates local user if not exists
- Profile updates sync to local DB (via webhook)
- User deletion handled (soft delete local)
- Roles/permissions sync from Auth0

## Decision

We will implement a **webhook-based synchronization strategy** with the following design:

### 1. User Entity Extension

Add Auth0-specific fields to `UserEntity`:

```csharp
public string? Auth0UserId { get; set; }      // Auth0 sub claim
public string? Auth0Connection { get; set; }  // google-oauth2, auth0, etc.
public DateTime? Auth0LastSyncedAt { get; set; }
```

### 2. Synchronization Strategy

**Webhook-based sync via Auth0 Post-Login Action:**

- Auth0 triggers webhook after each successful login
- Backend receives user data and syncs to local DB
- Non-blocking to avoid affecting login latency

**Why webhooks over on-login-only?**

| Aspect | Webhooks | On-Login Only |
|--------|----------|---------------|
| Real-time sync | Yes | No |
| Profile updates between logins | Captured | Missed |
| Complexity | Higher | Lower |
| Auth0 dependency | Action required | None |

Since the user chose webhooks, we get more reliable sync at the cost of webhook configuration.

### 3. User Linking Strategy

**Auto-link by email (user's choice):**

When an Auth0 user logs in:
1. Check if user exists by Auth0UserId (already linked)
2. If not, check if user exists by email (link existing)
3. If neither, create new user

```
Auth0 Login → Webhook → Find by Auth0ID → Found? → Update
                                     ↓ Not Found
                          Find by Email → Found? → Link
                                     ↓ Not Found
                              Create New User
```

**Security consideration:** Email matching assumes Auth0 has verified the email. Auth0 provides `email_verified` claim which should be checked in production.

### 4. Domain Events

New events for observability and downstream processing:

- `UserCreatedFromAuth0Event` - Triggers onboarding (welcome credits, plugins)
- `Auth0UserLinkedEvent` - Existing user linked to Auth0
- `UserProfileSyncedEvent` - Profile data updated
- `UserRolesUpdatedEvent` - Roles changed

### 5. Webhook Security

HMAC-SHA256 signature validation:

```csharp
// Constant-time comparison to prevent timing attacks
CryptographicOperations.FixedTimeEquals(expected, provided);
```

Configuration via `Auth0:WebhookSecret` environment variable.

## Architecture

### Component Diagram

```
┌─────────────┐     POST /api/webhooks/auth0/post-login
│   Auth0     │ ─────────────────────────────────────────┐
│  (Action)   │                                          │
└─────────────┘                                          ▼
                                              ┌─────────────────────┐
                                              │ Auth0WebhookController│
                                              └──────────┬──────────┘
                                                         │
                                              ┌──────────▼──────────┐
                                              │ SyncAuth0UserHandler │
                                              └──────────┬──────────┘
                                                         │
                    ┌────────────────────────────────────┼────────────────────────────────────┐
                    │                                    │                                    │
           ┌────────▼────────┐              ┌───────────▼───────────┐           ┌───────────▼───────────┐
           │  UserRepository  │              │   RoleRepository      │           │   EventPublisher      │
           └─────────────────┘              └───────────────────────┘           └───────────────────────┘
```

### Hexagonal Compliance

| Layer | Component | File |
|-------|-----------|------|
| Domain | UserEntity fields | `Domain/Entities/UserEntity.cs` |
| Domain | Events | `Domain/Events/UserCreated*.cs` |
| Application | Port | `Application/Interfaces/IAuth0SyncService.cs` |
| Application | Handler | `Application/UseCases/Auth/SyncAuth0User/` |
| Infrastructure | Adapter | `Infrastructure/Auth/Auth0SyncService.cs` |
| WebApi | Controller | `WebApi/Controllers/Auth0WebhookController.cs` |

## Implementation Details

### Database Changes

New columns on `Users` table:
- `Auth0UserId` VARCHAR(255) NULLABLE, UNIQUE INDEX (filtered)
- `Auth0Connection` VARCHAR(100) NULLABLE
- `Auth0LastSyncedAt` TIMESTAMP WITH TIME ZONE NULLABLE

### Sync Logic Pseudocode

```
function SyncAuth0User(auth0Data):
    email = normalize(auth0Data.email)

    // Try find by Auth0 ID (already linked)
    user = FindByAuth0UserId(auth0Data.sub)

    if user is null:
        // Try find by email (link existing)
        user = FindByEmail(email)

        if user is not null:
            // Link existing user
            user.Auth0UserId = auth0Data.sub
            user.Auth0Connection = auth0Data.connection
            emit Auth0UserLinkedEvent
        else:
            // Create new user
            user = CreateUser(auth0Data)
            emit UserCreatedFromAuth0Event

    // Sync profile if changed
    if ProfileChanged(user, auth0Data):
        UpdateProfile(user, auth0Data)
        emit UserProfileSyncedEvent

    // Sync roles if provided
    if RolesChanged(user, auth0Data.roles):
        SyncRoles(user, auth0Data.roles)
        emit UserRolesUpdatedEvent

    return user
```

## Security Considerations

1. **Webhook authentication** - HMAC-SHA256 with constant-time comparison
2. **Email normalization** - Lowercase to prevent case-sensitivity attacks
3. **Role validation** - Only assign roles that exist locally
4. **No PII logging** - Avoid logging email addresses or user data
5. **Idempotency** - Multiple syncs for same user are safe

## Testing Strategy

### Unit Tests
- `SyncAuth0UserHandlerTests` - All sync scenarios
- `Auth0WebhookControllerTests` - Webhook handling

### Scenarios Covered
1. New user creation
2. Existing user linking by email
3. Already linked user update
4. Profile sync (name, avatar)
5. Role sync (add/remove)
6. Signature validation

## Configuration

### Auth0 Action Setup

Create a Post-Login Action in Auth0:

```javascript
exports.onExecutePostLogin = async (event, api) => {
  const axios = require('axios');

  await axios.post('https://api.orchid-core.com/api/webhooks/auth0/post-login', {
    user: {
      userId: event.user.user_id,
      email: event.user.email,
      name: event.user.name,
      picture: event.user.picture,
      permissions: event.authorization?.permissions || [],
      identities: event.user.identities
    }
  }, {
    headers: {
      'X-Auth0-Signature': computeHmacSha256(payload, secrets.WEBHOOK_SECRET)
    }
  });
};
```

### Environment Variables

```
Auth0__WebhookSecret=<random-32-byte-hex>
```

## Consequences

### Positive
- Reliable user sync with Auth0
- Existing users automatically linked
- Profile data stays current
- Role changes reflected immediately
- Clear audit trail via domain events

### Negative
- Requires Auth0 Action configuration
- Webhook adds slight login latency
- Must handle webhook failures gracefully
- Need to secure webhook endpoint

### Neutral
- Migration needed for existing users
- Auth0 becomes source of truth for profile data

## Future Considerations

1. **Batch sync** - Admin endpoint to sync all users
2. **Webhook retry** - Handle transient failures
3. **User deletion** - Soft delete when Auth0 user is deleted
4. **MFA status** - Sync MFA enrollment status

## References

- [Auth0 Actions](https://auth0.com/docs/customize/actions)
- [ADR-0004: Auth0 Tenant Setup](./adr-0004-auth0-tenant-setup.md)
- [Issue #20: User Synchronization](https://github.com/pablitxn/orchid-core/issues/20)
