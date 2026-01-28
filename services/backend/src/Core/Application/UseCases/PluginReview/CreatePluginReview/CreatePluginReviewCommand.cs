using Application.UseCases.PluginReview.Common;
using MediatR;

namespace Application.UseCases.PluginReview.CreatePluginReview;

public sealed record CreatePluginReviewCommand(
    Guid PluginId,
    Guid UserId,
    int Rating,
    string? Title,
    string? Body
) : IRequest<PluginReviewDto>;
