using Domain.Entities;

namespace Application.Interfaces;

/// <summary>
/// Service for logging audit trails of sensitive operations
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Logs an audit entry
    /// </summary>
    Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Logs a successful operation
    /// </summary>
    Task LogSuccessAsync(
        string actionType,
        string entityType,
        string? entityId = null,
        object? oldValues = null,
        object? newValues = null,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Logs a failed operation
    /// </summary>
    Task LogFailureAsync(
        string actionType,
        string entityType,
        string? entityId,
        string errorMessage,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Queries audit logs
    /// </summary>
    Task<IReadOnlyList<AuditLogEntity>> QueryAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Entry for creating an audit log
/// </summary>
public class AuditLogEntry
{
    public Guid? UserId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public object? OldValues { get; set; }
    public object? NewValues { get; set; }
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public AuditSeverity Severity { get; set; } = AuditSeverity.Info;
}

/// <summary>
/// Query parameters for audit logs
/// </summary>
public class AuditLogQuery
{
    public Guid? UserId { get; set; }
    public string? ActionType { get; set; }
    public string? EntityType { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool? SuccessOnly { get; set; }
    public AuditSeverity? MinSeverity { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}