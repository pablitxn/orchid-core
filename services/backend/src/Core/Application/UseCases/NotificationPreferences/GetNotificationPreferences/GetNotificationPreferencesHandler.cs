using Application.Interfaces;
using MediatR;

namespace Application.UseCases.NotificationPreferences.GetNotificationPreferences;

public class GetNotificationPreferencesHandler(INotificationPreferencesRepository repository)
    : IRequestHandler<GetNotificationPreferencesQuery, NotificationPreferencesDto>
{
    private readonly INotificationPreferencesRepository _repository = repository;

    public async Task<NotificationPreferencesDto> Handle(
        GetNotificationPreferencesQuery request,
        CancellationToken cancellationToken)
    {
        var preferences = await _repository.GetOrCreateDefaultAsync(request.UserId, cancellationToken);
        return NotificationPreferencesDto.FromEntity(preferences);
    }
}
