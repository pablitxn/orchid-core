namespace Application.Interfaces;

public interface IUnitOfWork : IDisposable
{
    ISubscriptionRepository Subscriptions { get; }
    ICreditConsumptionRepository CreditConsumptions { get; }
    IMessageCostRepository MessageCosts { get; }
    IUserBillingPreferenceRepository UserBillingPreferences { get; }
    IWorkflowRepository Workflows { get; }
    IPluginRepository Plugins { get; }
    IUserPluginRepository UserPlugins { get; }
    IUserCreditLimitRepository UserCreditLimits { get; }
    ICostConfigurationRepository CostConfigurations { get; }
    IAuditLogRepository AuditLogs { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}