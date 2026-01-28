namespace Application.Interfaces;

/// <summary>
/// Port for Auth0 user synchronization operations.
/// </summary>
public interface IAuth0SyncService
{
    /// <summary>
    /// Synchronizes an Auth0 user with the local database.
    /// Creates user if not exists, links and updates if exists.
    /// </summary>
    Task<Auth0SyncResult> SyncUserAsync(
        Auth0UserData userData,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Data received from Auth0 (webhook or token claims).
/// </summary>
public record Auth0UserData(
    string Auth0UserId,
    string Email,
    string? Name,
    string? AvatarUrl,
    string? Connection,
    IReadOnlyList<string> Roles);

/// <summary>
/// Result of the sync operation.
/// </summary>
public record Auth0SyncResult(
    Guid UserId,
    bool IsNewUser,
    bool WasLinked,
    bool ProfileUpdated,
    bool RolesUpdated);
