using Application.Interfaces;
using Application.UseCases.Publisher.Common;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Publisher.UpdatePublisherProfile;

public class UpdatePublisherProfileHandler(
    IPublisherRepository publisherRepository,
    IPluginRepository pluginRepository,
    ILogger<UpdatePublisherProfileHandler> logger
) : IRequestHandler<UpdatePublisherProfileCommand, PublisherDto>
{
    private readonly IPublisherRepository _publisherRepository = publisherRepository;
    private readonly IPluginRepository _pluginRepository = pluginRepository;
    private readonly ILogger<UpdatePublisherProfileHandler> _logger = logger;

    public async Task<PublisherDto> Handle(UpdatePublisherProfileCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating publisher profile for user {UserId}", request.UserId);

        var publisher = await _publisherRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (publisher is null)
        {
            throw new PublisherNotFoundException($"No publisher found for user '{request.UserId}'.");
        }

        // Update fields if provided
        if (!string.IsNullOrWhiteSpace(request.Name))
            publisher.Name = request.Name;
        if (request.Bio is not null)
            publisher.Bio = request.Bio;
        if (request.WebsiteUrl is not null)
            publisher.WebsiteUrl = request.WebsiteUrl;
        if (request.LogoUrl is not null)
            publisher.LogoUrl = request.LogoUrl;
        if (request.SupportEmail is not null)
            publisher.SupportEmail = request.SupportEmail;

        await _publisherRepository.UpdateAsync(publisher, cancellationToken);

        _logger.LogInformation("Publisher {PublisherId} profile updated", publisher.Id);

        // Get plugin count for this publisher
        var plugins = await _pluginRepository.ListAsync(cancellationToken);
        var pluginCount = plugins.Count(p => p.PublisherId == publisher.Id);

        return PublisherDto.FromEntity(publisher, pluginCount);
    }
}
