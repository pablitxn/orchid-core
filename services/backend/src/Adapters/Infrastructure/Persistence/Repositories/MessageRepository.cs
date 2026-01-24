using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Core.Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;

namespace Infrastructure.Persistence.Repositories;

public sealed class MessageRepository : IMessageRepository
{
    private readonly ApplicationDbContext _context;

    public MessageRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    #region V1 Methods (Legacy Placeholders)

    public async Task<int> GetCountBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        // Legacy V1 placeholder - now delegating to V2 implementation
        return await GetCountBySessionIdV2Async(sessionId, cancellationToken);
    }

    public async Task<List<DateTime>> GetMessageTimestampsBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        // Get timestamps from V2 messages
        return await _context.MessagesV2
            .Where(m => m.SessionId == sessionId)
            .OrderByDescending(m => m.Timestamp)
            .Select(m => m.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<int, int>> GetMessagesByHourAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var startTimestamp = new DateTimeOffset(startDate).ToUnixTimeMilliseconds();
        var endTimestamp = new DateTimeOffset(endDate).ToUnixTimeMilliseconds();

        var messages = await _context.MessagesV2
            .Where(m => m.ChatSession != null && m.ChatSession.UserId == userId)
            .Where(m => m.Timestamp >= startTimestamp && m.Timestamp <= endTimestamp)
            .Select(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        var result = new Dictionary<int, int>();
        for (int hour = 0; hour < 24; hour++)
        {
            result[hour] = 0;
        }

        foreach (var timestamp in messages)
        {
            var hour = timestamp.Hour;
            result[hour]++;
        }

        return result;
    }

    #endregion

    #region V2 Methods

    public async Task<MessageV2Entity> CreateAsync(MessageV2Entity message, CancellationToken cancellationToken = default)
    {
        _context.MessagesV2.Add(message);
        await _context.SaveChangesAsync(cancellationToken);
        return message;
    }

    public async Task<MessageV2Entity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.MessagesV2
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<List<MessageV2Entity>> GetBySessionIdAsync(
        string sessionId,
        int limit = 50,
        long? beforeTimestamp = null,
        long? afterTimestamp = null,
        Guid? cursorMessageId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.MessagesV2
            .Where(m => m.SessionId == sessionId);

        // Apply cursor-based pagination
        if (beforeTimestamp.HasValue)
        {
            if (cursorMessageId.HasValue)
            {
                // For tie-breaking: get messages before the cursor
                // Either older timestamp, or same timestamp with smaller ID
                query = query.Where(m =>
                    m.Timestamp < beforeTimestamp.Value ||
                    (m.Timestamp == beforeTimestamp.Value && m.Id.CompareTo(cursorMessageId.Value) < 0));
            }
            else
            {
                query = query.Where(m => m.Timestamp < beforeTimestamp.Value);
            }

            // Order by timestamp descending for backward pagination
            query = query
                .OrderByDescending(m => m.Timestamp)
                .ThenByDescending(m => m.Id);
        }
        else if (afterTimestamp.HasValue)
        {
            if (cursorMessageId.HasValue)
            {
                // For tie-breaking: get messages after the cursor
                query = query.Where(m =>
                    m.Timestamp > afterTimestamp.Value ||
                    (m.Timestamp == afterTimestamp.Value && m.Id.CompareTo(cursorMessageId.Value) > 0));
            }
            else
            {
                query = query.Where(m => m.Timestamp > afterTimestamp.Value);
            }

            // Order by timestamp ascending for forward pagination
            query = query
                .OrderBy(m => m.Timestamp)
                .ThenBy(m => m.Id);
        }
        else
        {
            // Default: get most recent messages
            query = query
                .OrderByDescending(m => m.Timestamp)
                .ThenByDescending(m => m.Id);
        }

        return await query
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountBySessionIdV2Async(string sessionId, CancellationToken cancellationToken = default)
    {
        return await _context.MessagesV2
            .CountAsync(m => m.SessionId == sessionId, cancellationToken);
    }

    public async Task UpdateAsync(MessageV2Entity message, CancellationToken cancellationToken = default)
    {
        message.UpdatedAt = DateTime.UtcNow;
        _context.MessagesV2.Update(message);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var message = await _context.MessagesV2.FindAsync(new object[] { id }, cancellationToken);
        if (message != null)
        {
            _context.MessagesV2.Remove(message);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task BulkCreateAsync(IEnumerable<MessageV2Entity> messages, CancellationToken cancellationToken = default)
    {
        await _context.MessagesV2.AddRangeAsync(messages, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    #endregion
}
