using Application.Interfaces;
using Application.UseCases.Publisher.Common;
using MediatR;

namespace Application.UseCases.Publisher.GetPublisherProfile;

public class GetPublisherProfileHandler(
    IPublisherRepository publisherRepository,
    IPluginRepository pluginRepository
) : IRequestHandler<GetPublisherProfileQuery, PublisherDto?>
{
    private readonly IPublisherRepository _publisherRepository = publisherRepository;
    private readonly IPluginRepository _pluginRepository = pluginRepository;

    public async Task<PublisherDto?> Handle(GetPublisherProfileQuery request, CancellationToken cancellationToken)
    {
        var publisher = await _publisherRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (publisher is null)
        {
            return null;
        }

        // Get plugin count for this publisher
        var plugins = await _pluginRepository.ListAsync(cancellationToken);
        var pluginCount = plugins.Count(p => p.PublisherId == publisher.Id);

        return PublisherDto.FromEntity(publisher, pluginCount);
    }
}
