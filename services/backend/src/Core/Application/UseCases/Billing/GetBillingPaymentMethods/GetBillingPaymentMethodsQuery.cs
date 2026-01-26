using Application.UseCases.Billing.Common;
using MediatR;

namespace Application.UseCases.Billing.GetBillingPaymentMethods;

/// <summary>
/// Query to get saved payment methods for the authenticated user.
/// </summary>
public sealed record GetBillingPaymentMethodsQuery(Guid UserId) : IRequest<List<PaymentMethodDto>>;
