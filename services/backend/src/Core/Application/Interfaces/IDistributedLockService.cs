namespace Application.Interfaces;

/// <summary>
/// Service for managing distributed locks across multiple instances
/// </summary>
public interface IDistributedLockService
{
    /// <summary>
    /// Acquires a distributed lock for the specified key
    /// </summary>
    /// <param name="key">The lock key</param>
    /// <param name="expiry">Lock expiration time</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A disposable lock handle, or null if lock could not be acquired</returns>
    Task<IDistributedLock?> AcquireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Tries to acquire a distributed lock with retry logic
    /// </summary>
    /// <param name="key">The lock key</param>
    /// <param name="expiry">Lock expiration time</param>
    /// <param name="timeout">Maximum time to wait for the lock</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A disposable lock handle</returns>
    /// <exception cref="TimeoutException">Thrown if lock cannot be acquired within timeout</exception>
    Task<IDistributedLock> WaitAsync(string key, TimeSpan expiry, TimeSpan timeout, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an acquired distributed lock
/// </summary>
public interface IDistributedLock : IAsyncDisposable
{
    /// <summary>
    /// The key associated with this lock
    /// </summary>
    string Key { get; }
    
    /// <summary>
    /// Indicates whether the lock is still held
    /// </summary>
    bool IsHeld { get; }
    
    /// <summary>
    /// Extends the lock expiration
    /// </summary>
    /// <param name="extension">Additional time to hold the lock</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ExtendAsync(TimeSpan extension, CancellationToken cancellationToken = default);
}