using MediatR;

namespace Application.UseCases.PluginReview.FlagPluginReview;

public sealed record FlagPluginReviewCommand(
    Guid ReviewId,
    Guid UserId,
    string Reason,
    string? Details
) : IRequest<Unit>;
