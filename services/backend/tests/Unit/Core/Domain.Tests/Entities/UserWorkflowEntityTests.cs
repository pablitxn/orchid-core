using Domain.Entities;

namespace Domain.Tests.Entities;

public class UserWorkflowEntityTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var purchasedAt = DateTime.UtcNow;

        var userWorkflow = new UserWorkflowEntity
        {
            UserId = userId,
            WorkflowId = workflowId,
            PurchasedAt = purchasedAt
        };

        // Assert
        Assert.Equal(userId, userWorkflow.UserId);
        Assert.Equal(workflowId, userWorkflow.WorkflowId);
        Assert.Equal(purchasedAt, userWorkflow.PurchasedAt);
    }
}
