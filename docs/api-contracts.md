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

### List Agents
```http
GET /api/agents
Authorization: Bearer <token>
```

### Get Agent
```http
GET /api/agents/{id}
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
  "pluginIds": ["guid"],
  "isPublic": false
}
```

### Update Agent
```http
PUT /api/agents/{id}
Authorization: Bearer <token>
```

### Delete Agent (Soft Delete)
```http
DELETE /api/agents/{id}
Authorization: Bearer <token>
```

### Add Plugin to Agent
```http
POST /api/agents/{agentId}/plugins/{pluginId}
Authorization: Bearer <token>
```

### Remove Plugin from Agent
```http
DELETE /api/agents/{agentId}/plugins/{pluginId}
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

## Spreadsheet Controller (`/api/spreadsheet`)

### Upload Spreadsheet
```http
POST /api/spreadsheet/upload
Authorization: Bearer <token>
Content-Type: multipart/form-data

file: <binary>
```

### Ask Question
```http
POST /api/spreadsheet/ask
Authorization: Bearer <token>
Content-Type: application/json

{
  "workbookId": "guid",
  "question": "string"
}
```

### Get Workbook Summary
```http
GET /api/spreadsheet/{id}/summary
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

## Projects Controller (`/api/projects`)

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

## Plugins Controller (`/api/plugins`)

### List User Plugins
```http
GET /api/plugins
Authorization: Bearer <token>
```

### Create Plugin
```http
POST /api/plugins
Authorization: Bearer <token>
```

### Toggle Plugin
```http
PUT /api/plugins/{id}/toggle
Authorization: Bearer <token>
```

### Delete Plugin
```http
DELETE /api/plugins/{id}
Authorization: Bearer <token>
```

---

## Marketplace Controller (`/api/marketplace`)

### List Marketplace Items
```http
GET /api/marketplace
Authorization: Bearer <token>
```

### Get User Marketplace Items
```http
GET /api/marketplace/user
Authorization: Bearer <token>
```

### Purchase Plugin
```http
POST /api/marketplace/purchase/{pluginId}
Authorization: Bearer <token>
```

---

## Subscription Controller (`/api/subscription`)

### Get Current Subscription
```http
GET /api/subscription
Authorization: Bearer <token>
```

### Get Subscription Plans
```http
GET /api/subscription/plans
```

### Create/Update Subscription
```http
POST /api/subscription
Authorization: Bearer <token>
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
```

### Get Security Metrics
```http
GET /api/dashboard/security
Authorization: Bearer <token>
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

## Health Check

```http
GET /health
```
Returns health status of the application and dependencies.

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

**Errors:**
- Invalid session ID
- Empty message
- Message too long (>10,000 chars)
- User not authenticated
- Rate limit exceeded (10 msg/min)
- Insufficient credits
- Session not found
- Access denied (wrong user)
- No agent selected
- Agent access denied

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
- Configurable via `CreditSystemConfiguration`

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
