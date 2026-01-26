using Application.UseCases.Subscription.Common;

namespace Application.UseCases.Billing.Common;

/// <summary>
/// Comprehensive billing summary for a user.
/// </summary>
public sealed record BillingSummaryDto
{
    /// <summary>
    /// Current subscription details.
    /// </summary>
    public SubscriptionDto? Subscription { get; init; }

    /// <summary>
    /// Current credit balance.
    /// </summary>
    public int CreditBalance { get; init; }

    /// <summary>
    /// Whether the user has an unlimited credits plan.
    /// </summary>
    public bool IsUnlimited { get; init; }

    /// <summary>
    /// Number of payment methods on file.
    /// </summary>
    public int PaymentMethodCount { get; init; }

    /// <summary>
    /// Most recent invoice.
    /// </summary>
    public InvoiceDto? LatestInvoice { get; init; }

    /// <summary>
    /// Total amount spent in the current billing period.
    /// </summary>
    public long CurrentPeriodSpend { get; init; }

    /// <summary>
    /// Total credits consumed this month.
    /// </summary>
    public int CreditsConsumedThisMonth { get; init; }

    /// <summary>
    /// URL to access Stripe billing portal.
    /// </summary>
    public string? BillingPortalUrl { get; init; }
}
