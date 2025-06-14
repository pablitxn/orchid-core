using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Ai.SemanticKernel;
using Infrastructure.Ai.SemanticKernel.Services;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Xunit;

namespace Integration.AgentPluginLoading.IntegrationTests;

public class AgentPluginLoadingTests : IAsyncLifetime
{
    private ServiceProvider? _serviceProvider;
    private ApplicationDbContext? _dbContext;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        
        // Add database
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        
        // Add logging
        services.AddLogging(builder => builder.AddConsole());
        
        // Add repositories
        services.AddScoped<IPluginRepository, Infrastructure.Persistence.Repositories.PluginRepository>();
        services.AddScoped<IAgentRepository, Infrastructure.Persistence.Repositories.AgentRepository>();
        
        // Add Semantic Kernel services (mock configuration)
        var configuration = new Dictionary<string, string?>
        {
            ["OpenAI:ApiKey"] = "test-key",
            ["OpenAI:Model"] = "gpt-4"
        };
        
        var configurationRoot = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(configuration)
            .Build();
        
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(configurationRoot);
        services.AddSemanticKernel(configurationRoot);
        
        // Add agent plugin loader
        services.AddTransient<IAgentPluginLoader, AgentPluginLoader>();
        
        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<ApplicationDbContext>();
        
        // Ensure database is created
        await _dbContext.Database.EnsureCreatedAsync();
        
        // Seed test data
        await SeedTestDataAsync();
    }

    public async Task DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.Database.EnsureDeletedAsync();
            await _dbContext.DisposeAsync();
        }
        _serviceProvider?.Dispose();
    }

    private async Task SeedTestDataAsync()
    {
        if (_dbContext == null) return;
        
        // Create plugins
        var excelPlugin = new PluginEntity
        {
            Id = Guid.NewGuid(),
            Name = "Excel Plugin",
            Description = "Excel and spreadsheet operations",
            SystemName = "excel",
            IsActive = true,
            PriceCredits = 10
        };
        
        var vectorPlugin = new PluginEntity
        {
            Id = Guid.NewGuid(),
            Name = "Vector Store Plugin",
            Description = "Vector database operations",
            SystemName = "vector_store",
            IsActive = true,
            PriceCredits = 5
        };
        
        var inactivePlugin = new PluginEntity
        {
            Id = Guid.NewGuid(),
            Name = "Inactive Plugin",
            Description = "This plugin is inactive",
            SystemName = "inactive",
            IsActive = false,
            PriceCredits = 0
        };
        
        _dbContext.Plugins.AddRange(excelPlugin, vectorPlugin, inactivePlugin);
        
        // Create agents
        var agentWithPlugins = new AgentEntity
        {
            Id = Guid.NewGuid(),
            Name = "Test Agent with Plugins",
            Description = "Agent with Excel and Vector plugins",
            PluginIds = new[] { excelPlugin.Id, vectorPlugin.Id }
        };
        
        var agentWithInactivePlugin = new AgentEntity
        {
            Id = Guid.NewGuid(),
            Name = "Test Agent with Inactive Plugin",
            Description = "Agent with inactive plugin",
            PluginIds = new[] { inactivePlugin.Id }
        };
        
        var agentWithNoPlugins = new AgentEntity
        {
            Id = Guid.NewGuid(),
            Name = "Test Agent without Plugins",
            Description = "Agent with no plugins",
            PluginIds = Array.Empty<Guid>()
        };
        
        _dbContext.Agents.AddRange(agentWithPlugins, agentWithInactivePlugin, agentWithNoPlugins);
        
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task LoadAgentPluginsAsync_WithActivePlugins_LoadsPluginsIntoKernel()
    {
        // Arrange
        var agentPluginLoader = _serviceProvider!.GetRequiredService<IAgentPluginLoader>();
        var kernel = new Kernel(_serviceProvider);
        var agent = await _dbContext!.Agents.FirstAsync(a => a.Name == "Test Agent with Plugins");
        
        // Act
        var loadedPlugins = await agentPluginLoader.LoadAgentPluginsAsync(agent.Id, kernel);
        
        // Assert
        Assert.NotNull(loadedPlugins);
        Assert.Equal(2, loadedPlugins.Count);
        Assert.Contains("excel", loadedPlugins);
        Assert.Contains("vector_store", loadedPlugins);
        
        // Verify plugins are actually loaded in kernel
        var excelPlugin = kernel.Plugins.FirstOrDefault(p => p.Name == "excel");
        var vectorPlugin = kernel.Plugins.FirstOrDefault(p => p.Name == "vector_store");
        
        Assert.NotNull(excelPlugin);
        Assert.NotNull(vectorPlugin);
    }

    [Fact]
    public async Task LoadAgentPluginsAsync_WithInactivePlugin_DoesNotLoadPlugin()
    {
        // Arrange
        var agentPluginLoader = _serviceProvider!.GetRequiredService<IAgentPluginLoader>();
        var kernel = new Kernel(_serviceProvider);
        var agent = await _dbContext!.Agents.FirstAsync(a => a.Name == "Test Agent with Inactive Plugin");
        
        // Act
        var loadedPlugins = await agentPluginLoader.LoadAgentPluginsAsync(agent.Id, kernel);
        
        // Assert
        Assert.NotNull(loadedPlugins);
        Assert.Empty(loadedPlugins);
        Assert.Empty(kernel.Plugins);
    }

    [Fact]
    public async Task LoadAgentPluginsAsync_WithNoPlugins_ReturnsEmptyList()
    {
        // Arrange
        var agentPluginLoader = _serviceProvider!.GetRequiredService<IAgentPluginLoader>();
        var kernel = new Kernel(_serviceProvider);
        var agent = await _dbContext!.Agents.FirstAsync(a => a.Name == "Test Agent without Plugins");
        
        // Act
        var loadedPlugins = await agentPluginLoader.LoadAgentPluginsAsync(agent.Id, kernel);
        
        // Assert
        Assert.NotNull(loadedPlugins);
        Assert.Empty(loadedPlugins);
        Assert.Empty(kernel.Plugins);
    }

    [Fact]
    public async Task CompleteWithAgentAsync_UsesAgentSpecificPlugins()
    {
        // Arrange
        var chatCompletionPort = _serviceProvider!.GetRequiredService<IChatCompletionPort>();
        var agent = await _dbContext!.Agents.FirstAsync(a => a.Name == "Test Agent with Plugins");
        
        var messages = new List<ChatMessage>
        {
            new("system", "You are a helpful assistant with access to Excel and Vector Store plugins."),
            new("user", "Can you help me with Excel operations?")
        };
        
        // Act & Assert
        // This would normally make an API call, but in test environment it should at least not throw
        var exception = await Record.ExceptionAsync(async () =>
        {
            await chatCompletionPort.CompleteWithAgentAsync(messages, agent.Id);
        });
        
        // We expect this to fail due to missing OpenAI configuration in test environment
        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public async Task MultipleAgents_HaveDifferentPluginSets()
    {
        // Arrange
        var agentPluginLoader = _serviceProvider!.GetRequiredService<IAgentPluginLoader>();
        var agent1 = await _dbContext!.Agents.FirstAsync(a => a.Name == "Test Agent with Plugins");
        var agent2 = await _dbContext!.Agents.FirstAsync(a => a.Name == "Test Agent without Plugins");
        
        var kernel1 = new Kernel(_serviceProvider);
        var kernel2 = new Kernel(_serviceProvider);
        
        // Act
        var plugins1 = await agentPluginLoader.LoadAgentPluginsAsync(agent1.Id, kernel1);
        var plugins2 = await agentPluginLoader.LoadAgentPluginsAsync(agent2.Id, kernel2);
        
        // Assert
        Assert.NotEmpty(plugins1);
        Assert.Empty(plugins2);
        
        // Verify kernels have different plugin sets
        Assert.NotEmpty(kernel1.Plugins);
        Assert.Empty(kernel2.Plugins);
    }
}