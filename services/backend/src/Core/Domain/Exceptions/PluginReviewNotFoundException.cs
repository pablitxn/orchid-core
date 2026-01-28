namespace Domain.Exceptions;

public class PluginReviewNotFoundException : EntityNotFoundException
{
    public PluginReviewNotFoundException(Guid reviewId)
        : base("PluginReview", reviewId)
    {
        ReviewId = reviewId;
    }

    public Guid ReviewId { get; }
}
