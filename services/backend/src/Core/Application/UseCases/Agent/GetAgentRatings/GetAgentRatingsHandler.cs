using Application.Interfaces;
using Application.UseCases.Agent.RateAgent;
using Domain.Exceptions;
using MediatR;

namespace Application.UseCases.Agent.GetAgentRatings;

public class GetAgentRatingsHandler(
    IAgentRepository agentRepository,
    IAgentRatingRepository ratingRepository) : IRequestHandler<GetAgentRatingsQuery, AgentRatingsResultDto>
{
    private readonly IAgentRepository _agentRepository = agentRepository;
    private readonly IAgentRatingRepository _ratingRepository = ratingRepository;

    public async Task<AgentRatingsResultDto> Handle(GetAgentRatingsQuery request, CancellationToken cancellationToken)
    {
        if (request.AgentId == Guid.Empty)
        {
            throw new ValidationException("Agent ID is required.", nameof(request.AgentId));
        }

        // Verify agent exists
        var agent = await _agentRepository.GetByIdAsync(request.AgentId, cancellationToken);
        if (agent is null)
        {
            throw new AgentNotFoundException(request.AgentId);
        }

        // Get all ratings for this agent
        var ratings = await _ratingRepository.ListByAgentAsync(request.AgentId, cancellationToken);

        // Get average rating
        var averageRating = await _ratingRepository.GetAverageRatingAsync(request.AgentId, cancellationToken);

        return new AgentRatingsResultDto
        {
            AgentId = request.AgentId,
            AverageRating = averageRating,
            TotalRatings = ratings.Count,
            Ratings = ratings.Select(r => new AgentRatingDto
            {
                Id = r.Id,
                AgentId = r.AgentId,
                UserId = r.UserId,
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
                NewAverageRating = null // Not applicable for listing
            }).ToList()
        };
    }
}
