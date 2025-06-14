using MediatR;
using Core.Application.UseCases.Dashboard.Common;

namespace Core.Application.UseCases.Dashboard.GetSessionDetails;

public sealed record GetSessionDetailsQuery(
    Guid UserId,
    int PageNumber = 1,
    int PageSize = 20,
    bool? IsArchived = null,
    Guid? AgentId = null,
    Guid? TeamId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null
) : IRequest<List<SessionDetailsDto>>;