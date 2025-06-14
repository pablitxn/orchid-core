using Domain.Entities;
using Domain.Enums;

namespace Domain.Tests.Entities;

public class ActionCostEntityTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        var cost = new ActionCostEntity
        {
            Id = Guid.NewGuid(),
            ActionName = "chat_message",
            Credits = 2,
            PaymentUnit = PaymentUnit.PerMessage
        };

        Assert.Equal("chat_message", cost.ActionName);
        Assert.Equal(2, cost.Credits);
        Assert.Equal(PaymentUnit.PerMessage, cost.PaymentUnit);
    }
}