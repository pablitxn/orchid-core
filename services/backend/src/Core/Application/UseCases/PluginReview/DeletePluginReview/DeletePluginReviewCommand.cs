using MediatR;

namespace Application.UseCases.PluginReview.DeletePluginReview;

public sealed record DeletePluginReviewCommand(
    Guid ReviewId,
    Guid UserId
) : IRequest<Unit>;
