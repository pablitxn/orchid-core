# Pre-Production Checklist

This document tracks temporary code, migrations, and cleanup tasks that **must be completed before deploying to production**.

---

## Pending Items

### 2. Redis Chat History Migration Cleanup

**Added:** 2025-01-21
**Purpose:** Remove migrated chat history from Redis after verifying AgentThread migration works correctly.
**Why temporary:** Redis history was kept as backup during migration. Once verified, it should be cleaned to avoid data duplication.

**Files to modify:**
- `services/backend/src/Adapters/Infrastructure/Ai/AgentFramework/Services/AgentThreadManager.cs` line 281:
  Uncomment: `await _cache.RemoveAsync(redisKey, cancellationToken);`

**Verification:**
1. Deploy to staging and verify conversations load correctly
2. Check logs for successful migrations
3. Uncomment the line and redeploy
4. Verify old sessions still work (should be in AgentThread memory now)

---

## Completed Items

### ~~1. UserPluginMigrationSeeder~~ ✅

**Removed:** 2025-01-22 (Issue #9 - Dead Code Elimination)

---

## Orphaned Database Tables (Review Required)

The following database tables are defined in `ApplicationDbContext` but may require review to confirm they are actively used:

| Table | Entity | Status | Notes |
|-------|--------|--------|-------|
| `Projects` | `ProjectEntity` | Review | Audio transcription feature - verify if still in use |
| `AudioFiles` | `AudioFileEntity` | Review | Related to Projects - verify if still in use |
| `SubscriptionPlans` | `SubscriptionPlanEntity` | Active | Referenced by Subscriptions |
| `CostConfigurations` | `CostConfigurationEntity` | Review | No explicit configuration in OnModelCreating |
| `Notifications` | `NotificationEntity` | Review | No explicit configuration in OnModelCreating |
| `AuditLogs` | `AuditLogEntity` | Active | Used for security audit logging |

**Note:** These tables should NOT be deleted without verifying they are truly unused. The recommendation is to review their usage in the codebase before any removal.

---

## How to Add Items

When adding temporary code (migrations, feature flags, backwards-compatibility shims, etc.):

1. Add a `TODO: Remove before production` comment in the code
2. Add an entry to this document with:
   - Date added
   - Purpose/context
   - Files and line numbers to modify
   - Verification steps after removal

---

## Pre-Production Review Process

Before any production deployment:

1. Review this entire document
2. Remove all listed temporary code
3. Run full test suite
4. Verify each removal with the listed verification steps
5. Update this document (remove completed items)
