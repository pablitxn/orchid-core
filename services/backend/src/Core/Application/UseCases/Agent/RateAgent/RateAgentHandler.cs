using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.UseCases.Agent.RateAgent;

public class RateAgentHandler(
    IAgentRepository agentRepository,
    IAgentRatingRepository ratingRepository) : IRequestHandler<RateAgentCommand, AgentRatingDto>
{
    private readonly IAgentRepository _agentRepository = agentRepository;
    private readonly IAgentRatingRepository _ratingRepository = ratingRepository;

    public async Task<AgentRatingDto> Handle(RateAgentCommand request, CancellationToken cancellationToken)
    {
        RateAgentCommandValidator.Validate(request);

        // Verify agent exists
        var agent = await _agentRepository.GetByIdAsync(request.AgentId, cancellationToken);
        if (agent is null)
        {
            throw new InvalidOperationException($"Agent with ID {request.AgentId} not found.");
        }

        // Check if user already rated this agent
        var existingRating = await _ratingRepository.GetByAgentAndUserAsync(
            request.AgentId,
            request.UserId,
            cancellationToken);

        AgentRatingEntity rating;

        if (existingRating is not null)
        {
            // Update existing rating
            existingRating.Rating = request.Rating;
            existingRating.Comment = request.Comment;
            existingRating.UpdatedAt = DateTime.UtcNow;

            await _ratingRepository.UpdateAsync(existingRating, cancellationToken);
            rating = existingRating;
        }
        else
        {
            // Create new rating
            rating = new AgentRatingEntity
            {
                Id = Guid.NewGuid(),
                AgentId = request.AgentId,
                UserId = request.UserId,
                Rating = request.Rating,
                Comment = request.Comment,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _ratingRepository.CreateAsync(rating, cancellationToken);
        }

        // Get new average rating
        var newAverage = await _ratingRepository.GetAverageRatingAsync(request.AgentId, cancellationToken);

        return new AgentRatingDto
        {
            Id = rating.Id,
            AgentId = rating.AgentId,
            UserId = rating.UserId,
            Rating = rating.Rating,
            Comment = rating.Comment,
            CreatedAt = rating.CreatedAt,
            UpdatedAt = rating.UpdatedAt,
            NewAverageRating = newAverage
        };
    }
}
