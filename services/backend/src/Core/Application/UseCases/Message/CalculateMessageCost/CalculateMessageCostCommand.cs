using MediatR;

namespace Application.UseCases.Message.CalculateMessageCost;

public sealed record CalculateMessageCostCommand(
    Guid UserId,
    Guid MessageId,
    string MessageContent,
    int? TokenCount = null,
    bool HasPluginUsage = false,
    bool HasWorkflowUsage = false,
    int AdditionalPluginCredits = 0,
    int AdditionalWorkflowCredits = 0
) : IRequest<CalculateMessageCostResult>;