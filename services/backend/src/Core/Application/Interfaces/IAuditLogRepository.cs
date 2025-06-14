using Domain.Entities;

namespace Application.Interfaces;

public interface IAuditLogRepository
{
    Task<AuditLogEntity> CreateAsync(AuditLogEntity auditLog, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLogEntity>> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken = default);
    Task<AuditLogEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(AuditLogQuery query, CancellationToken cancellationToken = default);
}