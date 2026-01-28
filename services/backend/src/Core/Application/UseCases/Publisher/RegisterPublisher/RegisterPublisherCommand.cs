using Application.UseCases.Publisher.Common;
using MediatR;

namespace Application.UseCases.Publisher.RegisterPublisher;

public record RegisterPublisherCommand(
    Guid UserId,
    string Name,
    string? Bio,
    string? WebsiteUrl,
    string? LogoUrl,
    string? SupportEmail
) : IRequest<PublisherDto>;
