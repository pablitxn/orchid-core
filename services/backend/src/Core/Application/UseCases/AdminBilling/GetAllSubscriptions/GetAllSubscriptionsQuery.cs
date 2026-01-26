using Application.UseCases.AdminBilling.Common;
using MediatR;

namespace Application.UseCases.AdminBilling.GetAllSubscriptions;

/// <summary>
/// Query to get all subscriptions with pagination for admin purposes.
/// </summary>
public record GetAllSubscriptionsQuery(
    int Page = 1,
    int PageSize = 50
) : IRequest<GetAllSubscriptionsDto>;

/// <summary>
/// Response DTO with paginated subscription list.
/// </summary>
public sealed record GetAllSubscriptionsDto
{
    public IReadOnlyList<AdminSubscriptionDto> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
