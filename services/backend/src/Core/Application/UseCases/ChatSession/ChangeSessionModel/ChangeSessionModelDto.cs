namespace Application.UseCases.ChatSession.ChangeSessionModel;

/// <summary>
/// Response DTO for model change operation.
/// </summary>
public sealed record ChangeSessionModelDto
{
    public required string SessionId { get; init; }
    public string? PreviousModel { get; init; }
    public required string CurrentModel { get; init; }
    public required DateTime ChangedAt { get; init; }
}
