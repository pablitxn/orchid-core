namespace Application.Interfaces;

/// <summary>
/// Provides status information about database seeding operations.
/// Used to track whether initial data seeding has completed.
/// </summary>
public interface ISeedingStatusService
{
    /// <summary>
    /// Gets whether the database seeding has completed.
    /// </summary>
    bool IsSeedingComplete { get; }

    /// <summary>
    /// Gets any error that occurred during seeding, or null if no error.
    /// </summary>
    Exception? SeedingError { get; }

    /// <summary>
    /// Marks the seeding as complete.
    /// </summary>
    void MarkComplete();

    /// <summary>
    /// Marks the seeding as failed with the specified error.
    /// </summary>
    void MarkFailed(Exception error);

    /// <summary>
    /// Waits for seeding to complete (or fail).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when seeding is done.</returns>
    Task WaitForSeedingAsync(CancellationToken cancellationToken = default);
}
