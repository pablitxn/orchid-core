using MediatR;

namespace Application.UseCases.PluginReview.VoteHelpful;

public sealed record VoteHelpfulCommand(
    Guid ReviewId,
    Guid UserId
) : IRequest<VoteHelpfulDto>;

public sealed record VoteHelpfulDto(bool IsHelpful, int HelpfulCount);
