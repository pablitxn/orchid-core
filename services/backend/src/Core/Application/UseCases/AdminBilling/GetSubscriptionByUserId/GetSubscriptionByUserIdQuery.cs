using Application.UseCases.AdminBilling.Common;
using MediatR;

namespace Application.UseCases.AdminBilling.GetSubscriptionByUserId;

/// <summary>
/// Query to get a user's subscription details for admin purposes.
/// </summary>
public record GetSubscriptionByUserIdQuery(Guid UserId) : IRequest<AdminSubscriptionDto>;
