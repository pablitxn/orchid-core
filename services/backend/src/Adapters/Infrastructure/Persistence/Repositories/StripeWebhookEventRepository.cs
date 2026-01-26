using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class StripeWebhookEventRepository(ApplicationDbContext db) : IStripeWebhookEventRepository
{
    private readonly ApplicationDbContext _db = db;

    public async Task<bool> ExistsAsync(string stripeEventId, CancellationToken cancellationToken = default)
    {
        return await _db.StripeWebhookEvents
            .AnyAsync(e => e.StripeEventId == stripeEventId, cancellationToken);
    }

    public async Task<StripeWebhookEventEntity?> GetByStripeEventIdAsync(string stripeEventId, CancellationToken cancellationToken = default)
    {
        return await _db.StripeWebhookEvents
            .FirstOrDefaultAsync(e => e.StripeEventId == stripeEventId, cancellationToken);
    }

    public async Task<StripeWebhookEventEntity> CreateAsync(StripeWebhookEventEntity webhookEvent, CancellationToken cancellationToken = default)
    {
        _db.StripeWebhookEvents.Add(webhookEvent);
        await _db.SaveChangesAsync(cancellationToken);
        return webhookEvent;
    }

    public async Task UpdateAsync(StripeWebhookEventEntity webhookEvent, CancellationToken cancellationToken = default)
    {
        var entry = _db.Entry(webhookEvent);
        if (entry.State == EntityState.Detached)
        {
            _db.StripeWebhookEvents.Attach(webhookEvent);
            entry.State = EntityState.Modified;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<StripeWebhookEventEntity>> GetRetryableEventsAsync(int maxRetries = 3, int limit = 100, CancellationToken cancellationToken = default)
    {
        return await _db.StripeWebhookEvents
            .Where(e => e.Status == WebhookEventStatus.Failed && e.RetryCount < maxRetries)
            .OrderBy(e => e.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<StripeWebhookEventEntity>> GetEventsReadyForRetryAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _db.StripeWebhookEvents
            .Where(e => e.Status == WebhookEventStatus.Failed
                        && e.NextRetryAt != null
                        && e.NextRetryAt <= now)
            .OrderBy(e => e.NextRetryAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<StripeWebhookEventEntity>> GetDeadLetterEventsAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        return await _db.StripeWebhookEvents
            .Where(e => e.Status == WebhookEventStatus.DeadLetter)
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<WebhookEventStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var stats = await _db.StripeWebhookEvents
            .GroupBy(_ => 1)
            .Select(g => new WebhookEventStats(
                g.Count(),
                g.Count(e => e.Status == WebhookEventStatus.Processed),
                g.Count(e => e.Status == WebhookEventStatus.Failed),
                g.Count(e => e.Status == WebhookEventStatus.DeadLetter),
                g.Count(e => e.Status == WebhookEventStatus.Failed && e.NextRetryAt != null && e.NextRetryAt <= now)
            ))
            .FirstOrDefaultAsync(cancellationToken);

        return stats ?? new WebhookEventStats(0, 0, 0, 0, 0);
    }

    public async Task<StripeWebhookEventEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.StripeWebhookEvents
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<(IEnumerable<StripeWebhookEventEntity> Items, int TotalCount)> GetAllAsync(
        WebhookEventStatus? status = null,
        string? eventType = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = _db.StripeWebhookEvents.AsNoTracking();

        if (status.HasValue)
            query = query.Where(e => e.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(eventType))
            query = query.Where(e => e.EventType.Contains(eventType));

        if (startDate.HasValue)
            query = query.Where(e => e.CreatedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(e => e.CreatedAt <= endDate.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
