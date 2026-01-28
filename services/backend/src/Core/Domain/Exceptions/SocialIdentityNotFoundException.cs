namespace Domain.Exceptions;

/// <summary>
/// Exception thrown when a social identity is not found.
/// </summary>
public class SocialIdentityNotFoundException : EntityNotFoundException
{
    public SocialIdentityNotFoundException(Guid identityId)
        : base("SocialIdentity", identityId.ToString()) { }

    public SocialIdentityNotFoundException(string provider, Guid userId)
        : base($"Social identity for provider '{provider}' not found for user '{userId}'.") { }
}
