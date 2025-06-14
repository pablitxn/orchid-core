namespace Application.UseCases.Agent.Common;

public record AgentDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? AvatarUrl { get; init; }
    public List<PluginDto> Plugins { get; init; } = new();
    public string? Personality { get; init; }
    public Guid? PersonalityTemplateId { get; init; }
    public string? Language { get; init; }
    public Guid? UserId { get; init; }
    public bool IsPublic { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public AgentStatsDto? Stats { get; init; }
}

public record PluginDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Icon { get; init; }
}

public record AgentStatsDto
{
    public int TotalConversations { get; init; }
    public double? AvgRating { get; init; }
    public DateTime? LastUsed { get; init; }
}