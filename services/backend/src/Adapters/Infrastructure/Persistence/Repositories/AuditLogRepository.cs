using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class AuditLogRepository(ApplicationDbContext db) : IAuditLogRepository
{
    private readonly ApplicationDbContext _db = db;

    public async Task<AuditLogEntity> CreateAsync(AuditLogEntity auditLog, CancellationToken cancellationToken = default)
    {
        _db.AuditLogs.Add(auditLog);
        await _db.SaveChangesAsync(cancellationToken);
        return auditLog;
    }

    public async Task<IReadOnlyList<AuditLogEntity>> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
    {
        var queryable = _db.AuditLogs.AsNoTracking();

        if (query.UserId.HasValue)
            queryable = queryable.Where(a => a.UserId == query.UserId.Value);

        if (!string.IsNullOrEmpty(query.ActionType))
            queryable = queryable.Where(a => a.ActionType == query.ActionType);

        if (!string.IsNullOrEmpty(query.EntityType))
            queryable = queryable.Where(a => a.EntityType == query.EntityType);

        if (query.StartDate.HasValue)
            queryable = queryable.Where(a => a.Timestamp >= query.StartDate.Value);

        if (query.EndDate.HasValue)
            queryable = queryable.Where(a => a.Timestamp <= query.EndDate.Value);

        if (query.SuccessOnly.HasValue)
            queryable = queryable.Where(a => a.Success == query.SuccessOnly.Value);

        if (query.MinSeverity.HasValue)
            queryable = queryable.Where(a => a.Severity >= query.MinSeverity.Value);

        return await queryable
            .OrderByDescending(a => a.Timestamp)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<AuditLogEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.AuditLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<int> GetCountAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
    {
        var queryable = _db.AuditLogs.AsNoTracking();

        if (query.UserId.HasValue)
            queryable = queryable.Where(a => a.UserId == query.UserId.Value);

        if (!string.IsNullOrEmpty(query.ActionType))
            queryable = queryable.Where(a => a.ActionType == query.ActionType);

        if (!string.IsNullOrEmpty(query.EntityType))
            queryable = queryable.Where(a => a.EntityType == query.EntityType);

        if (query.StartDate.HasValue)
            queryable = queryable.Where(a => a.Timestamp >= query.StartDate.Value);

        if (query.EndDate.HasValue)
            queryable = queryable.Where(a => a.Timestamp <= query.EndDate.Value);

        if (query.SuccessOnly.HasValue)
            queryable = queryable.Where(a => a.Success == query.SuccessOnly.Value);

        if (query.MinSeverity.HasValue)
            queryable = queryable.Where(a => a.Severity >= query.MinSeverity.Value);

        return await queryable.CountAsync(cancellationToken);
    }
}