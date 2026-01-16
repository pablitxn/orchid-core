# Orchid Core - Data Models

## Database Overview

- **Database:** PostgreSQL 17
- **ORM:** Entity Framework Core 9.0.5
- **Extensions:** pgvector for vector embeddings
- **Migrations:** Located in `Adapters/Infrastructure/Migrations/`

---

## Core Entities

### UserEntity
Primary user account entity.

| Column | Type | Description |
|--------|------|-------------|
| Id | Guid | Primary key |
| Email | string? | User email address |
| Name | string? | Display name |
| AvatarUrl | string? | Profile picture URL |
| PasswordHash | string | Hashed password (PBKDF2) |
| PasswordResetToken | string? | Password reset token |
| PasswordResetExpiry | DateTime? | Token expiration |
| PasswordChangedAt | DateTime? | Last password change |
| CreatedAt | DateTime | Account creation time |
| UpdatedAt | DateTime | Last update time |
| DeletedAt | DateTime? | Soft delete timestamp |

**Relationships:**
- Has many `UserRoleEntity` (roles)
- Has many `AgentEntity` (owned agents)
- Has many `SubscriptionEntity`

---

### AgentEntity
AI agent configuration.

| Column | Type | Description |
|--------|------|-------------|
| Id | Guid | Primary key |
| Name | string | Agent name |
| Description | string? | Agent description |
| AvatarUrl | string? | Agent avatar |
| Personality | string? | Custom personality prompt |
| PersonalityTemplateId | Guid? | Reference to template |
| Language | string? | Preferred language |
| PluginIds | Guid[] | Installed plugins |
| UserId | Guid? | Owner user ID |
| IsPublic | bool | Public visibility |
| IsDeleted | bool | Soft delete flag |
| DeletedAt | DateTime? | Deletion timestamp |
| IsInRecycleBin | bool | Recycle bin status |
| RecycleBinExpiresAt | DateTime? | Auto-delete time |
| CreatedAt | DateTime | Creation time |
| UpdatedAt | DateTime | Update time |

**Relationships:**
- Belongs to `UserEntity` (owner)
- Belongs to `PersonalityTemplateEntity`
- Has many `TeamAgentEntity` (team memberships)
- Has many `AgentRatingEntity` (ratings)

---

### ChatSessionEntity
Chat conversation session.

| Column | Type | Description |
|--------|------|-------------|
| Id | Guid | Primary key |
| UserId | Guid | Owner user |
| AgentId | Guid? | Associated agent |
| Title | string? | Session title |
| IsArchived | bool | Archive status |
| CreatedAt | DateTime | Creation time |
| UpdatedAt | DateTime | Update time |

---

### TeamEntity
Multi-agent team.

| Column | Type | Description |
|--------|------|-------------|
| Id | Guid | Primary key |
| Name | string | Team name |
| Description | string? | Team description |
| UserId | Guid | Owner user |
| InteractionPolicy | TeamInteractionPolicy | How agents interact |
| CreatedAt | DateTime | Creation time |
| UpdatedAt | DateTime | Update time |

**Relationships:**
- Has many `TeamAgentEntity` (agents)
- Belongs to `UserEntity` (owner)

---

### SubscriptionEntity
User subscription.

| Column | Type | Description |
|--------|------|-------------|
| Id | Guid | Primary key |
| UserId | Guid | Subscriber |
| PlanId | Guid | Subscription plan |
| Credits | int | Remaining credits |
| Status | string | Active/Cancelled/Expired |
| StartsAt | DateTime | Start date |
| ExpiresAt | DateTime? | Expiration date |
| CreatedAt | DateTime | Creation time |
| UpdatedAt | DateTime | Update time |

---

### CreditConsumptionEntity
Credit usage tracking.

| Column | Type | Description |
|--------|------|-------------|
| Id | Guid | Primary key |
| UserId | Guid | User who consumed |
| SubscriptionId | Guid | Source subscription |
| Amount | int | Credits consumed |
| ActionType | string | Type of action |
| Description | string? | Usage description |
| CreatedAt | DateTime | Consumption time |

---

### DocumentEntity
Knowledge base document.

| Column | Type | Description |
|--------|------|-------------|
| Id | Guid | Primary key |
| UserId | Guid | Owner |
| FileName | string | Original filename |
| ContentType | string | MIME type |
| StoragePath | string | File storage path |
| Status | DocumentStatus | Processing status |
| CreatedAt | DateTime | Upload time |
| UpdatedAt | DateTime | Update time |

---

### KnowledgeBaseFileEntity
Processed document with embeddings.

| Column | Type | Description |
|--------|------|-------------|
| Id | Guid | Primary key |
| DocumentId | Guid | Source document |
| UserId | Guid | Owner |
| ChunkIndex | int | Chunk number |
| Content | string | Text content |
| Embedding | vector | pgvector embedding |
| CreatedAt | DateTime | Creation time |

---

## Spreadsheet Entities

### WorkbookEntity
Excel workbook representation.

| Column | Type | Description |
|--------|------|-------------|
| Id | Guid | Primary key |
| UserId | Guid | Owner |
| FileName | string | Original filename |
| StoragePath | string | File path |
| SheetCount | int | Number of sheets |
| TotalRows | int | Total row count |
| CreatedAt | DateTime | Upload time |

### WorksheetEntity
Individual worksheet.

| Column | Type | Description |
|--------|------|-------------|
| Id | Guid | Primary key |
| WorkbookId | Guid | Parent workbook |
| Name | string | Sheet name |
| RowCount | int | Rows in sheet |
| ColumnCount | int | Columns in sheet |

### CellEntity
Cell data.

| Column | Type | Description |
|--------|------|-------------|
| Id | Guid | Primary key |
| WorksheetId | Guid | Parent worksheet |
| Row | int | Row index |
| Column | int | Column index |
| Value | string? | Cell value |

---

## Orphaned or Legacy Tables

The following table concepts appear in legacy docs or code paths but are not
part of the current EF Core model or migrations. Do not drop them without a DB
audit.

- WorkbookEntity
- WorksheetEntity
- CellEntity
| Formula | string? | Cell formula |
| DataType | string | Value type |

---

## Authentication Entities

### RoleEntity
User roles.

| Column | Type | Description |
|--------|------|-------------|
| Id | Guid | Primary key |
| Name | string | Role name (User, Admin) |

### UserRoleEntity
User-role junction.

| Column | Type | Description |
|--------|------|-------------|
| UserId | Guid | User reference |
| RoleId | Guid | Role reference |

### RefreshTokenEntity
JWT refresh tokens.

| Column | Type | Description |
|--------|------|-------------|
| Id | Guid | Primary key |
| UserId | Guid | Token owner |
| Token | string | Token value (Base64) |
| ExpiresAt | DateTime | Expiration time |
| CreatedAt | DateTime | Creation time |
| CreatedByIp | string? | Client IP |
| RevokedAt | DateTime? | Revocation time |
| RevokedByIp | string? | Revoking IP |
| ReplacedByToken | string? | Replacement token |
| RevokeReason | string? | Revocation reason |

### LoginHistoryEntity
Authentication audit log.

| Column | Type | Description |
|--------|------|-------------|
| Id | Guid | Primary key |
| UserId | Guid | User reference |
| IpAddress | string | Client IP |
| UserAgent | string? | Browser/client info |
| Success | bool | Login succeeded |
| FailureReason | string? | Failure message |
| CreatedAt | DateTime | Attempt time |

---

## Plugin System Entities

### PluginEntity
Plugin definition.

| Column | Type | Description |
|--------|------|-------------|
| Id | Guid | Primary key |
| Name | string | Plugin name |
| Description | string? | Plugin description |
| Version | string | Version number |
| Code | string | Plugin code/config |
| IsPublic | bool | Marketplace visibility |
| Price | decimal? | Purchase price |
| AuthorId | Guid | Plugin author |
| CreatedAt | DateTime | Creation time |

### AgentPluginEntity
Agent-plugin association.

| Column | Type | Description |
|--------|------|-------------|
| AgentId | Guid | Agent reference |
| PluginId | Guid | Plugin reference |
| InstalledAt | DateTime | Installation time |

### UserPluginEntity
User-owned plugins.

| Column | Type | Description |
|--------|------|-------------|
| UserId | Guid | User reference |
| PluginId | Guid | Plugin reference |
| PurchasedAt | DateTime | Purchase time |
| IsEnabled | bool | Enabled status |

---

## Other Entities

### PersonalityTemplateEntity
Reusable agent personalities.

| Column | Type | Description |
|--------|------|-------------|
| Id | Guid | Primary key |
| Name | string | Template name |
| Description | string? | Description |
| SystemPrompt | string | Personality prompt |
| UserId | Guid | Owner |
| IsPublic | bool | Public visibility |

### NotificationEntity
User notifications.

| Column | Type | Description |
|--------|------|-------------|
| Id | Guid | Primary key |
| UserId | Guid | Recipient |
| Type | string | Notification type |
| Title | string | Notification title |
| Message | string | Notification body |
| IsRead | bool | Read status |
| CreatedAt | DateTime | Creation time |

### AuditLogEntity
System audit trail.

| Column | Type | Description |
|--------|------|-------------|
| Id | Guid | Primary key |
| UserId | Guid? | Acting user |
| Action | string | Action performed |
| EntityType | string | Affected entity type |
| EntityId | string | Affected entity ID |
| Details | string? | JSON details |
| IpAddress | string? | Client IP |
| CreatedAt | DateTime | Action time |

---

## Enumerations

### DocumentStatus
```csharp
enum DocumentStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}
```

### InteractionType
```csharp
enum InteractionType
{
    Chat,
    Voice,
    Video
}
```

### TeamInteractionPolicy
```csharp
enum TeamInteractionPolicy
{
    Sequential,
    Parallel,
    Collaborative
}
```

### SubscriptionPlanEnum
```csharp
enum SubscriptionPlanEnum
{
    Free,
    Basic,
    Pro,
    Enterprise
}
```

---

## Database Relationships Diagram

```
UserEntity
├── UserRoles[] ──► RoleEntity
├── Agents[] ──► AgentEntity
│                  ├── PersonalityTemplate? ──► PersonalityTemplateEntity
│                  ├── AgentPlugins[] ──► PluginEntity
│                  └── TeamAgents[] ──► TeamEntity
├── Teams[] ──► TeamEntity
├── Subscriptions[] ──► SubscriptionEntity
│                       └── CreditConsumptions[]
├── Documents[] ──► DocumentEntity
│                   └── KnowledgeBaseFiles[]
├── RefreshTokens[]
├── LoginHistory[]
├── Notifications[]
└── UserPlugins[] ──► PluginEntity
```
