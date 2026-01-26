namespace Application.UseCases.Credits.Common;

public sealed record CreditTransactionDto
{
    public Guid Id { get; init; }
    public string Type { get; init; } = string.Empty;
    public int Amount { get; init; }
    public int BalanceAfter { get; init; }
    public string? ResourceType { get; init; }
    public string? ResourceName { get; init; }
    public DateTime CreatedAt { get; init; }
    public object? Metadata { get; init; }
}

public sealed record CreditTransactionsDto
{
    public List<CreditTransactionDto> Transactions { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}
