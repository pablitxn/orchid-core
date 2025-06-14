using Application.Interfaces;
using Infrastructure.Persistence;

namespace CreditSystem.IntegrationTests;

/// <summary>
/// Test implementation of IUnitOfWork that doesn't use transactions
/// since InMemory database provider doesn't support them.
/// </summary>
public class TestUnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    
    public TestUnitOfWork(ApplicationDbContext context,
        ISubscriptionRepository subscriptions,
        ICreditConsumptionRepository creditConsumptions,
        IMessageCostRepository messageCosts,
        IUserBillingPreferenceRepository userBillingPreferences,
        IWorkflowRepository workflows,
        IPluginRepository plugins,
        IUserPluginRepository userPlugins,
        IUserCreditLimitRepository userCreditLimits,
        ICostConfigurationRepository costConfigurations,
        IAuditLogRepository auditLogs)
    {
        _context = context;
        Subscriptions = subscriptions;
        CreditConsumptions = creditConsumptions;
        MessageCosts = messageCosts;
        UserBillingPreferences = userBillingPreferences;
        Workflows = workflows;
        Plugins = plugins;
        UserPlugins = userPlugins;
        UserCreditLimits = userCreditLimits;
        CostConfigurations = costConfigurations;
        AuditLogs = auditLogs;
    }

    public ISubscriptionRepository Subscriptions { get; }
    public ICreditConsumptionRepository CreditConsumptions { get; }
    public IMessageCostRepository MessageCosts { get; }
    public IUserBillingPreferenceRepository UserBillingPreferences { get; }
    public IWorkflowRepository Workflows { get; }
    public IPluginRepository Plugins { get; }
    public IUserPluginRepository UserPlugins { get; }
    public IUserCreditLimitRepository UserCreditLimits { get; }
    public ICostConfigurationRepository CostConfigurations { get; }
    public IAuditLogRepository AuditLogs { get; }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        // InMemory database doesn't support transactions, so we just return completed task
        return Task.CompletedTask;
    }

    public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        // InMemory database doesn't support transactions, so we just return completed task
        return Task.CompletedTask;
    }

    public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        // InMemory database doesn't support transactions, so we just return completed task
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // Nothing to dispose for InMemory transactions
    }
}