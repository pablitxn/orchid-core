# Pre-Production Checklist

This document tracks temporary code, migrations, and cleanup tasks that **must be completed before deploying to production**.

---

## Temporary Code to Remove

### 1. UserPluginMigrationSeeder

**Added:** 2025-01-19
**Purpose:** Retroactively assigns public agent plugins to existing users in dev/staging environments.
**Why temporary:** Only needed for migration of existing environments. New users get plugins automatically via `UserCreatedEventHandler`.

**Files to remove:**
- `src/Adapters/Infrastructure/Persistence/Seeders/UserPluginMigrationSeeder.cs` (entire file)
- `src/Adapters/Infrastructure/InfrastructureServiceCollectionExtensions.cs` lines 175-177:
  ```csharp
  // ─── User plugin migration (temporary, remove before production) ─────────────
  // TODO: Remove this service after all environments are migrated
  services.AddHostedService<Persistence.Seeders.UserPluginMigrationSeeder>();
  ```

**Verification:** After removal, ensure new user registration still assigns public agent plugins via `UserCreatedEventHandler.AssignPublicAgentPluginsAsync()`.

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
