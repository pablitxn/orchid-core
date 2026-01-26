using Application.Interfaces;
using Application.UseCases.Credits.Common;
using MediatR;

namespace Application.UseCases.Credits.GetCreditTransactions;

public sealed class GetCreditTransactionsHandler(
    ICreditConsumptionRepository creditConsumptionRepository)
    : IRequestHandler<GetCreditTransactionsQuery, CreditTransactionsDto>
{
    private const int MaxPageSize = 100;

    public async Task<CreditTransactionsDto> Handle(
        GetCreditTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        var pageSize = Math.Min(Math.Max(1, request.PageSize), MaxPageSize);
        var page = Math.Max(1, request.Page);

        var startDate = request.StartDate ?? DateTime.UtcNow.AddMonths(-1);
        var endDate = request.EndDate ?? DateTime.UtcNow;

        var consumptions = await creditConsumptionRepository.GetByDateRangeAsync(
            request.UserId,
            startDate,
            endDate,
            cancellationToken);

        var ordered = consumptions
            .OrderByDescending(c => c.ConsumedAt)
            .ToList();

        var totalCount = ordered.Count;
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var transactions = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CreditTransactionDto
            {
                Id = c.Id,
                Type = c.ConsumptionType,
                Amount = c.CreditsConsumed,
                BalanceAfter = c.BalanceAfter,
                ResourceType = c.ConsumptionType,
                ResourceName = c.ResourceName,
                CreatedAt = c.ConsumedAt,
                Metadata = c.Metadata
            })
            .ToList();

        return new CreditTransactionsDto
        {
            Transactions = transactions,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }
}
