---
epicId: '0004'
title: 'Marketplace'
phase: 3
priority: P2
masterPrd: prd.md
status: draft
estimatedEffort: '3 weeks'
dependencies:
  - epic-0003
---

# Epic PRD: 0004 - Marketplace

**Parent:** [Master PRD](./prd.md)
**Phase:** 3 - Extend
**Priority:** P2 (Medium)
**Author:** Pablo
**Date:** 2026-01-04

---

## Overview

Build the backend infrastructure for an agent plugin marketplace where users can discover, install, and manage plugins that extend their AI agents' capabilities. This leverages the plugin system from Epic 0003 to create a curated ecosystem of extensions.

### Current State

Based on codebase analysis:
- Agent entity exists with basic properties
- Plugin loading exists but no marketplace
- No plugin discovery or installation system
- No plugin ratings or reviews
- No publisher management

### Target State

- Searchable plugin catalog
- Plugin installation per user/workspace
- Rating and review system
- Publisher submission flow
- Plugin versioning and updates
- Usage analytics per plugin

---

## Goals

| Goal | Success Metric |
|------|----------------|
| Plugin discovery | Users find relevant plugins via search |
| Easy installation | One-click plugin enable/disable |
| Quality signals | Ratings help users choose plugins |
| Publisher ecosystem | Third-party plugins submittable |
| Usage tracking | Plugin popularity measurable |

---

## Scope

### In Scope

1. **Plugin Catalog**
   - Plugin listing with metadata
   - Search and filtering
   - Categories and tags
   - Featured/promoted plugins
   - Plugin detail pages (API response)

2. **Plugin Installation**
   - User plugin preferences
   - Workspace-level plugin settings
   - Enable/disable plugins per agent
   - Plugin configuration storage

3. **Rating & Reviews**
   - Star ratings (1-5)
   - Text reviews
   - Review moderation
   - Aggregate ratings

4. **Publisher System**
   - Publisher registration
   - Plugin submission
   - Review/approval workflow
   - Publisher dashboard data

5. **Plugin Management**
   - Version management
   - Update notifications
   - Deprecation handling
   - Usage statistics

### Out of Scope

- Frontend marketplace UI (separate PRD)
- Payment/revenue sharing for plugins
- Plugin development SDK
- Automated plugin testing
- Plugin hosting (publishers host own artifacts)

---

## Requirements

### REQ-0004-01: Plugin Catalog

**Description:** Backend API for browsing and searching plugins.

**Acceptance Criteria:**
- [ ] List all published plugins with pagination
- [ ] Search plugins by name, description, tags
- [ ] Filter by category, rating, compatibility
- [ ] Sort by popularity, rating, newest
- [ ] Featured plugins endpoint
- [ ] Plugin detail endpoint with full metadata

**API Endpoints:**
```
GET    /api/marketplace/plugins
       ?search=query
       &category=productivity
       &minRating=4
       &sortBy=popularity
       &page=1&limit=20

GET    /api/marketplace/plugins/{pluginId}
GET    /api/marketplace/plugins/featured
GET    /api/marketplace/categories
```

**Response Model:**
```json
{
  "plugins": [
    {
      "id": "uuid",
      "name": "Knowledge Search Pro",
      "slug": "knowledge-search-pro",
      "shortDescription": "Enhanced RAG search",
      "category": "productivity",
      "tags": ["search", "rag", "knowledge"],
      "publisher": {
        "id": "uuid",
        "name": "Orchid Labs",
        "verified": true
      },
      "version": "2.1.0",
      "rating": 4.5,
      "reviewCount": 128,
      "installCount": 5420,
      "iconUrl": "https://...",
      "compatibility": ["agent-v2"],
      "pricing": "free",
      "createdAt": "2026-01-01T00:00:00Z"
    }
  ],
  "pagination": {
    "page": 1,
    "limit": 20,
    "total": 145
  }
}
```

---

### REQ-0004-02: Plugin Installation

**Description:** User-level plugin management and configuration.

**Acceptance Criteria:**
- [ ] Users can install/uninstall plugins
- [ ] Plugins can be enabled/disabled per agent
- [ ] Plugin-specific settings stored per user
- [ ] Workspace admins can manage workspace plugins
- [ ] Installation count tracked
- [ ] Proper authorization checks

**API Endpoints:**
```
POST   /api/user/plugins/{pluginId}/install
DELETE /api/user/plugins/{pluginId}/uninstall
PUT    /api/user/plugins/{pluginId}/settings
GET    /api/user/plugins
GET    /api/user/plugins/{pluginId}

POST   /api/agents/{agentId}/plugins/{pluginId}/enable
DELETE /api/agents/{agentId}/plugins/{pluginId}/disable
GET    /api/agents/{agentId}/plugins
```

**User Plugin Model:**
```json
{
  "pluginId": "uuid",
  "installedAt": "2026-01-04T00:00:00Z",
  "enabled": true,
  "settings": {
    "maxResults": 10,
    "includeArchived": false
  },
  "enabledAgents": ["agent-1", "agent-2"]
}
```

---

### REQ-0004-03: Rating & Reviews

**Description:** User feedback system for plugins.

**Acceptance Criteria:**
- [ ] Users can rate plugins (1-5 stars)
- [ ] Users can write text reviews
- [ ] One review per user per plugin (updateable)
- [ ] Reviews require plugin to be installed
- [ ] Review moderation (flag inappropriate)
- [ ] Aggregate ratings calculated

**API Endpoints:**
```
POST   /api/marketplace/plugins/{pluginId}/reviews
PUT    /api/marketplace/plugins/{pluginId}/reviews/{reviewId}
DELETE /api/marketplace/plugins/{pluginId}/reviews/{reviewId}
GET    /api/marketplace/plugins/{pluginId}/reviews
       ?sortBy=newest|helpful
       &page=1&limit=10

POST   /api/marketplace/reviews/{reviewId}/flag
```

**Review Model:**
```json
{
  "id": "uuid",
  "pluginId": "uuid",
  "userId": "uuid",
  "userName": "John D.",
  "rating": 5,
  "title": "Great plugin!",
  "body": "This plugin has transformed how I...",
  "helpful": 42,
  "createdAt": "2026-01-04T00:00:00Z",
  "updatedAt": null,
  "verified": true
}
```

---

### REQ-0004-04: Publisher System

**Description:** Infrastructure for plugin publishers.

**Acceptance Criteria:**
- [ ] Publishers can register
- [ ] Publishers can submit plugins for review
- [ ] Admin review/approval workflow
- [ ] Publishers can update plugins
- [ ] Publisher dashboard data (stats)
- [ ] Publisher verification process

**API Endpoints:**
```
POST   /api/publishers/register
GET    /api/publishers/me
PUT    /api/publishers/me

POST   /api/publishers/plugins
PUT    /api/publishers/plugins/{pluginId}
GET    /api/publishers/plugins
POST   /api/publishers/plugins/{pluginId}/versions
GET    /api/publishers/plugins/{pluginId}/stats

-- Admin
GET    /api/admin/marketplace/submissions
POST   /api/admin/marketplace/submissions/{id}/approve
POST   /api/admin/marketplace/submissions/{id}/reject
```

**Plugin Submission Model:**
```json
{
  "name": "My Plugin",
  "slug": "my-plugin",
  "shortDescription": "Does cool things",
  "fullDescription": "# My Plugin\n\nDetailed markdown...",
  "category": "productivity",
  "tags": ["utility", "search"],
  "version": "1.0.0",
  "manifestUrl": "https://github.com/.../manifest.json",
  "repositoryUrl": "https://github.com/...",
  "iconUrl": "https://...",
  "screenshotUrls": ["https://..."],
  "compatibility": {
    "minAgentVersion": "2.0.0"
  },
  "pricing": "free"
}
```

---

### REQ-0004-05: Plugin Versioning

**Description:** Handle plugin versions and updates.

**Acceptance Criteria:**
- [ ] Plugins have semantic versions
- [ ] Users can see available updates
- [ ] Version changelog stored
- [ ] Breaking changes flagged
- [ ] Rollback to previous version possible
- [ ] Deprecation notices supported

**API Endpoints:**
```
GET    /api/marketplace/plugins/{pluginId}/versions
GET    /api/user/plugins/updates
POST   /api/user/plugins/{pluginId}/update
POST   /api/user/plugins/{pluginId}/rollback
```

**Version Model:**
```json
{
  "version": "2.1.0",
  "releaseDate": "2026-01-04T00:00:00Z",
  "changelog": "- Added feature X\n- Fixed bug Y",
  "breaking": false,
  "minCompatibility": "agent-v2",
  "downloadUrl": "https://...",
  "checksum": "sha256:..."
}
```

---

### REQ-0004-06: Usage Analytics

**Description:** Track plugin usage for publishers and platform.

**Acceptance Criteria:**
- [ ] Installation count per plugin
- [ ] Active usage count (DAU/MAU)
- [ ] Usage by agent type
- [ ] Error rates per plugin
- [ ] Publisher can see their plugin stats
- [ ] Platform-wide analytics for admins

**Events Tracked:**
- `plugin.installed`
- `plugin.uninstalled`
- `plugin.invoked`
- `plugin.error`
- `plugin.review.created`

**Stats Response:**
```json
{
  "pluginId": "uuid",
  "period": "last_30_days",
  "installs": 542,
  "uninstalls": 23,
  "activeUsers": 1205,
  "invocations": 45230,
  "errorRate": 0.02,
  "avgRating": 4.3,
  "reviewCount": 89
}
```

---

## Technical Design Considerations

### Data Model

```
Publisher
├── Id (Guid)
├── UserId (Guid) -- owner
├── Name (string)
├── Slug (string)
├── Description (string)
├── Website (string?)
├── Verified (bool)
├── Status (enum: Pending, Active, Suspended)
└── Timestamps

MarketplacePlugin
├── Id (Guid)
├── PublisherId (Guid)
├── Slug (string, unique)
├── Name (string)
├── ShortDescription (string)
├── FullDescription (text)
├── Category (string)
├── Tags (string[])
├── IconUrl (string)
├── RepositoryUrl (string?)
├── CurrentVersion (string)
├── Status (enum: Draft, PendingReview, Published, Deprecated)
├── InstallCount (int)
├── RatingAverage (decimal)
├── RatingCount (int)
├── Compatibility (jsonb)
├── Pricing (string)
└── Timestamps

PluginVersion
├── Id (Guid)
├── PluginId (Guid)
├── Version (string)
├── Changelog (text)
├── ManifestUrl (string)
├── Checksum (string)
├── Breaking (bool)
├── MinAgentVersion (string)
├── Status (enum: Draft, Published, Deprecated)
└── Timestamps

UserPlugin
├── Id (Guid)
├── UserId (Guid)
├── PluginId (Guid)
├── Version (string)
├── Settings (jsonb)
├── Enabled (bool)
├── InstalledAt (DateTime)
└── UpdatedAt (DateTime)

AgentPlugin
├── AgentId (Guid)
├── PluginId (Guid)
├── Enabled (bool)
├── Configuration (jsonb)
└── Timestamps

PluginReview
├── Id (Guid)
├── PluginId (Guid)
├── UserId (Guid)
├── Rating (int)
├── Title (string)
├── Body (text)
├── HelpfulCount (int)
├── Status (enum: Active, Flagged, Removed)
└── Timestamps
```

### Architecture

```
WebApi
└── Controllers/
    ├── MarketplaceController.cs
    ├── PublisherController.cs
    └── UserPluginController.cs

Application
└── UseCases/
    ├── Marketplace/
    │   ├── SearchPlugins/
    │   ├── GetPluginDetails/
    │   └── GetFeaturedPlugins/
    ├── Plugins/
    │   ├── InstallPlugin/
    │   ├── UninstallPlugin/
    │   ├── UpdatePlugin/
    │   └── ConfigurePlugin/
    ├── Reviews/
    │   ├── CreateReview/
    │   ├── UpdateReview/
    │   └── FlagReview/
    └── Publishers/
        ├── RegisterPublisher/
        ├── SubmitPlugin/
        └── UpdatePlugin/

Domain
└── Marketplace/
    ├── Entities/
    │   ├── Publisher.cs
    │   ├── MarketplacePlugin.cs
    │   ├── PluginVersion.cs
    │   └── PluginReview.cs
    └── Events/
        ├── PluginPublished.cs
        ├── PluginInstalled.cs
        └── ReviewCreated.cs
```

### Search Implementation

```csharp
// Using PostgreSQL full-text search
public async Task<PagedResult<PluginSummary>> SearchPluginsAsync(
    string? query,
    string? category,
    decimal? minRating,
    string sortBy,
    int page,
    int limit,
    CancellationToken ct)
{
    var queryable = _db.MarketplacePlugins
        .Where(p => p.Status == PluginStatus.Published);

    if (!string.IsNullOrEmpty(query))
    {
        queryable = queryable.Where(p =>
            EF.Functions.ToTsVector("english", p.Name + " " + p.ShortDescription)
                .Matches(EF.Functions.PlainToTsQuery("english", query)));
    }

    if (!string.IsNullOrEmpty(category))
        queryable = queryable.Where(p => p.Category == category);

    if (minRating.HasValue)
        queryable = queryable.Where(p => p.RatingAverage >= minRating);

    queryable = sortBy switch
    {
        "rating" => queryable.OrderByDescending(p => p.RatingAverage),
        "newest" => queryable.OrderByDescending(p => p.CreatedAt),
        _ => queryable.OrderByDescending(p => p.InstallCount)
    };

    return await queryable.ToPagedResultAsync(page, limit, ct);
}
```

---

## Dependencies

### External
- None (self-contained)

### Internal
- Epic 0003 (Agent Framework) - plugin system foundation
- Epic 0001 (Subscriptions) - for premium plugins (future)
- Epic 0002 (Auth0) - user/publisher identity

---

## Security Considerations

1. **Plugin Validation**
   - Manifest schema validation
   - URL allowlists for plugin sources
   - Checksum verification on load

2. **Publisher Verification**
   - Email verification required
   - Manual verification for "verified" badge
   - Rate limiting on submissions

3. **Review Spam Prevention**
   - One review per user per plugin
   - Require plugin installation to review
   - Rate limiting on review creation

4. **Admin Controls**
   - Ability to remove malicious plugins
   - Publisher suspension capability
   - Content moderation for reviews

---

## Success Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| Plugin catalog size | 20+ plugins | DB count |
| Plugin search latency | < 200ms p95 | Monitoring |
| Installation success | 99.9% | Error tracking |
| Publisher satisfaction | 4+ stars | Survey |

---

## Open Questions

1. Should there be a review process for all plugins or only featured?
2. What categories should exist at launch?
3. How to handle plugin security scanning?
4. Should publishers be able to see who installed their plugins?
5. What's the plugin deprecation policy?

---

## Future Considerations

- Premium/paid plugins with revenue sharing
- Plugin bundles/collections
- AI-powered plugin recommendations
- Plugin compatibility checker
- Automated security scanning

---

*This is an Epic PRD. See [Master PRD](./prd.md) for overall project context.*
