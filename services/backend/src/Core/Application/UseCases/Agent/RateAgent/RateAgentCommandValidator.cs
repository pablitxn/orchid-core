using Domain.Exceptions;

namespace Application.UseCases.Agent.RateAgent;

public static class RateAgentCommandValidator
{
    public static void Validate(RateAgentCommand command)
    {
        if (command.AgentId == Guid.Empty)
        {
            throw new ValidationException("Agent ID is required.", nameof(command.AgentId));
        }

        if (command.UserId == Guid.Empty)
        {
            throw new ValidationException("User ID is required.", nameof(command.UserId));
        }

        if (command.Rating < 1 || command.Rating > 5)
        {
            throw new ValidationException("Rating must be between 1 and 5.", nameof(command.Rating));
        }

        if (command.Comment is not null && command.Comment.Length > 1000)
        {
            throw new ValidationException("Comment must not exceed 1000 characters.", nameof(command.Comment));
        }
    }
}
