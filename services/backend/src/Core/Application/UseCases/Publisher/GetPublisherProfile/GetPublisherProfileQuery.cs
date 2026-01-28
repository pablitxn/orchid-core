using Application.UseCases.Publisher.Common;
using MediatR;

namespace Application.UseCases.Publisher.GetPublisherProfile;

public record GetPublisherProfileQuery(Guid UserId) : IRequest<PublisherDto?>;
