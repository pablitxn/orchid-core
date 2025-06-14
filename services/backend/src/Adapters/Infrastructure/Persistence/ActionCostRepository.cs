using System.Text.Json;
using Application.Interfaces;
using Domain.Enums;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of action cost tracking.
/// </summary>
public sealed class ActionCostRepository(ApplicationDbContext context, ILogger<ActionCostRepository> logger)
    : IActionCostRepository
{
    private readonly ApplicationDbContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly ILogger<ActionCostRepository> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task RecordActionCostAsync(
        string actionType,
        decimal cost,
        object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = new ActionCostEntity
            {
                Id = Guid.NewGuid(),
                ActionName = actionType,
                Credits = cost,
                PaymentUnit = metadata is PaymentUnit unit
                    ? unit
                    : (metadata != null && metadata.GetType().GetProperty("tokens", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase) != null)
                        ? PaymentUnit.PerToken
                        : PaymentUnit.PerMessage,
                CreatedAt = DateTime.UtcNow
            };

            _context.ActionCosts.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Recorded cost ${Cost} for action {ActionType}", cost, actionType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record action cost for {ActionType}", actionType);
            throw;
        }
    }

    public async Task<decimal> GetTotalCostAsync(
        string actionType,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        return await _context.ActionCosts
            .Where(ac => ac.ActionName == actionType
                && ac.CreatedAt >= startDate
                && ac.CreatedAt <= endDate)
            .SumAsync(ac => ac.Credits, cancellationToken);
    }

    public async Task<Dictionary<string, decimal>> GetCostBreakdownAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var breakdown = await _context.ActionCosts
            .Where(ac => ac.CreatedAt >= startDate
                && ac.CreatedAt <= endDate)
            .GroupBy(ac => ac.ActionName)
            .Select(g => new { ActionName = g.Key, TotalCost = g.Sum(ac => ac.Credits) })
            .ToDictionaryAsync(x => x.ActionName, x => x.TotalCost, cancellationToken);

        return breakdown;
    }

    public Task<ActionCostEntity?> GetByActionAsync(string requestActionType, CancellationToken cancellationToken)
    {
        return _context.ActionCosts
            .AsNoTracking()
            .Where(a => a.ActionName == requestActionType)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new ActionCostEntity
            {
                Id = a.Id,
                ActionName = a.ActionName,
                Credits = a.Credits,
                PaymentUnit = a.PaymentUnit,
                CreatedAt = a.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken)!;
    }
}

/// <summary>
/// Internal persistence model mapped via EF Core. Kept separate from domain model to avoid
/// leaking database concerns.
/// </summary>
