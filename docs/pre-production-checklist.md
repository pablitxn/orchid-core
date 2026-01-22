# Pre-Production Checklist

This document tracks temporary code, migrations, and cleanup tasks that **must be completed before deploying to production**.

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
