namespace Application.UseCases.ChatSession.GetChatSessionById;

public sealed record GetChatSessionByIdDto(
    Guid Id,
    string SessionId,
    Guid UserId,
    Guid? AgentId,
    Guid? TeamId,
    string Title,
    string InteractionType,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    AgentDto? Agent,
    TeamDto? Team
);

public sealed record AgentDto(
    Guid Id,
    string Name,
    string? Description,
    string? Prompt,
    string Model,
    bool IsActive,
    bool IsPublic,
    Guid OwnerId,
    List<PluginDto> Plugins
);

public sealed record PluginDto(
    Guid Id,
    string Name,
    string Description,
    string Version,
    decimal Price,
    string Category,
    bool IsActive
);

public sealed record TeamDto(
    Guid Id,
    string Name,
    string? Description
);