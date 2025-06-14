namespace Domain.Entities;

/// <summary>
/// Entity for tracking all sensitive operations in the system
/// </summary>
public class AuditLogEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// User who performed the action
    /// </summary>
    public Guid? UserId { get; set; }
    
    /// <summary>
    /// Type of action performed
    /// </summary>
    public string ActionType { get; set; } = string.Empty;
    
    /// <summary>
    /// Entity type that was affected
    /// </summary>
    public string EntityType { get; set; } = string.Empty;
    
    /// <summary>
    /// ID of the affected entity
    /// </summary>
    public string? EntityId { get; set; }
    
    /// <summary>
    /// Old values before the change (JSON)
    /// </summary>
    public string? OldValues { get; set; }
    
    /// <summary>
    /// New values after the change (JSON)
    /// </summary>
    public string? NewValues { get; set; }
    
    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Error message if operation failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// IP address of the request
    /// </summary>
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// User agent of the request
    /// </summary>
    public string? UserAgent { get; set; }
    
    /// <summary>
    /// Additional metadata (JSON)
    /// </summary>
    public string? Metadata { get; set; }
    
    /// <summary>
    /// When the action occurred
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Severity level of the action
    /// </summary>
    public AuditSeverity Severity { get; set; } = AuditSeverity.Info;
}

public enum AuditSeverity
{
    Info,
    Warning,
    Critical
}

public static class AuditActionTypes
{
    // Authentication
    public const string UserLogin = "USER_LOGIN";
    public const string UserLogout = "USER_LOGOUT";
    public const string UserLoginFailed = "USER_LOGIN_FAILED";
    public const string PasswordReset = "PASSWORD_RESET";
    
    // Credit Operations
    public const string CreditConsume = "CREDIT_CONSUME";
    public const string CreditAdd = "CREDIT_ADD";
    public const string CreditLimitExceeded = "CREDIT_LIMIT_EXCEEDED";
    public const string CreditPurchase = "CREDIT_PURCHASE";
    
    // Marketplace
    public const string PluginPurchase = "PLUGIN_PURCHASE";
    public const string WorkflowPurchase = "WORKFLOW_PURCHASE";
    public const string PurchaseFailed = "PURCHASE_FAILED";
    
    // Plugin Operations
    public const string PluginExecute = "PLUGIN_EXECUTE";
    public const string PluginExecuteFailed = "PLUGIN_EXECUTE_FAILED";
    public const string PluginLoad = "PLUGIN_LOAD";
    public const string PluginUnload = "PLUGIN_UNLOAD";
    
    // Chat Operations
    public const string ChatMessage = "CHAT_MESSAGE";
    public const string ChatRateLimitExceeded = "CHAT_RATE_LIMIT_EXCEEDED";
    public const string ChatAccessDenied = "CHAT_ACCESS_DENIED";
    
    // Administrative
    public const string UserCreate = "USER_CREATE";
    public const string UserUpdate = "USER_UPDATE";
    public const string UserDelete = "USER_DELETE";
    public const string SubscriptionCreate = "SUBSCRIPTION_CREATE";
    public const string SubscriptionUpdate = "SUBSCRIPTION_UPDATE";
    public const string SubscriptionCancel = "SUBSCRIPTION_CANCEL";
}