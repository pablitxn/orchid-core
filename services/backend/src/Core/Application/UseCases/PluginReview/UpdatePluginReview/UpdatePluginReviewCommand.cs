using Application.UseCases.PluginReview.Common;
using MediatR;

namespace Application.UseCases.PluginReview.UpdatePluginReview;

public sealed record UpdatePluginReviewCommand(
    Guid ReviewId,
    Guid UserId,
    int Rating,
    string? Title,
    string? Body
) : IRequest<PluginReviewDto>;
