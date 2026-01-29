using System.Text.Json;
using Application.Interfaces;
using MediatR;

namespace Application.UseCases.NotificationPreferences.UpdateNotificationPreferences;

public class UpdateNotificationPreferencesHandler(INotificationPreferencesRepository repository)
    : IRequestHandler<UpdateNotificationPreferencesCommand, NotificationPreferencesDto>
{
    private readonly INotificationPreferencesRepository _repository = repository;

    public async Task<NotificationPreferencesDto> Handle(
        UpdateNotificationPreferencesCommand request,
        CancellationToken cancellationToken)
    {
        var preferences = await _repository.GetOrCreateDefaultAsync(request.UserId, cancellationToken);

        if (request.NotificationsEnabled.HasValue)
            preferences.NotificationsEnabled = request.NotificationsEnabled.Value;

        if (request.QuietHoursStart.HasValue)
            preferences.QuietHoursStart = request.QuietHoursStart.Value;

        if (request.QuietHoursEnd.HasValue)
            preferences.QuietHoursEnd = request.QuietHoursEnd.Value;

        if (!string.IsNullOrEmpty(request.Timezone))
            preferences.Timezone = request.Timezone;

        if (request.EnabledTypes != null)
            preferences.EnabledTypes = JsonSerializer.Serialize(request.EnabledTypes);

        if (request.MatrixNotificationsEnabled.HasValue)
            preferences.MatrixNotificationsEnabled = request.MatrixNotificationsEnabled.Value;

        if (request.EmailNotificationsEnabled.HasValue)
            preferences.EmailNotificationsEnabled = request.EmailNotificationsEnabled.Value;

        if (request.EmailDigestFrequency != null)
            preferences.EmailDigestFrequency = request.EmailDigestFrequency;

        preferences.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(preferences, cancellationToken);
        return NotificationPreferencesDto.FromEntity(preferences);
    }
}
