using MediatR;

namespace Application.UseCases.Social.UnlinkSocialIdentity;

/// <summary>
/// Command to unlink a social identity from a user account.
/// </summary>
public record UnlinkSocialIdentityCommand(
    Guid UserId,
    string Provider) : IRequest<Unit>;
