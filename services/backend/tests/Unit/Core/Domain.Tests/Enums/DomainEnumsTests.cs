using Domain.Enums;

namespace Domain.Tests.Enums;

public class DocumentEnumTests
{
    [Fact]
    public void HasExpectedValues()
    {
        // Assert
        Assert.Equal(0, (int)DocumentEnum.Attachment);
        Assert.Equal(1, (int)DocumentEnum.KnowledgeBase);
    }

    [Fact]
    public void HasExactlyTwoValues()
    {
        // Arrange & Act
        var values = Enum.GetValues<DocumentEnum>();

        // Assert
        Assert.Equal(2, values.Length);
    }

    [Theory]
    [InlineData(DocumentEnum.Attachment)]
    [InlineData(DocumentEnum.KnowledgeBase)]
    public void AllValues_AreDefined(DocumentEnum value)
    {
        // Assert
        Assert.True(Enum.IsDefined(value));
    }
}

public class InteractionTypeTests
{
    [Fact]
    public void HasExpectedValues()
    {
        // Assert
        Assert.Equal(0, (int)InteractionType.Text);
        Assert.Equal(1, (int)InteractionType.Voice);
        Assert.Equal(2, (int)InteractionType.Video);
    }

    [Fact]
    public void HasExactlyThreeValues()
    {
        // Arrange & Act
        var values = Enum.GetValues<InteractionType>();

        // Assert
        Assert.Equal(3, values.Length);
    }

    [Theory]
    [InlineData(InteractionType.Text)]
    [InlineData(InteractionType.Voice)]
    [InlineData(InteractionType.Video)]
    public void AllValues_AreDefined(InteractionType value)
    {
        // Assert
        Assert.True(Enum.IsDefined(value));
    }
}

public class PaymentUnitTests
{
    [Fact]
    public void HasExpectedValues()
    {
        // Assert
        Assert.Equal(0, (int)PaymentUnit.PerMessage);
        Assert.Equal(1, (int)PaymentUnit.PerToken);
    }

    [Fact]
    public void HasExactlyTwoValues()
    {
        // Arrange & Act
        var values = Enum.GetValues<PaymentUnit>();

        // Assert
        Assert.Equal(2, values.Length);
    }

    [Theory]
    [InlineData(PaymentUnit.PerMessage)]
    [InlineData(PaymentUnit.PerToken)]
    public void AllValues_AreDefined(PaymentUnit value)
    {
        // Assert
        Assert.True(Enum.IsDefined(value));
    }
}

public class SubscriptionPlanEnumTests
{
    [Fact]
    public void HasExpectedValues()
    {
        // Assert
        Assert.Equal(0, (int)SubscriptionPlanEnum.Monthly100);
        Assert.Equal(1, (int)SubscriptionPlanEnum.Package5);
        Assert.Equal(2, (int)SubscriptionPlanEnum.Package10);
        Assert.Equal(3, (int)SubscriptionPlanEnum.Package25);
        Assert.Equal(4, (int)SubscriptionPlanEnum.Unlimited);
    }

    [Fact]
    public void HasExactlyFiveValues()
    {
        // Arrange & Act
        var values = Enum.GetValues<SubscriptionPlanEnum>();

        // Assert
        Assert.Equal(5, values.Length);
    }

    [Theory]
    [InlineData(SubscriptionPlanEnum.Monthly100)]
    [InlineData(SubscriptionPlanEnum.Package5)]
    [InlineData(SubscriptionPlanEnum.Package10)]
    [InlineData(SubscriptionPlanEnum.Package25)]
    [InlineData(SubscriptionPlanEnum.Unlimited)]
    public void AllValues_AreDefined(SubscriptionPlanEnum value)
    {
        // Assert
        Assert.True(Enum.IsDefined(value));
    }
}

public class TeamInteractionPolicyTests
{
    [Fact]
    public void HasExpectedValues()
    {
        // Assert
        Assert.Equal(0, (int)TeamInteractionPolicy.Open);
        Assert.Equal(1, (int)TeamInteractionPolicy.LeaderDriven);
    }

    [Fact]
    public void HasExactlyTwoValues()
    {
        // Arrange & Act
        var values = Enum.GetValues<TeamInteractionPolicy>();

        // Assert
        Assert.Equal(2, values.Length);
    }

    [Theory]
    [InlineData(TeamInteractionPolicy.Open)]
    [InlineData(TeamInteractionPolicy.LeaderDriven)]
    public void AllValues_AreDefined(TeamInteractionPolicy value)
    {
        // Assert
        Assert.True(Enum.IsDefined(value));
    }
}
