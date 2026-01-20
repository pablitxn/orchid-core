using Application.UseCases.Agent.Common;
using MediatR;

namespace Application.UseCases.Agent.UpdateAgent;

public sealed record UpdateAgentCommand(
    Guid AgentId,
    Guid UserId,
    string? Name,
    string? Description,
    string? AvatarUrl,
    string? Personality,
    Guid? PersonalityTemplateId,
    string? Language,
    string? Model,
    Guid[]? PluginIds,
    bool? IsPublic
) : IRequest<AgentDto>;
