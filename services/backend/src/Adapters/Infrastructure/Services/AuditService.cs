using System.Text.Json;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public AuditService(
        IUnitOfWork unitOfWork,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditService> logger)
    {
        _unitOfWork = unitOfWork;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            
            var auditLog = new AuditLogEntity
            {
                UserId = entry.UserId ?? GetCurrentUserId(),
                ActionType = entry.ActionType,
                EntityType = entry.EntityType,
                EntityId = entry.EntityId,
                OldValues = SerializeValues(entry.OldValues),
                NewValues = SerializeValues(entry.NewValues),
                Success = entry.Success,
                ErrorMessage = entry.ErrorMessage,
                IpAddress = entry.IpAddress ?? GetClientIpAddress(httpContext),
                UserAgent = entry.UserAgent ?? GetUserAgent(httpContext),
                Metadata = SerializeValues(entry.Metadata),
                Timestamp = DateTime.UtcNow,
                Severity = entry.Severity
            };

            await _unitOfWork.AuditLogs.CreateAsync(auditLog, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            
            // Log critical events to application logs as well
            if (entry.Severity == AuditSeverity.Critical)
            {
                _logger.LogWarning("Critical audit event: {ActionType} on {EntityType} {EntityId} by user {UserId}", 
                    entry.ActionType, entry.EntityType, entry.EntityId, entry.UserId);
            }
        }
        catch (Exception ex)
        {
            // Don't let audit logging failures break the application
            _logger.LogError(ex, "Failed to write audit log for action {ActionType}", entry.ActionType);
        }
    }

    public async Task LogSuccessAsync(
        string actionType,
        string entityType,
        string? entityId = null,
        object? oldValues = null,
        object? newValues = null,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        await LogAsync(new AuditLogEntry
        {
            ActionType = actionType,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues,
            NewValues = newValues,
            Success = true,
            Metadata = metadata,
            Severity = DetermineSeverity(actionType)
        }, cancellationToken);
    }

    public async Task LogFailureAsync(
        string actionType,
        string entityType,
        string? entityId,
        string errorMessage,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        await LogAsync(new AuditLogEntry
        {
            ActionType = actionType,
            EntityType = entityType,
            EntityId = entityId,
            Success = false,
            ErrorMessage = errorMessage,
            Metadata = metadata,
            Severity = DetermineSeverity(actionType, isFailure: true)
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLogEntity>> QueryAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.AuditLogs.QueryAsync(query, cancellationToken);
    }

    private Guid? GetCurrentUserId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = httpContext.User.FindFirst("sub") 
                ?? httpContext.User.FindFirst("userId")
                ?? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                
            if (Guid.TryParse(userIdClaim?.Value, out var userId))
            {
                return userId;
            }
        }
        return null;
    }

    private string? GetClientIpAddress(HttpContext? httpContext)
    {
        if (httpContext == null) return null;
        
        // Check for forwarded IP first (when behind proxy/load balancer)
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }
        
        // Check X-Real-IP header
        var realIp = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }
        
        // Fall back to remote IP
        return httpContext.Connection.RemoteIpAddress?.ToString();
    }

    private string? GetUserAgent(HttpContext? httpContext)
    {
        return httpContext?.Request.Headers["User-Agent"].FirstOrDefault();
    }

    private string? SerializeValues(object? values)
    {
        if (values == null) return null;
        
        try
        {
            return JsonSerializer.Serialize(values, _jsonOptions);
        }
        catch
        {
            return values.ToString();
        }
    }

    private AuditSeverity DetermineSeverity(string actionType, bool isFailure = false)
    {
        // Critical actions
        var criticalActions = new[]
        {
            AuditActionTypes.UserDelete,
            AuditActionTypes.PasswordReset,
            AuditActionTypes.CreditLimitExceeded,
            AuditActionTypes.ChatAccessDenied
        };
        
        if (criticalActions.Contains(actionType))
            return AuditSeverity.Critical;
            
        // Failures are generally warnings
        if (isFailure)
            return AuditSeverity.Warning;
            
        // Everything else is info
        return AuditSeverity.Info;
    }
}