namespace Application.UseCases.Message.CalculateMessageCost;

public sealed record CalculateMessageCostResult(
    bool Success,
    int TotalCredits = 0,
    int MessageCredits = 0,
    int AdditionalCredits = 0,
    string? BillingMethod = null,
    string? Error = null
);