using System.Text.Json;
using Domain.Entities;

namespace Application.UseCases.UserPlugin.Common;

public sealed record UserPluginDto
{
    public Guid PluginId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public DateTime InstalledAt { get; init; }
    public bool Enabled { get; init; }
    public Dictionary<string, object>? Settings { get; init; }
    public List<Guid> EnabledAgents { get; init; } = new();

    public static UserPluginDto FromEntity(UserPluginEntity userPlugin, IEnumerable<Guid> enabledAgentIds)
    {
        Dictionary<string, object>? settings = null;
        if (!string.IsNullOrEmpty(userPlugin.Settings))
        {
            try
            {
                settings = JsonSerializer.Deserialize<Dictionary<string, object>>(userPlugin.Settings);
            }
            catch
            {
                // If deserialization fails, leave settings as null
            }
        }

        return new UserPluginDto
        {
            PluginId = userPlugin.PluginId,
            Name = userPlugin.Plugin?.Name ?? string.Empty,
            Description = userPlugin.Plugin?.Description,
            Icon = userPlugin.Plugin?.Icon,
            InstalledAt = userPlugin.PurchasedAt,
            Enabled = userPlugin.IsActive,
            Settings = settings,
            EnabledAgents = enabledAgentIds.ToList()
        };
    }
}
