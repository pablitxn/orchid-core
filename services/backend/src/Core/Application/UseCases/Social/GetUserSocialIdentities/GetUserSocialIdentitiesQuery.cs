using MediatR;

namespace Application.UseCases.Social.GetUserSocialIdentities;

/// <summary>
/// Query to get all linked social identities for a user.
/// </summary>
public record GetUserSocialIdentitiesQuery(Guid UserId) : IRequest<IReadOnlyList<SocialIdentityDto>>;
