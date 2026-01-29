using Domain.Entities;

namespace Application.UseCases.NotificationPreferences;

public record NotificationPreferencesDto(
    Guid Id,
    Guid UserId,
    bool NotificationsEnabled,
    TimeOnly? QuietHoursStart,
    TimeOnly? QuietHoursEnd,
    string Timezone,
    string[] EnabledTypes,
    bool MatrixNotificationsEnabled,
    bool EmailNotificationsEnabled,
    string? EmailDigestFrequency)
{
    public static NotificationPreferencesDto FromEntity(NotificationPreferencesEntity entity)
    {
        string[] enabledTypes;
        try
        {
            enabledTypes = System.Text.Json.JsonSerializer.Deserialize<string[]>(entity.EnabledTypes) ?? [];
        }
        catch
        {
            enabledTypes = ["reminders", "insights", "ingestion", "sync"];
        }

        return new NotificationPreferencesDto(
            entity.Id,
            entity.UserId,
            entity.NotificationsEnabled,
            entity.QuietHoursStart,
            entity.QuietHoursEnd,
            entity.Timezone,
            enabledTypes,
            entity.MatrixNotificationsEnabled,
            entity.EmailNotificationsEnabled,
            entity.EmailDigestFrequency);
    }
}
