using Application.UseCases.Agent.Common;
using MediatR;

namespace Application.UseCases.Agent.CreateAgent;

public class CreateAgentCommand(
    string name,
    string? description,
    string? avatarUrl,
    string? personality,
    Guid? personalityTemplateId,
    string? language,
    Guid[]? pluginIds,
    Guid? userId = null,
    bool isPublic = false) : IRequest<AgentDto>
{
    public string Name { get; set; } = name;
    public string? Description { get; set; } = description;
    public string? AvatarUrl { get; set; } = avatarUrl;
    public string? Personality { get; set; } = personality;
    public Guid? PersonalityTemplateId { get; set; } = personalityTemplateId;
    public string? Language { get; set; } = language;
    public Guid[] PluginIds { get; set; } = pluginIds ?? [];
    public Guid? UserId { get; set; } = userId;
    public bool IsPublic { get; set; } = isPublic;
}