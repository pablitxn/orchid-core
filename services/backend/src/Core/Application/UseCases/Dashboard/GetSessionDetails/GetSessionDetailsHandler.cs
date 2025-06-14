using Application.Interfaces;
using Core.Application.Interfaces;
using Core.Application.UseCases.Dashboard.Common;
using Core.Application.UseCases.Dashboard.GetSessionDetails;
using MediatR;

namespace Application.UseCases.Dashboard.GetSessionDetails;

public sealed class GetSessionDetailsHandler(
    IChatSessionRepository chatSessionRepository,
    IMessageRepository messageRepository,
    ICreditConsumptionRepository creditConsumptionRepository)
    : IRequestHandler<GetSessionDetailsQuery, List<SessionDetailsDto>>
{
    public async Task<List<SessionDetailsDto>> Handle(GetSessionDetailsQuery request,
        CancellationToken cancellationToken)
    {
        var sessions = await chatSessionRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        // Apply filters
        if (request.IsArchived.HasValue)
        {
            sessions = sessions.Where(s => s.IsArchived == request.IsArchived.Value).ToList();
        }

        if (request.AgentId.HasValue)
        {
            sessions = sessions.Where(s => s.AgentId == request.AgentId.Value).ToList();
        }

        if (request.TeamId.HasValue)
        {
            sessions = sessions.Where(s => s.TeamId == request.TeamId.Value).ToList();
        }

        if (request.StartDate.HasValue)
        {
            sessions = sessions.Where(s => s.CreatedAt >= request.StartDate.Value).ToList();
        }

        if (request.EndDate.HasValue)
        {
            sessions = sessions.Where(s => s.CreatedAt <= request.EndDate.Value).ToList();
        }

        // Order by creation date descending
        sessions = sessions.OrderByDescending(s => s.CreatedAt).ToList();

        // Paginate
        sessions = sessions
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        // Build session details
        var sessionDetails = new List<SessionDetailsDto>();

        foreach (var session in sessions)
        {
            var messageCount = await messageRepository.GetCountBySessionIdAsync(session.SessionId, cancellationToken);
            var messageTimes =
                await messageRepository.GetMessageTimestampsBySessionIdAsync(session.SessionId, cancellationToken);

            var duration = messageTimes.Any()
                ? messageTimes.Max() - messageTimes.Min()
                : TimeSpan.Zero;

            var lastMessageAt = messageTimes.Any() ? messageTimes.Max() : (DateTime?)null;

            // Get credits consumed for this session
            var creditsConsumed = await creditConsumptionRepository.GetByUserIdAsync(request.UserId, cancellationToken);
            var sessionCredits = creditsConsumed
                // .Where(c => c.ResourceId == session.SessionId)
                .Sum(c => c.CreditsConsumed);

            sessionDetails.Add(new SessionDetailsDto
            {
                SessionId = session.Id,
                Title = session.Title ?? $"Session {session.CreatedAt:yyyy-MM-dd HH:mm}",
                AgentId = session.AgentId,
                AgentName = session.Agent?.Name,
                TeamId = session.TeamId,
                TeamName = session.Team?.Name,
                MessageCount = messageCount,
                CreditsConsumed = sessionCredits,
                Duration = duration,
                CreatedAt = session.CreatedAt,
                LastMessageAt = lastMessageAt,
                IsArchived = session.IsArchived
            });
        }

        return sessionDetails;
    }
}