using Application.UseCases.Publisher.Common;
using MediatR;

namespace Application.UseCases.Publisher.UpdatePublisherProfile;

public record UpdatePublisherProfileCommand(
    Guid UserId,
    string? Name,
    string? Bio,
    string? WebsiteUrl,
    string? LogoUrl,
    string? SupportEmail
) : IRequest<PublisherDto>;
