namespace Application.UseCases.PluginReview.Common;

public sealed record PluginReviewDto
{
    public Guid Id { get; init; }
    public Guid PluginId { get; init; }
    public Guid UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public int Rating { get; init; }
    public string? Title { get; init; }
    public string? Body { get; init; }
    public int HelpfulCount { get; init; }
    public bool IsVerified { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
