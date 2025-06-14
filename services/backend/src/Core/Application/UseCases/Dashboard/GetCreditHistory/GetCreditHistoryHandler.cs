using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using MediatR;
using Core.Application.UseCases.Dashboard.Common;
using Core.Application.Interfaces;

namespace Core.Application.UseCases.Dashboard.GetCreditHistory;

public sealed class GetCreditHistoryHandler : IRequestHandler<GetCreditHistoryQuery, CreditHistoryDto>
{
    private readonly ICreditConsumptionRepository _creditConsumptionRepository;

    public GetCreditHistoryHandler(ICreditConsumptionRepository creditConsumptionRepository)
    {
        _creditConsumptionRepository = creditConsumptionRepository;
    }

    public async Task<CreditHistoryDto> Handle(GetCreditHistoryQuery request, CancellationToken cancellationToken)
    {
        var startDate = request.StartDate ?? DateTime.UtcNow.AddMonths(-1);
        var endDate = request.EndDate ?? DateTime.UtcNow;
        
        var creditHistory = await _creditConsumptionRepository.GetByDateRangeAsync(
            request.UserId,
            startDate,
            endDate,
            cancellationToken
        );

        // Filter by consumption type if specified
        if (!string.IsNullOrWhiteSpace(request.ConsumptionType))
        {
            creditHistory = creditHistory
                .Where(c => c.ConsumptionType.Equals(request.ConsumptionType, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Order by date descending
        creditHistory = creditHistory.OrderByDescending(c => c.ConsumedAt).ToList();

        // Paginate
        var totalItems = creditHistory.Count;
        var totalPages = (int)Math.Ceiling(totalItems / (double)request.PageSize);
        
        var pagedItems = creditHistory
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new CreditHistoryItemDto
            {
                Id = c.Id,
                ConsumptionType = c.ConsumptionType,
                ResourceName = c.ResourceName,
                CreditsConsumed = c.CreditsConsumed,
                BalanceAfter = c.BalanceAfter,
                ConsumedAt = c.ConsumedAt,
                // Metadata = c.Metadata
            })
            .ToList();

        return new CreditHistoryDto
        {
            Items = pagedItems,
            TotalPages = totalPages,
            CurrentPage = request.PageNumber
        };
    }
}