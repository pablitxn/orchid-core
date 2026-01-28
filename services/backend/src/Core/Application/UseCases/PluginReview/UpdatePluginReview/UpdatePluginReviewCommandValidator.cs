using Domain.Exceptions;

namespace Application.UseCases.PluginReview.UpdatePluginReview;

public static class UpdatePluginReviewCommandValidator
{
    private const int MinRating = 1;
    private const int MaxRating = 5;
    private const int MaxTitleLength = 100;
    private const int MaxBodyLength = 2000;

    public static void Validate(UpdatePluginReviewCommand command)
    {
        if (command == null)
            throw new ValidationException("Command cannot be null.", nameof(command));

        if (command.ReviewId == Guid.Empty)
            throw new ValidationException("ReviewId is required.", nameof(command.ReviewId));

        if (command.UserId == Guid.Empty)
            throw new ValidationException("UserId is required.", nameof(command.UserId));

        if (command.Rating < MinRating || command.Rating > MaxRating)
            throw new ValidationException($"Rating must be between {MinRating} and {MaxRating}.", nameof(command.Rating));

        if (command.Title?.Length > MaxTitleLength)
            throw new ValidationException($"Title cannot exceed {MaxTitleLength} characters.", nameof(command.Title));

        if (command.Body?.Length > MaxBodyLength)
            throw new ValidationException($"Body cannot exceed {MaxBodyLength} characters.", nameof(command.Body));
    }
}
