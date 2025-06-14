using Application.Interfaces;
using Application.UseCases.SubscriptionPlan.GetPlan;
using Domain.Entities;
using Moq;

namespace Application.Tests.UseCases.SubscriptionPlan.GetPlan;

public class GetSubscriptionPlanHandlerTests
{
    private readonly GetSubscriptionPlanHandler _handler;
    private readonly Mock<ISubscriptionPlanRepository> _repo = new();

    public GetSubscriptionPlanHandlerTests()
    {
        _handler = new GetSubscriptionPlanHandler(_repo.Object);
    }

    [Fact]
    public async Task Handle_ReturnsPlan()
    {
        var plan = new SubscriptionPlanEntity
        {
            Id = Guid.NewGuid(), PlanEnum = Domain.Enums.SubscriptionPlanEnum.Monthly100, Price = 9.99m, Credits = 100
        };
        _repo.Setup(r => r.GetByPlanAsync(plan.PlanEnum, It.IsAny<CancellationToken>())).ReturnsAsync(plan);

        var result = await _handler.Handle(new GetSubscriptionPlanQuery(plan.PlanEnum), CancellationToken.None);

        _repo.Verify(r => r.GetByPlanAsync(plan.PlanEnum, It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(result);
        Assert.Equal(plan.Price, result!.Price);
    }
}