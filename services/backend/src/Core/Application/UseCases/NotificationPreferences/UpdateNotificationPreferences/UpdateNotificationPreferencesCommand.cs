using MediatR;

namespace Application.UseCases.NotificationPreferences.UpdateNotificationPreferences;

public record UpdateNotificationPreferencesCommand(
    Guid UserId,
    bool? NotificationsEnabled,
    TimeOnly? QuietHoursStart,
    TimeOnly? QuietHoursEnd,
    string? Timezone,
    string[]? EnabledTypes,
    bool? MatrixNotificationsEnabled,
    bool? EmailNotificationsEnabled,
    string? EmailDigestFrequency
) : IRequest<NotificationPreferencesDto>;
