namespace Application.UseCases.Billing.Common;

/// <summary>
/// Represents a Stripe invoice.
/// </summary>
public sealed record InvoiceDto
{
    public string Id { get; init; } = string.Empty;
    public string Number { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public long AmountDue { get; init; }
    public long AmountPaid { get; init; }
    public string Currency { get; init; } = "usd";
    public DateTime? CreatedAt { get; init; }
    public DateTime? PaidAt { get; init; }
    public DateTime? DueDate { get; init; }
    public string? PdfUrl { get; init; }
    public string? HostedInvoiceUrl { get; init; }
    public string? Description { get; init; }
    public List<InvoiceLineItemDto> LineItems { get; init; } = new();
}

/// <summary>
/// Represents an invoice line item.
/// </summary>
public sealed record InvoiceLineItemDto
{
    public string Id { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public long Amount { get; init; }
    public int Quantity { get; init; }
    public string? PriceId { get; init; }
}
