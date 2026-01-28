namespace Domain.Exceptions;

public class DuplicateReviewFlagException : Exception
{
    public DuplicateReviewFlagException(Guid userId, Guid reviewId)
        : base($"User '{userId}' has already flagged review '{reviewId}'.")
    {
        UserId = userId;
        ReviewId = reviewId;
    }

    public Guid UserId { get; }
    public Guid ReviewId { get; }
}
