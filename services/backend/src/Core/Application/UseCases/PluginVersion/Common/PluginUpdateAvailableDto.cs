namespace Application.UseCases.PluginVersion.Common;

public sealed record PluginUpdateAvailableDto
{
    public Guid PluginId { get; init; }
    public string PluginName { get; init; } = string.Empty;
    public string? PluginIcon { get; init; }
    public string InstalledVersion { get; init; } = string.Empty;
    public string LatestVersion { get; init; } = string.Empty;
    public string? Changelog { get; init; }
    public bool IsBreakingChange { get; init; }
    public bool IsDeprecated { get; init; }
    public DateTime? DeprecationEndsAt { get; init; }
    public string? DeprecationMessage { get; init; }
}
