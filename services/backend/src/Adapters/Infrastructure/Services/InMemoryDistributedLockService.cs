using System.Collections.Concurrent;
using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// In-memory implementation of IDistributedLockService for single-instance scenarios (development)
/// </summary>
public class InMemoryDistributedLockService : IDistributedLockService
{
    private readonly ConcurrentDictionary<string, LockEntry> _locks = new();
    private readonly ILogger<InMemoryDistributedLockService> _logger;

    public InMemoryDistributedLockService(ILogger<InMemoryDistributedLockService> logger)
    {
        _logger = logger;
    }

    public Task<IDistributedLock?> AcquireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var entry = new LockEntry(Guid.NewGuid().ToString(), now.Add(expiry));

        // Try to add or update if expired
        var added = _locks.AddOrUpdate(
            key,
            entry,
            (k, existing) => existing.ExpiresAt < now ? entry : existing
        );

        if (added.Id == entry.Id)
        {
            _logger.LogDebug("Acquired in-memory lock for key: {Key}", key);
            return Task.FromResult<IDistributedLock?>(new InMemoryDistributedLock(this, key, entry.Id));
        }

        return Task.FromResult<IDistributedLock?>(null);
    }

    public async Task<IDistributedLock> WaitAsync(string key, TimeSpan expiry, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        var delay = TimeSpan.FromMilliseconds(50);

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var lockHandle = await AcquireAsync(key, expiry, cancellationToken);
            if (lockHandle != null)
            {
                return lockHandle;
            }

            await Task.Delay(delay, cancellationToken);
            delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 1.5, 500));
        }

        throw new TimeoutException($"Could not acquire lock for key '{key}' within {timeout}");
    }

    internal bool TryRelease(string key, string lockId)
    {
        if (_locks.TryGetValue(key, out var entry) && entry.Id == lockId)
        {
            _locks.TryRemove(key, out _);
            _logger.LogDebug("Released in-memory lock for key: {Key}", key);
            return true;
        }
        return false;
    }

    internal bool TryExtend(string key, string lockId, TimeSpan extension)
    {
        if (_locks.TryGetValue(key, out var entry) && entry.Id == lockId)
        {
            var newEntry = new LockEntry(lockId, DateTime.UtcNow.Add(extension));
            return _locks.TryUpdate(key, newEntry, entry);
        }
        return false;
    }

    private record LockEntry(string Id, DateTime ExpiresAt);

    private class InMemoryDistributedLock : IDistributedLock
    {
        private readonly InMemoryDistributedLockService _service;
        private readonly string _key;
        private readonly string _lockId;
        private bool _disposed;

        public InMemoryDistributedLock(InMemoryDistributedLockService service, string key, string lockId)
        {
            _service = service;
            _key = key;
            _lockId = lockId;
            IsHeld = true;
        }

        public string Key => _key;
        public bool IsHeld { get; private set; }

        public Task ExtendAsync(TimeSpan extension, CancellationToken cancellationToken = default)
        {
            if (!IsHeld || _disposed)
                throw new InvalidOperationException("Lock is no longer held");

            if (!_service.TryExtend(_key, _lockId, extension))
            {
                IsHeld = false;
                throw new InvalidOperationException("Lock was lost");
            }

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            if (_disposed)
                return ValueTask.CompletedTask;

            _disposed = true;

            if (IsHeld)
            {
                _service.TryRelease(_key, _lockId);
                IsHeld = false;
            }

            return ValueTask.CompletedTask;
        }
    }
}
