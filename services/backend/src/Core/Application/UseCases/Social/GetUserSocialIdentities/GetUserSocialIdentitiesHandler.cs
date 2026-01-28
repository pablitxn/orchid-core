using Application.Interfaces;
using MediatR;

namespace Application.UseCases.Social.GetUserSocialIdentities;

/// <summary>
/// Handler for getting all linked social identities for a user.
/// </summary>
public class GetUserSocialIdentitiesHandler(
    ISocialIdentityRepository socialIdentityRepository)
    : IRequestHandler<GetUserSocialIdentitiesQuery, IReadOnlyList<SocialIdentityDto>>
{
    public async Task<IReadOnlyList<SocialIdentityDto>> Handle(
        GetUserSocialIdentitiesQuery request,
        CancellationToken cancellationToken)
    {
        var identities = await socialIdentityRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        return identities
            .Where(i => i.UnlinkedAt == null)
            .Select(i => new SocialIdentityDto(
                i.Id,
                i.Provider,
                i.Email,
                i.DisplayName,
                i.AvatarUrl,
                i.IsPrimary,
                i.LinkedAt,
                i.LastUsedAt))
            .ToList();
    }
}
