using Application.Interfaces;

namespace Infrastructure.Persistence.Seeders;

/// <summary>
/// Tracks the status of database seeding operations.
/// Thread-safe singleton that allows components to check if seeding is complete.
/// </summary>
public sealed class SeedingStatusService : ISeedingStatusService
{
    private readonly TaskCompletionSource _completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile bool _isComplete;
    private Exception? _error;

    public bool IsSeedingComplete => _isComplete;

    public Exception? SeedingError => _error;

    public void MarkComplete()
    {
        _isComplete = true;
        _completionSource.TrySetResult();
    }

    public void MarkFailed(Exception error)
    {
        _error = error;
        _isComplete = true;
        _completionSource.TrySetException(error);
    }

    public async Task WaitForSeedingAsync(CancellationToken cancellationToken = default)
    {
        using var registration = cancellationToken.Register(() => _completionSource.TrySetCanceled(cancellationToken));
        await _completionSource.Task;
    }
}
