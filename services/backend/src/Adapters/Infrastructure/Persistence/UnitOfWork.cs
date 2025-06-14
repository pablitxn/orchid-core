using Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private IDbContextTransaction? _transaction;
    
    public UnitOfWork(ApplicationDbContext context,
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

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}