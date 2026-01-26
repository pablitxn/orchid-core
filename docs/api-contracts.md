# Orchid Core - API Contracts

## Base URL
```
/api
```

## Authentication
Most endpoints require JWT Bearer authentication via:
- `Authorization: Bearer <token>` header, or
- `token` HttpOnly cookie

---

## Auth Controller (`/api/auth`)

### Get Auth Mode
```http
GET /api/auth/mode
```
**Response:** `{ "provider": "google" | "generic" }`

### Register
```http
POST /api/auth/register
Content-Type: application/json

{
  "name": "string",
  "email": "string",
  "password": "string"
}
```
**Rate Limit:** 3 per hour per IP

### Login
```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "string",
  "password": "string"
}
```
**Rate Limit:** 5 per 15 min per account, 10 per 15 min per IP
**Response:** `{ "token": "string", "refreshToken": "string", "expiresAt": "datetime" }`

### Refresh Token
```http
POST /api/auth/refresh
Content-Type: application/json

{
  "refreshToken": "string"  // Optional, also read from cookie
}
```

### Logout
```http
POST /api/auth/logout
Authorization: Bearer <token>
```

### Revoke All Tokens
```http
POST /api/auth/revoke-all-tokens
Authorization: Bearer <token>
```

### Forgot Password
```http
POST /api/auth/forgot-password
Content-Type: application/json

{
  "email": "string"
}
```
**Rate Limit:** 3 per hour per IP

### Reset Password
```http
POST /api/auth/reset-password
Content-Type: application/json

{
  "email": "string",
  "token": "string",
  "newPassword": "string"
}
```

### Service Token (Machine-to-Machine)
```http
POST /api/auth/service-token
Content-Type: application/json

{
  "clientId": "string",
  "clientSecret": "string"
}
```
**Rate Limit:** 10 per minute per IP
**Response:** `{ "accessToken": "string", "tokenType": "Bearer", "expiresIn": <seconds> }`

### Google OAuth
```http
GET /api/auth/google
```
Redirects to Google OAuth consent screen.

```http
GET /api/auth/callback/google?code=<authorization_code>
```
Handles OAuth callback, creates/finds user, sets cookies.

---

## Agents Controller (`/api/agents`)

### List Agents (Public + User's)
```http
GET /api/agents
Authorization: Bearer <token> (optional)
```

### List My Agents
```http
GET /api/agents/my-agents
Authorization: Bearer <token>
```

### Create Agent
```http
POST /api/agents
Authorization: Bearer <token>
Content-Type: application/json

{
  "name": "string",
  "description": "string?",
  "avatarUrl": "string?",
  "personality": "string?",
  "personalityTemplateId": "guid?",
  "language": "string?",
  "model": "string?",
  "pluginIds": ["guid"],
  "isPublic": false
}
```

### Update Agent
```http
PUT /api/agents/{id}
Authorization: Bearer <token>
Content-Type: application/json

{
  "name": "string?",
  "description": "string?",
  "avatarUrl": "string?",
  "personality": "string?",
  "personalityTemplateId": "guid?",
  "language": "string?",
  "model": "string?",
  "pluginIds": ["guid"],
  "isPublic": "boolean?"
}
```

### Delete Agent (Soft Delete)
```http
DELETE /api/agents/{id}
Authorization: Bearer <token>
```

### Get Recycle Bin Agents
```http
GET /api/agents/recycle-bin
Authorization: Bearer <token>
```

### Restore Agent
```http
POST /api/agents/{id}/restore
Authorization: Bearer <token>
```

### Permanent Delete Agent
```http
DELETE /api/agents/{id}/permanent
Authorization: Bearer <token>
```

### Rate Agent
```http
POST /api/agents/{id}/rate
Authorization: Bearer <token>
Content-Type: application/json

{
  "rating": 1-5,
  "comment": "string?"
}
```

### Get Agent Ratings
```http
GET /api/agents/{id}/ratings
```

### Verify Agent Access
```http
GET /api/agents/{id}/verify-access
Authorization: Bearer <token>
```

---

## Chat Sessions Controller (`/api/chatsessions`)

### List Chat Sessions
```http
GET /api/chatsessions
Authorization: Bearer <token>
```

### Get Chat Session
```http
GET /api/chatsessions/{id}
Authorization: Bearer <token>
```

### Create Chat Session
```http
POST /api/chatsessions
Authorization: Bearer <token>
Content-Type: application/json

{
  "agentId": "guid",
  "title": "string?"
}
```

### Archive Chat Session
```http
PUT /api/chatsessions/{id}/archive
Authorization: Bearer <token>
```

### Delete Chat Sessions
```http
DELETE /api/chatsessions
Authorization: Bearer <token>
Content-Type: application/json

{
  "sessionIds": ["guid"]
}
```

---

## V2 Chat Messages (`/api/v2/chat-sessions/{sessionId}/messages`)

### List Messages (Cursor-based pagination)
```http
GET /api/v2/chat-sessions/{sessionId}/messages
Authorization: Bearer <token>

Query Parameters:
- limit: int (default: 50, max: 100)
- cursor: string (base64-encoded cursor)
- direction: "backward" | "forward"
```

### Send Message
```http
POST /api/v2/chat-sessions/{sessionId}/messages
Authorization: Bearer <token>
Content-Type: application/json

{
  "content": "string",
  "artifacts": [...]
}
```

---

## Knowledge Base Controller (`/api/knowledge-base`)

### List Knowledge Base Files
```http
GET /api/knowledge-base
Authorization: Bearer <token>
```

### Get Knowledge Base File
```http
GET /api/knowledge-base/{id}
Authorization: Bearer <token>
```

### Upload Document
```http
POST /api/knowledge-base
Authorization: Bearer <token>
Content-Type: multipart/form-data

file: <binary>
```

### Update Knowledge Base
```http
PUT /api/knowledge-base/{id}
Authorization: Bearer <token>
```

### Delete Knowledge Base
```http
DELETE /api/knowledge-base/{id}
Authorization: Bearer <token>
```

---

## Teams Controller (`/api/teams`)

### List Teams
```http
GET /api/teams
Authorization: Bearer <token>
```

### Get Team
```http
GET /api/teams/{id}
Authorization: Bearer <token>
```

### Create Team
```http
POST /api/teams
Authorization: Bearer <token>
Content-Type: application/json

{
  "name": "string",
  "description": "string?",
  "interactionPolicy": "string"
}
```

### Add Agent to Team
```http
POST /api/teams/{teamId}/agents/{agentId}
Authorization: Bearer <token>
```

### Remove Agent from Team
```http
DELETE /api/teams/{teamId}/agents/{agentId}
Authorization: Bearer <token>
```

---

## Plugins Controller (`/api/plugins`)

### List All Plugins
```http
GET /api/plugins
```

### Install Plugin
```http
POST /api/plugins
Content-Type: application/json

{
  "name": "string",
  "description": "string",
  ...
}
```

### Activate Plugin
```http
POST /api/plugins/{id}/activate
```

### Deactivate Plugin
```http
POST /api/plugins/{id}/deactivate
```

---

## Plugin Controller (`/api/plugin`)

### List User's Plugins
```http
GET /api/plugin
Authorization: Bearer <token>
```

### List Available Plugins
```http
GET /api/plugin/available
Authorization: Bearer <token>
```

### Purchase Plugin
```http
POST /api/plugin/{pluginId}/purchase
Authorization: Bearer <token>
```

### Add Plugin to Agent
```http
POST /api/plugin/agent/{agentId}/plugin/{pluginId}
Authorization: Bearer <token>
```

### Remove Plugin from Agent
```http
DELETE /api/plugin/agent/{agentId}/plugin/{pluginId}
Authorization: Bearer <token>
```

### Delete Plugin
```http
DELETE /api/plugin/{pluginId}
Authorization: Bearer <token>
```

---

## Workflows Controller (`/api/workflows`)

### List Workflows
```http
GET /api/workflows
```

### Get Workflow
```http
GET /api/workflows/{id}
```

### Create Workflow
```http
POST /api/workflows
Content-Type: application/json
```

### Purchase Workflow
```http
POST /api/workflows/{id}/purchase
Authorization: Bearer <token>
```

---

## Marketplace Controller (`/api/marketplace`)

### Get User Marketplace Items
```http
GET /api/marketplace/user/items
Authorization: Bearer <token>
```

### Get My Items (Backward compatibility)
```http
GET /api/marketplace/my-items
Authorization: Bearer <token>
```

### Purchase Plugin
```http
POST /api/marketplace/plugins/{pluginId}/purchase
Authorization: Bearer <token>
```

### Purchase Workflow
```http
POST /api/marketplace/workflows/{workflowId}/purchase
Authorization: Bearer <token>
```

---

## Public Marketplace (`/api/public/marketplace`)

### List Public Plugins
```http
GET /api/public/marketplace/plugins
```

### Get Plugin Details
```http
GET /api/public/marketplace/plugins/{pluginId}
```

### List Public Workflows
```http
GET /api/public/marketplace/workflows
```

### Get Workflow Details
```http
GET /api/public/marketplace/workflows/{workflowId}
```

---

## Subscription Controller (`/api/subscription`)

### Create Subscription
```http
POST /api/subscription
Authorization: Bearer <token>
Content-Type: application/json

{
  "userId": "guid",
  "credits": <int>,
  "expiresAt": "datetime?"
}
```

### Get Subscription
```http
GET /api/subscription/{userId}
Authorization: Bearer <token>
```

### Get Subscription Status
```http
GET /api/subscription/status
Authorization: Bearer <token>
```

### Get Subscription History
```http
GET /api/subscription/history
Authorization: Bearer <token>
```

### Consume Credits
```http
POST /api/subscription/consume
Authorization: Bearer <token>
Content-Type: application/json

{
  "userId": "guid",
  "amount": <int>
}
```

### Add Credits
```http
POST /api/subscription/add
Authorization: Bearer <token>
Content-Type: application/json

{
  "userId": "guid",
  "amount": <int>
}
```

### Purchase Plan
```http
POST /api/subscription/purchase
Authorization: Bearer <token>
Content-Type: application/json

{
  "userId": "guid",
  "plan": "Free" | "Basic" | "Premium" | "Enterprise"
}
```

### Toggle Auto-Renew
```http
POST /api/subscription/auto-renew
PUT /api/subscription/auto-renew
Authorization: Bearer <token>
Content-Type: application/json

{
  "userId": "guid",
  "autoRenew": <boolean>
}
```

---

## Billing Preference Controller (`/api/billingpreference`)

### Get Billing Preferences
```http
GET /api/billingpreference
Authorization: Bearer <token>
```

### Update Billing Preferences
```http
PUT /api/billingpreference
Authorization: Bearer <token>
Content-Type: application/json

{
  "messageBillingMethod": "tokens" | "fixed",
  "tokenRate": <decimal>,
  "fixedMessageRate": <int>,
  "lowCreditThreshold": <int?>,
  "enableLowCreditAlerts": <boolean>
}
```

---

## Credit Validation Controller (`/api/credit-validation`)

### Validate Message Cost
```http
POST /api/credit-validation/message/validate
Authorization: Bearer <token>
Content-Type: application/json

{
  "messageContent": "string",
  "pluginIds": ["guid"],
  "workflowIds": ["guid"]
}
```

### Validate Plugin Purchase
```http
POST /api/credit-validation/plugin/{pluginId}/validate
Authorization: Bearer <token>
```

### Validate Workflow Purchase
```http
POST /api/credit-validation/workflow/{workflowId}/validate
Authorization: Bearer <token>
```

### Validate Operation
```http
POST /api/credit-validation/operation/validate
Authorization: Bearer <token>
Content-Type: application/json

{
  "requiredCredits": <int>,
  "operationType": "string"
}
```

---

## Credit History Controller (`/api/credithistory`)

### Get Consumption History
```http
GET /api/credithistory/consumption
Authorization: Bearer <token>

Query Parameters:
- limit: int (default: 50)
- startDate: datetime?
- endDate: datetime?
```

### Get Message Cost History
```http
GET /api/credithistory/messages
Authorization: Bearer <token>

Query Parameters:
- limit: int (default: 50)
```

### Get Consumption Summary
```http
GET /api/credithistory/summary
Authorization: Bearer <token>

Query Parameters:
- period: "day" | "week" | "month" | "year" (default: "month")
```

### Get Credit Balance
```http
GET /api/credithistory/balance
Authorization: Bearer <token>
```

---

## Dashboard Controller (`/api/dashboard`)

### Get Dashboard Overview
```http
GET /api/dashboard/overview
Authorization: Bearer <token>
```

### Get Credit History
```http
GET /api/dashboard/credits/history
Authorization: Bearer <token>

Query Parameters:
- pageNumber: int (default: 1)
- pageSize: int (default: 20)
- startDate: datetime?
- endDate: datetime?
- consumptionType: string?
```

### Get Credit Statistics
```http
GET /api/dashboard/credits/statistics
Authorization: Bearer <token>
```

### Get Session Details
```http
GET /api/dashboard/sessions
Authorization: Bearer <token>

Query Parameters:
- pageNumber: int (default: 1)
- pageSize: int (default: 20)
- isArchived: boolean?
- agentId: guid?
- teamId: guid?
- startDate: datetime?
- endDate: datetime?
```

### Get Security Metrics
```http
GET /api/dashboard/security
Authorization: Bearer <token>
```

### Get Session Activity Heatmap
```http
GET /api/dashboard/sessions/activity-heatmap
Authorization: Bearer <token>
```

### Get Agent Performance
```http
GET /api/dashboard/agents/performance
Authorization: Bearer <token>
```

### Get Plugin Analytics
```http
GET /api/dashboard/plugins/analytics
Authorization: Bearer <token>
```

### Get Workflow Analytics
```http
GET /api/dashboard/workflows/analytics
Authorization: Bearer <token>
```

### Get Knowledge Base Statistics
```http
GET /api/dashboard/knowledge-base/statistics
Authorization: Bearer <token>
```

### Get Media Center Statistics
```http
GET /api/dashboard/media-center/statistics
Authorization: Bearer <token>
```

### Get Billing History
```http
GET /api/dashboard/billing/history
Authorization: Bearer <token>
```

### Get Usage Forecast
```http
GET /api/dashboard/forecast
Authorization: Bearer <token>
```

### Export Dashboard Data
```http
GET /api/dashboard/export
Authorization: Bearer <token>

Query Parameters:
- format: "csv" | "pdf" (default: "csv")
- startDate: datetime?
- endDate: datetime?
```

---

## User Controller (`/api/user`)

### Get Current User
```http
GET /api/user/me
Authorization: Bearer <token>
```

### Update User Profile
```http
PUT /api/user/me
Authorization: Bearer <token>
```

---

## Personality Templates Controller (`/api/personality-templates`)

### List Templates
```http
GET /api/personality-templates
Authorization: Bearer <token>

Query Parameters:
- includeInactive: boolean (default: false)
```

### Get Template
```http
GET /api/personality-templates/{id}
Authorization: Bearer <token>
```

### Create Template (Admin only)
```http
POST /api/personality-templates
Authorization: Bearer <token>
Requires: admin role
```

### Update Template (Admin only)
```http
PUT /api/personality-templates/{id}
Authorization: Bearer <token>
Requires: admin role
```

### Delete Template (Admin only)
```http
DELETE /api/personality-templates/{id}
Authorization: Bearer <token>
Requires: admin role
```

---

## Files Controller (`/api/files`)

### Upload File
```http
POST /api/files/upload
Authorization: Bearer <token>
Content-Type: multipart/form-data
```

### Download File
```http
GET /api/files/{id}
Authorization: Bearer <token>
```

---

## Features Controller (`/api/features`)

### Get All Features
```http
GET /api/features
Authorization: Bearer <token> (optional)
```

### Get Specific Feature
```http
GET /api/features/{key}
Authorization: Bearer <token> (optional)
```

---

## Activities Controller (`/api/activities`)

### Get Activities
```http
GET /api/activities
```
Returns activity history from Redis cache.

---

## Media Center Controller (`/api/media-center`)

### Get Assets
```http
GET /api/media-center/assets
Query Parameters:
- mimeType: string?
```

---

## Project Controller (`/api/projects`)

### List Projects
```http
GET /api/projects
Authorization: Bearer <token>
```

### Get Project
```http
GET /api/projects/{id}
Authorization: Bearer <token>
```

### Create Project from Document
```http
POST /api/projects/document
Authorization: Bearer <token>
Content-Type: multipart/form-data
```

### Create Project from Audio
```http
POST /api/projects/audio
Authorization: Bearer <token>
Content-Type: multipart/form-data
```

---

## Health Check

```http
GET /health
```
Returns health status of the application and dependencies.

```http
GET /health/live
```
Returns liveness probe status.

**Note:** Health endpoints are available on the management port (9090) in production.

---

## SignalR Chat Hub (`/chatHub`)

Real-time bidirectional communication for chat functionality.

### Connection
```
ws://localhost:5210/chatHub?sessionId=<session_id>&access_token=<jwt_token>
```

**Query Parameters:**
- `sessionId` (required): The chat session identifier
- `access_token` (required): JWT authentication token

### Client → Server Methods

#### SendMessage
Sends a message to the AI agent and receives a streaming response.

```typescript
invoke('SendMessage', sessionId: string, message: string, byTokens?: boolean, agentId?: string, teamId?: string)
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `sessionId` | string | Yes | Chat session identifier |
| `message` | string | Yes | User message content (max 10,000 chars) |
| `byTokens` | boolean | No | Calculate credits by token count (default: false) |
| `agentId` | GUID | No | Override session's default agent |
| `teamId` | GUID | No | Team context for team-based chat |

#### GetHistory
Retrieves the conversation history for a session.

```typescript
invoke('GetHistory', sessionId: string): Promise<ChatMessage[]>
```

### Server → Client Events

#### ReceiveMessage
Receives a message from the bot.

```typescript
on('ReceiveMessage', (sender: 'bot' | 'user', message: string) => void)
```

#### ReceiveActivity
Receives activity events (tool invocations, status updates, etc.).

```typescript
on('ReceiveActivity', (activity: { type: string, payload?: object }) => void)
```

**Activity Types:**
- `user_message` - User sent a message
- `assistant_message` - AI response completed
- `user_connected` - User joined the session
- `user_disconnected` - User left the session
- `tool_invocation` - AI tool/plugin was invoked

### Rate Limits
- 10 messages per minute per user

---

## SignalR Chat Hub V2 (`/chatHubV2`)

Enhanced chat hub with cursor-based pagination and artifacts support.

---

## SignalR Credit Hub (`/hubs/credits`)

Real-time credit balance updates.

### Events
- `CreditBalanceUpdated` - User's credit balance has changed
- `LowCreditWarning` - User's credits are below threshold

---

## SignalR Notification Hub (`/notificationHub`)

General notifications for the user.

---

## Error Response Format

```json
{
  "error": "Error message",
  "details": "Optional detailed message"
}
```

## Rate Limit Response

```http
HTTP/1.1 429 Too Many Requests
Retry-After: <seconds>

{
  "error": "Too many requests. Please try again later.",
  "retryAfter": <seconds>
}
```

---

## OpenAPI Documentation

Interactive API documentation is available at `/swagger` when running in development mode.

For production API documentation, the OpenAPI spec can be accessed at `/swagger/v1/swagger.json`.
