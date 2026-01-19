using Application.UseCases.Agent.VerifyAgentAccess;
using Application.UseCases.Subscription.ConsumeCredits;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CreditSystem.IntegrationTests.Mocks;

public class MockConsumeCreditsHandler : IRequestHandler<ConsumeCreditsCommand, SubscriptionEntity>
{
    private readonly ApplicationDbContext _context;
    
    public MockConsumeCreditsHandler(ApplicationDbContext context)
    {
        _context = context;
    }
    
    public async Task<SubscriptionEntity> Handle(ConsumeCreditsCommand request, CancellationToken cancellationToken)
    {
        var subscription = await _context.Subscriptions
            .Include(s => s.SubscriptionPlan)
            .FirstOrDefaultAsync(s => s.UserId == request.UserId, cancellationToken);
            
        if (subscription == null)
        {
            throw new SubscriptionNotFoundException(request.UserId);
        }
        
        // Check if user has unlimited plan
        var isUnlimited = subscription.SubscriptionPlan?.PlanEnum == SubscriptionPlanEnum.Unlimited;
        
        if (!isUnlimited && subscription.Credits < request.Amount)
        {
            throw new InsufficientCreditsException(subscription.Credits, request.Amount);
        }
        
        // Only deduct credits if not unlimited
        if (!isUnlimited)
        {
            subscription.Credits -= request.Amount;
            subscription.UpdatedAt = DateTime.UtcNow;
        }
        
        // Create consumption record - 0 for unlimited plans
        var consumedCredits = isUnlimited ? 0 : request.Amount;
        var consumption = new CreditConsumptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            CreditsConsumed = consumedCredits,
            ConsumptionType = request.ResourceType ?? "message",
            ResourceId = Guid.NewGuid(),
            ResourceName = "Chat message",
            BalanceAfter = subscription.Credits,
            ConsumedAt = DateTime.UtcNow
        };
        
        await _context.CreditConsumptions.AddAsync(consumption, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        
        return subscription;
    }
}

public class MockVerifyAgentAccessHandler : IRequestHandler<VerifyAgentAccessQuery, VerifyAgentAccessResult>
{
    public Task<VerifyAgentAccessResult> Handle(VerifyAgentAccessQuery request, CancellationToken cancellationToken)
    {
        // For testing, always grant access unless agentId is empty
        if (request.AgentId == Guid.Empty)
        {
            return Task.FromResult(new VerifyAgentAccessResult(false, "Invalid agent"));
        }
        
        return Task.FromResult(new VerifyAgentAccessResult(true));
    }
}