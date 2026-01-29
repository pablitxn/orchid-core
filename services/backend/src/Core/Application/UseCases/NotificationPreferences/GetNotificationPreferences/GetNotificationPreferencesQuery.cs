using MediatR;

namespace Application.UseCases.NotificationPreferences.GetNotificationPreferences;

public record GetNotificationPreferencesQuery(Guid UserId) : IRequest<NotificationPreferencesDto>;
