# Logging Standards Guide

This document provides guidelines for effective logging in the Orchid Core platform.

## Overview

Orchid Core uses **Serilog** for structured logging with built-in support for:
- Correlation IDs for distributed tracing
- Environment-specific log levels
- Sensitive data protection via `SafeLogger`

## Log Levels

### Level Guidelines

| Level | When to Use | Example |
|-------|-------------|---------|
| `Trace` | Very detailed diagnostic info | Loop iterations, variable values |
| `Debug` | Detailed diagnostic info | Method entry/exit, internal state |
| `Information` | Normal operation events | Request received, action completed |
| `Warning` | Unexpected but recoverable | Retry attempt, cache miss, deprecated usage |
| `Error` | Errors that affect the current operation | Failed API call, validation error |
| `Critical` | System-wide failures | Unrecoverable database error, startup failure |

### Environment Configuration

The following table shows the default log levels per environment:

| Environment | Application Logs | Framework Logs | Description |
|-------------|-----------------|----------------|-------------|
| Development | Information | Warning | Verbose application logs for debugging, minimal framework noise |
| Production | Information | Warning | Application events visible, framework noise suppressed |

**Note**: Application logs (`Infrastructure.*`, `Application.*`, `WebApi.*`) are kept at `Information` level in both environments to capture important business events. Framework logs (Microsoft.*, System.*, MassTransit) are suppressed to `Warning` to reduce noise.

### Namespace-Specific Overrides

The following namespaces are configured to reduce framework noise:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.AspNetCore.SignalR": "Warning",
      "Microsoft.AspNetCore.Http.Connections": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning",
      "System.Net.Http.HttpClient": "Warning",
      "MassTransit": "Warning",
      "Infrastructure": "Information",
      "Application": "Information",
      "WebApi": "Information"
    }
  }
}
```

### Enabling Debug Mode

For troubleshooting specific areas, copy these overrides to your `appsettings.local.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore.Database.Command": "Information",
      "Microsoft.AspNetCore.SignalR": "Debug",
      "Microsoft.AspNetCore.Http.Connections": "Debug",
      "MassTransit": "Debug"
    }
  }
}
```

## Correlation IDs

Every HTTP request is automatically assigned a **Correlation ID** for distributed tracing.

### How It Works

1. **Incoming requests**: Correlation ID is extracted from `X-Correlation-ID` header or auto-generated
2. **Log entries**: All logs include `{CorrelationId}` in the output
3. **Outgoing calls**: Correlation ID is propagated to HTTP clients and message queues
4. **Response headers**: Correlation ID is returned in response headers for debugging

### Log Output Format

```
[HH:mm:ss LVL] [CorrelationId] Message
[14:32:15 INF] [abc123def456] User login successful for userId: 12345
```

### Accessing Correlation ID in Code

```csharp
// Via dependency injection
public class MyService(ICorrelationIdAccessor correlationIdAccessor)
{
    public void DoSomething()
    {
        var correlationId = correlationIdAccessor.CorrelationId;
        // Use for custom logging or external calls
    }
}
```

## Structured Logging Best Practices

### DO: Use Message Templates

```csharp
// ✅ Good - structured logging with template
_logger.LogInformation("User {UserId} logged in from {IpAddress}", userId, ipAddress);

// ❌ Bad - string interpolation loses structure
_logger.LogInformation($"User {userId} logged in from {ipAddress}");
```

### DO: Include Relevant Context

```csharp
// ✅ Good - includes traceable identifiers
_logger.LogWarning("Credit consumption failed for user {UserId} in session {SessionId}", 
    userId, sessionId);

// ❌ Bad - missing context
_logger.LogWarning("Credit consumption failed");
```

### DO: Use Appropriate Log Levels

```csharp
// ✅ Good - appropriate level for the situation
_logger.LogDebug("Attempting to fetch user {UserId} from cache", userId);
_logger.LogInformation("User {UserId} registration completed", userId);
_logger.LogWarning("Cache miss for user {UserId}, falling back to database", userId);
_logger.LogError(ex, "Failed to create subscription for user {UserId}", userId);
```

### DON'T: Log Sensitive Data

```csharp
// ❌ Bad - exposes sensitive data
_logger.LogInformation("User login: {Email} with password {Password}", email, password);

// ✅ Good - no sensitive data
_logger.LogInformation("User login attempt for {Email}", email);
```

## Sensitive Data Protection

### SafeLogger

For logging that might contain user data, use the `ISafeLogger` interface:

```csharp
public class MyHandler(ISafeLogger safeLogger)
{
    public void Handle()
    {
        // Automatically redacts emails, phone numbers, credit cards, SSNs, IPs
        safeLogger.LogSafe(LogLevel.Information, "Processing user: {UserData}", userData);
    }
}
```

### What Gets Redacted

| Data Type | Pattern | Replacement |
|-----------|---------|-------------|
| Email | `user@example.com` | `***@***.***` |
| Phone | `(555) 123-4567` | `***-***-****` |
| Credit Card | `4111-1111-1111-1111` | `****-****-****-****` |
| SSN | `123-45-6789` | `***-**-****` |
| IP Address | `192.168.1.100` | `***.***.***.***` |

### Production Security

⚠️ **Important**: The following are automatically disabled in production:

1. **Entity Framework Sensitive Data Logging** - Parameter values in SQL logs are hidden
2. **Detailed Error Messages** - Internal exceptions are not exposed to clients

**Verification**: To verify sensitive data logging is properly disabled in production:
- Check that `ASPNETCORE_ENVIRONMENT` is set to `Production` (not `Development`)
- Review startup logs - they should NOT contain "Sensitive data logging is enabled" message
- SQL query logs should show parameter placeholders (e.g., `@p0`) instead of actual values

## Configuration Files

### Environment Hierarchy

Configuration is loaded in this order (later overrides earlier):

1. `appsettings.json` - Base configuration
2. `appsettings.{Environment}.json` - Environment-specific (Development, Production)
3. `appsettings.local.json` - Local developer overrides (not committed)
4. Environment variables
5. User secrets (development only)

### File Purposes

| File | Purpose | Committed |
|------|---------|-----------|
| `appsettings.json` | Base defaults | ✅ Yes |
| `appsettings.Development.json` | Dev-friendly settings | ✅ Yes |
| `appsettings.Production.json` | Prod-safe defaults | ✅ Yes |
| `appsettings.local.json` | Personal overrides | ❌ No |

## Troubleshooting

### Common Issues

| Symptom | Cause | Solution |
|---------|-------|----------|
| Too many SQL logs | EF Core logging at Information | Set `Microsoft.EntityFrameworkCore.Database.Command` to `Warning` |
| SignalR flooding logs | SignalR at Debug level | Set `Microsoft.AspNetCore.SignalR` to `Warning` |
| Missing correlation IDs | Middleware not registered | Ensure `app.UseCorrelationId()` is first in pipeline |
| Sensitive data in logs | SafeLogger not used | Use `ISafeLogger` for user data |

### Verifying Correlation IDs

```bash
# Check response header
curl -v http://localhost:5000/health 2>&1 | grep X-Correlation-ID

# Provide your own correlation ID
curl -H "X-Correlation-ID: my-test-id-123" http://localhost:5000/health
```

## Related Documentation

- [Architecture Guide](./architecture.md) - Overall system architecture
- [Development Guide](./development-guide.md) - Development setup and workflow
- [Pre-Production Checklist](./pre-production-checklist.md) - Production readiness checks
