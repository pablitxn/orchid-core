using Application.Interfaces;
using Application.UseCases.Publisher.Common;
using Domain.Entities;
using Domain.Enums;
using Domain.Events;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Publisher.RegisterPublisher;

public class RegisterPublisherHandler(
    IPublisherRepository publisherRepository,
    IUserRepository userRepository,
    IEventPublisher eventPublisher,
    ILogger<RegisterPublisherHandler> logger
) : IRequestHandler<RegisterPublisherCommand, PublisherDto>
{
    private readonly IPublisherRepository _publisherRepository = publisherRepository;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IEventPublisher _eventPublisher = eventPublisher;
    private readonly ILogger<RegisterPublisherHandler> _logger = logger;

    public async Task<PublisherDto> Handle(RegisterPublisherCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Registering publisher for user {UserId}", request.UserId);

        // Check if user exists
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            throw new UserNotFoundException(request.UserId);
        }

        // Check if publisher already exists for this user
        var existingPublisher = await _publisherRepository.ExistsByUserIdAsync(request.UserId, cancellationToken);
        if (existingPublisher)
        {
            throw new PublisherAlreadyExistsException(request.UserId);
        }

        // Create new publisher
        var publisher = new PublisherEntity
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Name = request.Name,
            Bio = request.Bio,
            WebsiteUrl = request.WebsiteUrl,
            LogoUrl = request.LogoUrl,
            SupportEmail = request.SupportEmail,
            IsVerified = false,
            Status = PublisherStatus.Active, // Publishers are immediately active but not verified
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _publisherRepository.CreateAsync(publisher, cancellationToken);

        _logger.LogInformation("Publisher {PublisherId} registered for user {UserId}", publisher.Id, request.UserId);

        // Publish domain event
        await _eventPublisher.PublishAsync(
            new PublisherRegisteredEvent(publisher.Id, request.UserId, publisher.Name),
            cancellationToken);

        return PublisherDto.FromEntity(publisher);
    }
}
