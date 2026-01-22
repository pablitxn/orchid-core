using Domain.Entities;

namespace Domain.Tests.Entities;

public class AuditLogEntityTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;

        var auditLog = new AuditLogEntity
        {
            Id = id,
            UserId = userId,
            ActionType = AuditActionTypes.UserLogin,
            EntityType = "User",
            EntityId = userId.ToString(),
            OldValues = "{\"status\": \"inactive\"}",
            NewValues = "{\"status\": \"active\"}",
            Success = true,
            ErrorMessage = null,
            IpAddress = "192.168.1.1",
            UserAgent = "Mozilla/5.0",
            Metadata = "{\"browser\": \"Chrome\"}",
            Timestamp = timestamp,
            Severity = AuditSeverity.Info
        };

        // Assert
        Assert.Equal(id, auditLog.Id);
        Assert.Equal(userId, auditLog.UserId);
        Assert.Equal(AuditActionTypes.UserLogin, auditLog.ActionType);
        Assert.Equal("User", auditLog.EntityType);
        Assert.Equal(userId.ToString(), auditLog.EntityId);
        Assert.Equal("{\"status\": \"inactive\"}", auditLog.OldValues);
        Assert.Equal("{\"status\": \"active\"}", auditLog.NewValues);
        Assert.True(auditLog.Success);
        Assert.Null(auditLog.ErrorMessage);
        Assert.Equal("192.168.1.1", auditLog.IpAddress);
        Assert.Equal("Mozilla/5.0", auditLog.UserAgent);
        Assert.Equal("{\"browser\": \"Chrome\"}", auditLog.Metadata);
        Assert.Equal(timestamp, auditLog.Timestamp);
        Assert.Equal(AuditSeverity.Info, auditLog.Severity);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange
        var auditLog = new AuditLogEntity();

        // Assert
        Assert.NotEqual(Guid.Empty, auditLog.Id);
        Assert.Equal(string.Empty, auditLog.ActionType);
        Assert.Equal(string.Empty, auditLog.EntityType);
        Assert.Equal(AuditSeverity.Info, auditLog.Severity);
    }

    [Fact]
    public void FailedOperation_CanBeRecorded()
    {
        // Arrange
        var auditLog = new AuditLogEntity
        {
            ActionType = AuditActionTypes.CreditConsume,
            EntityType = "Subscription",
            Success = false,
            ErrorMessage = "Insufficient credits",
            Severity = AuditSeverity.Warning
        };

        // Assert
        Assert.False(auditLog.Success);
        Assert.Equal("Insufficient credits", auditLog.ErrorMessage);
        Assert.Equal(AuditSeverity.Warning, auditLog.Severity);
    }

    [Theory]
    [InlineData(AuditSeverity.Info)]
    [InlineData(AuditSeverity.Warning)]
    [InlineData(AuditSeverity.Critical)]
    public void AllSeverityLevels_CanBeSet(AuditSeverity severity)
    {
        // Arrange
        var auditLog = new AuditLogEntity
        {
            ActionType = "TEST",
            EntityType = "Test",
            Severity = severity
        };

        // Assert
        Assert.Equal(severity, auditLog.Severity);
    }
}

public class AuditSeverityEnumTests
{
    [Fact]
    public void HasExpectedValues()
    {
        // Assert
        Assert.Equal(0, (int)AuditSeverity.Info);
        Assert.Equal(1, (int)AuditSeverity.Warning);
        Assert.Equal(2, (int)AuditSeverity.Critical);
    }
}

public class AuditActionTypesTests
{
    [Fact]
    public void AuthenticationActions_HaveCorrectValues()
    {
        Assert.Equal("USER_LOGIN", AuditActionTypes.UserLogin);
        Assert.Equal("USER_LOGOUT", AuditActionTypes.UserLogout);
        Assert.Equal("USER_LOGIN_FAILED", AuditActionTypes.UserLoginFailed);
        Assert.Equal("PASSWORD_RESET", AuditActionTypes.PasswordReset);
    }

    [Fact]
    public void CreditOperationActions_HaveCorrectValues()
    {
        Assert.Equal("CREDIT_CONSUME", AuditActionTypes.CreditConsume);
        Assert.Equal("CREDIT_ADD", AuditActionTypes.CreditAdd);
        Assert.Equal("CREDIT_LIMIT_EXCEEDED", AuditActionTypes.CreditLimitExceeded);
        Assert.Equal("CREDIT_PURCHASE", AuditActionTypes.CreditPurchase);
    }

    [Fact]
    public void MarketplaceActions_HaveCorrectValues()
    {
        Assert.Equal("PLUGIN_PURCHASE", AuditActionTypes.PluginPurchase);
        Assert.Equal("WORKFLOW_PURCHASE", AuditActionTypes.WorkflowPurchase);
        Assert.Equal("PURCHASE_FAILED", AuditActionTypes.PurchaseFailed);
    }

    [Fact]
    public void PluginOperationActions_HaveCorrectValues()
    {
        Assert.Equal("PLUGIN_EXECUTE", AuditActionTypes.PluginExecute);
        Assert.Equal("PLUGIN_EXECUTE_FAILED", AuditActionTypes.PluginExecuteFailed);
        Assert.Equal("PLUGIN_LOAD", AuditActionTypes.PluginLoad);
        Assert.Equal("PLUGIN_UNLOAD", AuditActionTypes.PluginUnload);
    }

    [Fact]
    public void ChatOperationActions_HaveCorrectValues()
    {
        Assert.Equal("CHAT_MESSAGE", AuditActionTypes.ChatMessage);
        Assert.Equal("CHAT_RATE_LIMIT_EXCEEDED", AuditActionTypes.ChatRateLimitExceeded);
        Assert.Equal("CHAT_ACCESS_DENIED", AuditActionTypes.ChatAccessDenied);
    }

    [Fact]
    public void AdministrativeActions_HaveCorrectValues()
    {
        Assert.Equal("USER_CREATE", AuditActionTypes.UserCreate);
        Assert.Equal("USER_UPDATE", AuditActionTypes.UserUpdate);
        Assert.Equal("USER_DELETE", AuditActionTypes.UserDelete);
        Assert.Equal("SUBSCRIPTION_CREATE", AuditActionTypes.SubscriptionCreate);
        Assert.Equal("SUBSCRIPTION_UPDATE", AuditActionTypes.SubscriptionUpdate);
        Assert.Equal("SUBSCRIPTION_CANCEL", AuditActionTypes.SubscriptionCancel);
    }
}
