using MediatR;

namespace Application.UseCases.Subscription.GetPortalSession;

/// <summary>
/// Query to get a Stripe billing portal session URL.
/// </summary>
public record GetPortalSessionQuery(
    Guid UserId,
    string ReturnUrl) : IRequest<PortalSessionDto>;

/// <summary>
/// Result containing the billing portal URL.
/// </summary>
public sealed record PortalSessionDto(string Url);
