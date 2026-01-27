using System.Text.Json;
using Application.Interfaces;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Ai.AgentFramework.Abstractions;
using Infrastructure.Ai.AgentFramework.Services;
using Infrastructure.Ai.AgentFramework.Tools;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace AgentFramework.IntegrationTests;

/// <summary>
/// Integration tests for the OpenAPI plugin feature.
/// Tests the complete flow: register plugin → configure agent → invoke tool.
/// Uses WireMock to simulate external API servers.
/// </summary>
public class OpenApiPluginIntegrationTests : IAsyncLifetime
{
    private WireMockServer _mockServer = null!;
    private ApplicationDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private IToolRegistry _toolRegistry = null!;
    private IOpenApiToolFactory _openApiToolFactory = null!;

    private static readonly Guid TestUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private const string SampleOpenApiSpec = @"{
        ""openapi"": ""3.0.0"",
        ""info"": { ""title"": ""Test Plugin"", ""version"": ""1.0.0"" },
        ""paths"": {
            ""/analyze"": {
                ""post"": {
                    ""operationId"": ""analyzeData"",
                    ""summary"": ""Analyze input data"",
                    ""requestBody"": {
                        ""content"": {
                            ""application/json"": {
                                ""schema"": {
                                    ""type"": ""object"",
                                    ""properties"": {
                                        ""text"": { ""type"": ""string"" }
                                    }
                                }
                            }
                        }
                    },
                    ""responses"": { ""200"": { ""description"": ""Analysis result"" } }
                }
            },
            ""/status"": {
                ""get"": {
                    ""operationId"": ""getStatus"",
                    ""summary"": ""Get service status"",
                    ""responses"": { ""200"": { ""description"": ""Status response"" } }
                }
            },
            ""/items/{itemId}"": {
                ""get"": {
                    ""operationId"": ""getItem"",
                    ""summary"": ""Get item by ID"",
                    ""parameters"": [
                        { ""name"": ""itemId"", ""in"": ""path"", ""required"": true, ""schema"": { ""type"": ""string"" } }
                    ],
                    ""responses"": { ""200"": { ""description"": ""Item details"" } }
                }
            }
        }
    }";

    public async Task InitializeAsync()
    {
        // Start WireMock server
        _mockServer = WireMockServer.Start();

        // Setup mock responses
        SetupMockResponses();

        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDb_OpenApiPlugin_" + Guid.NewGuid())
            .Options;
        _context = new ApplicationDbContext(options);

        // Seed test data
        await SeedTestDataAsync();

        // Setup DI
        SetupServices();
    }

    public Task DisposeAsync()
    {
        _mockServer?.Stop();
        _mockServer?.Dispose();
        _context?.Dispose();
        _serviceProvider?.Dispose();
        return Task.CompletedTask;
    }

    private void SetupMockResponses()
    {
        // OpenAPI spec endpoint
        _mockServer
            .Given(Request.Create().WithPath("/openapi.json").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(SampleOpenApiSpec));

        // Status endpoint
        _mockServer
            .Given(Request.Create().WithPath("/status").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(@"{""status"": ""healthy"", ""version"": ""1.0.0""}"));

        // Analyze endpoint
        _mockServer
            .Given(Request.Create().WithPath("/analyze").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(@"{""result"": ""analyzed"", ""sentiment"": ""positive""}"));

        // Get item endpoint (with path parameter)
        _mockServer
            .Given(Request.Create().WithPath("/items/*").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(@"{""id"": ""123"", ""name"": ""Test Item"", ""price"": 19.99}"));
    }

    private async Task SeedTestDataAsync()
    {
        // Add test user
        var user = new UserEntity
        {
            Id = TestUserId,
            Email = "test@openapiplugin.com",
            Name = "Test User",
            PasswordHash = "hash"
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
    }

    private void SetupServices()
    {
        var services = new ServiceCollection();

        // Add logging
        services.AddSingleton(new Mock<ILogger<OpenApiToolFactory>>().Object);
        services.AddSingleton(new Mock<ILogger<OpenApiTool>>().Object);
        services.AddSingleton(new Mock<ILogger<ToolRegistry>>().Object);

        // Add mock activity publisher
        services.AddSingleton(new Mock<IActivityPublisher>().Object);

        // Add HTTP client factory with base address pointing to WireMock
        services.AddHttpClient(string.Empty, client =>
        {
            client.BaseAddress = new Uri(_mockServer.Url!);
        });

        // Add mock dependencies for builtin tools
        services.AddSingleton(new Mock<ILogger<VectorStoreTool>>().Object);
        services.AddSingleton(new Mock<MediatR.IMediator>().Object);
        services.AddSingleton(new Mock<ILogger<WebSearchTool>>().Object);
        services.AddSingleton(new Mock<ISearchProvider>().Object);
        services.AddSingleton(new Mock<IChatCompletionPort>().Object);
        services.AddSingleton(new Mock<IEmbeddingGeneratorPort>().Object);
        services.AddSingleton(new Mock<IVectorStorePort>().Object);

        // Add builtin tools
        services.AddTransient<VectorStoreTool>();
        services.AddTransient<WebSearchTool>();

        // Add plugin repository
        services.AddSingleton<IPluginRepository>(sp =>
            new InMemoryPluginRepository(_context));

        // Add OpenAPI tool factory
        services.AddScoped<IOpenApiToolFactory>(sp =>
            new OpenApiToolFactory(
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<IActivityPublisher>(),
                sp.GetRequiredService<IPluginRepository>(),
                sp.GetRequiredService<ILogger<OpenApiToolFactory>>(),
                sp.GetRequiredService<ILogger<OpenApiTool>>()));

        // Add tool registry with OpenAPI support
        services.AddScoped<IToolRegistry>(sp =>
            new ToolRegistry(
                sp,
                sp.GetRequiredService<ILogger<ToolRegistry>>(),
                sp.GetRequiredService<IOpenApiToolFactory>(),
                sp.GetRequiredService<IPluginRepository>()));

        _serviceProvider = services.BuildServiceProvider();
        _toolRegistry = _serviceProvider.GetRequiredService<IToolRegistry>();
        _openApiToolFactory = _serviceProvider.GetRequiredService<IOpenApiToolFactory>();
    }

    #region End-to-End Tests

    [Fact]
    public async Task EndToEnd_RegisterPlugin_ConfigureAgent_InvokeTool()
    {
        // ARRANGE: Register a new OpenAPI plugin
        var pluginId = Guid.NewGuid();
        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Test Analysis API",
            Description = "A test API for analyzing data",
            SystemName = "test_analysis_api",
            PluginType = "openapi",
            BaseUrl = _mockServer.Url,
            OpenApiSpecUrl = "/openapi.json",
            PriceCredits = 3,
            IsSubscriptionBased = false,
            IsActive = true
        };

        await _context.Plugins.AddAsync(plugin);
        await _context.SaveChangesAsync();

        // ARRANGE: Configure an agent with this plugin
        var agentId = Guid.NewGuid();
        var agent = new AgentEntity
        {
            Id = agentId,
            Name = "Analysis Agent",
            UserId = TestUserId,
            PluginIds = new[] { pluginId }
        };

        await _context.Agents.AddAsync(agent);
        await _context.SaveChangesAsync();

        // ACT: Resolve the tool through ToolRegistry
        var tool = await _toolRegistry.GetToolBySystemNameAsync("test_analysis_api");

        // ASSERT: Tool should be resolved correctly
        tool.Should().NotBeNull();
        tool!.SystemName.Should().Be("test_analysis_api");
        tool.DisplayName.Should().Be("Test Analysis API");
        tool.CreditCost.Should().Be(3);
        tool.RequiresSubscription.Should().BeFalse();

        // ACT: Get AI functions from the tool
        var functions = tool.GetAIFunctions();

        // ASSERT: Should have 3 functions from the spec
        functions.Should().HaveCount(3);
        functions.Should().Contain(f => f.Name == "analyzeData");
        functions.Should().Contain(f => f.Name == "getStatus");
        functions.Should().Contain(f => f.Name == "getItem");

        // ACT: Invoke the getStatus function
        var statusFunc = functions.First(f => f.Name == "getStatus");
        var statusArgs = new Microsoft.Extensions.AI.AIFunctionArguments
        {
            ["parameters"] = JsonSerializer.SerializeToNode(new { })
        };
        var statusResult = await statusFunc.InvokeAsync(statusArgs);

        // ASSERT: Should get successful response
        statusResult.Should().NotBeNull();
        var statusJson = statusResult!.ToString();
        statusJson.Should().Contain("\"success\":true");
        statusJson.Should().Contain("\"statusCode\":200");
        statusJson.Should().Contain("healthy");

        // ACT: Invoke the analyzeData function with request body
        var analyzeFunc = functions.First(f => f.Name == "analyzeData");
        var analyzeArgs = new Microsoft.Extensions.AI.AIFunctionArguments
        {
            ["parameters"] = JsonSerializer.SerializeToNode(new
            {
                requestBody = new { text = "This is a test message" }
            })
        };
        var analyzeResult = await analyzeFunc.InvokeAsync(analyzeArgs);

        // ASSERT: Should get analysis response
        analyzeResult.Should().NotBeNull();
        var analyzeJson = analyzeResult!.ToString();
        analyzeJson.Should().Contain("\"success\":true");
        analyzeJson.Should().Contain("analyzed");

        // ACT: Invoke the getItem function with path parameter
        var getItemFunc = functions.First(f => f.Name == "getItem");
        var getItemArgs = new Microsoft.Extensions.AI.AIFunctionArguments
        {
            ["parameters"] = JsonSerializer.SerializeToNode(new { itemId = "item-456" })
        };
        var getItemResult = await getItemFunc.InvokeAsync(getItemArgs);

        // ASSERT: Should get item response
        getItemResult.Should().NotBeNull();
        var getItemJson = getItemResult!.ToString();
        getItemJson.Should().Contain("\"success\":true");
        getItemJson.Should().Contain("Test Item");
    }

    [Fact]
    public async Task EndToEnd_BuiltinToolsRemainAccessible()
    {
        // Verify that builtin tools still work alongside OpenAPI plugins
        var vectorStoreTool = _toolRegistry.GetToolBySystemName("vector_store");
        var webSearchTool = _toolRegistry.GetToolBySystemName("web_search");

        vectorStoreTool.Should().NotBeNull();
        webSearchTool.Should().NotBeNull();

        // Also verify async resolution for builtins (should not hit database)
        var asyncVectorStore = await _toolRegistry.GetToolBySystemNameAsync("vector_store");
        asyncVectorStore.Should().NotBeNull();
    }

    [Fact]
    public async Task EndToEnd_InactivePluginNotResolved()
    {
        // ARRANGE: Register an inactive plugin
        var plugin = new PluginEntity
        {
            Id = Guid.NewGuid(),
            Name = "Inactive API",
            SystemName = "inactive_api",
            PluginType = "openapi",
            BaseUrl = _mockServer.Url,
            OpenApiSpecUrl = "/openapi.json",
            IsActive = false
        };

        await _context.Plugins.AddAsync(plugin);
        await _context.SaveChangesAsync();

        // ACT
        var tool = await _toolRegistry.GetToolBySystemNameAsync("inactive_api");

        // ASSERT
        tool.Should().BeNull();
    }

    [Fact]
    public async Task EndToEnd_NonOpenApiPluginNotHandled()
    {
        // ARRANGE: Register a non-OpenAPI plugin
        var plugin = new PluginEntity
        {
            Id = Guid.NewGuid(),
            Name = "Builtin Style Plugin",
            SystemName = "builtin_style_plugin",
            PluginType = "builtin",
            IsActive = true
        };

        await _context.Plugins.AddAsync(plugin);
        await _context.SaveChangesAsync();

        // ACT
        var tool = await _toolRegistry.GetToolBySystemNameAsync("builtin_style_plugin");

        // ASSERT: Should not be resolved because factory can't handle non-openapi plugins
        tool.Should().BeNull();
    }

    #endregion

    #region Spec Caching Tests

    [Fact]
    public async Task SpecCaching_ValidCacheUsedWithoutFetch()
    {
        // ARRANGE: Create plugin with fresh cache
        var plugin = new PluginEntity
        {
            Id = Guid.NewGuid(),
            Name = "Cached Plugin",
            SystemName = "cached_plugin",
            PluginType = "openapi",
            BaseUrl = _mockServer.Url,
            OpenApiSpecUrl = "/openapi.json",
            OpenApiSpecCache = SampleOpenApiSpec,
            SpecCachedAt = DateTime.UtcNow.AddMinutes(-30), // 30 min ago (within 1h TTL)
            IsActive = true
        };

        await _context.Plugins.AddAsync(plugin);
        await _context.SaveChangesAsync();

        // Clear WireMock logs to verify no requests made
        _mockServer.ResetLogEntries();

        // ACT
        var tool = await _toolRegistry.GetToolBySystemNameAsync("cached_plugin");

        // ASSERT
        tool.Should().NotBeNull();

        // Verify no HTTP request was made to fetch spec
        _mockServer.LogEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task SpecCaching_ExpiredCacheTriggersFetch()
    {
        // ARRANGE: Create plugin with expired cache
        var plugin = new PluginEntity
        {
            Id = Guid.NewGuid(),
            Name = "Expired Cache Plugin",
            SystemName = "expired_cache_plugin",
            PluginType = "openapi",
            BaseUrl = _mockServer.Url,
            OpenApiSpecUrl = "/openapi.json",
            OpenApiSpecCache = SampleOpenApiSpec,
            SpecCachedAt = DateTime.UtcNow.AddHours(-2), // 2 hours ago (expired)
            IsActive = true
        };

        await _context.Plugins.AddAsync(plugin);
        await _context.SaveChangesAsync();

        // Clear WireMock logs
        _mockServer.ResetLogEntries();

        // ACT
        var tool = await _toolRegistry.GetToolBySystemNameAsync("expired_cache_plugin");

        // ASSERT
        tool.Should().NotBeNull();

        // Verify HTTP request was made to refresh spec
        _mockServer.LogEntries.Should().Contain(e =>
            e.RequestMessage.Path == "/openapi.json");
    }

    [Fact]
    public async Task SpecCaching_EmptyCacheTriggersFetch()
    {
        // ARRANGE: Create plugin without cache
        var plugin = new PluginEntity
        {
            Id = Guid.NewGuid(),
            Name = "No Cache Plugin",
            SystemName = "no_cache_plugin",
            PluginType = "openapi",
            BaseUrl = _mockServer.Url,
            OpenApiSpecUrl = "/openapi.json",
            OpenApiSpecCache = null,
            SpecCachedAt = null,
            IsActive = true
        };

        await _context.Plugins.AddAsync(plugin);
        await _context.SaveChangesAsync();

        // Clear WireMock logs
        _mockServer.ResetLogEntries();

        // ACT
        var tool = await _toolRegistry.GetToolBySystemNameAsync("no_cache_plugin");

        // ASSERT
        tool.Should().NotBeNull();

        // Verify HTTP request was made to fetch spec
        _mockServer.LogEntries.Should().Contain(e =>
            e.RequestMessage.Path == "/openapi.json");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ErrorHandling_ServerUnavailable_ReturnsErrorResult()
    {
        // ARRANGE: Create plugin pointing to a non-existent endpoint
        _mockServer
            .Given(Request.Create().WithPath("/failing-endpoint").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(503)
                .WithBody(@"{""error"": ""Service Unavailable""}"));

        var failingSpec = @"{
            ""openapi"": ""3.0.0"",
            ""info"": { ""title"": ""Failing API"", ""version"": ""1.0.0"" },
            ""paths"": {
                ""/failing-endpoint"": {
                    ""get"": {
                        ""operationId"": ""failingOperation"",
                        ""responses"": { ""200"": { ""description"": ""OK"" } }
                    }
                }
            }
        }";

        var plugin = new PluginEntity
        {
            Id = Guid.NewGuid(),
            Name = "Failing API",
            SystemName = "failing_api",
            PluginType = "openapi",
            BaseUrl = _mockServer.Url,
            OpenApiSpecCache = failingSpec,
            SpecCachedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _context.Plugins.AddAsync(plugin);
        await _context.SaveChangesAsync();

        // ACT
        var tool = await _toolRegistry.GetToolBySystemNameAsync("failing_api");
        tool.Should().NotBeNull();

        var functions = tool!.GetAIFunctions();
        var failFunc = functions.First(f => f.Name == "failingOperation");

        var args = new Microsoft.Extensions.AI.AIFunctionArguments
        {
            ["parameters"] = JsonSerializer.SerializeToNode(new { })
        };
        var result = await failFunc.InvokeAsync(args);

        // ASSERT: Should get error response but not throw
        result.Should().NotBeNull();
        var resultJson = result!.ToString();
        resultJson.Should().Contain("\"success\":false");
        resultJson.Should().Contain("503");
    }

    [Fact]
    public async Task ErrorHandling_InvalidSpecUrl_ReturnsNull()
    {
        // ARRANGE: Create plugin with spec that can't be fetched
        _mockServer
            .Given(Request.Create().WithPath("/invalid-spec").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(404)
                .WithBody("Not Found"));

        var plugin = new PluginEntity
        {
            Id = Guid.NewGuid(),
            Name = "Invalid Spec Plugin",
            SystemName = "invalid_spec_plugin",
            PluginType = "openapi",
            BaseUrl = _mockServer.Url,
            OpenApiSpecUrl = "/invalid-spec",
            OpenApiSpecCache = null,
            SpecCachedAt = null,
            IsActive = true
        };

        await _context.Plugins.AddAsync(plugin);
        await _context.SaveChangesAsync();

        // ACT
        var tool = await _toolRegistry.GetToolBySystemNameAsync("invalid_spec_plugin");

        // ASSERT: Should return null when spec can't be fetched
        tool.Should().BeNull();
    }

    #endregion

    #region Agent Plugin Association Tests

    [Fact]
    public async Task AgentPluginAssociation_AgentCanAccessConfiguredPlugins()
    {
        // ARRANGE: Create multiple plugins
        var plugin1Id = Guid.NewGuid();
        var plugin2Id = Guid.NewGuid();

        var plugin1 = new PluginEntity
        {
            Id = plugin1Id,
            Name = "Plugin 1",
            SystemName = "plugin_1",
            PluginType = "openapi",
            BaseUrl = _mockServer.Url,
            OpenApiSpecCache = SampleOpenApiSpec,
            SpecCachedAt = DateTime.UtcNow,
            IsActive = true
        };

        var plugin2 = new PluginEntity
        {
            Id = plugin2Id,
            Name = "Plugin 2",
            SystemName = "plugin_2",
            PluginType = "openapi",
            BaseUrl = _mockServer.Url,
            OpenApiSpecCache = SampleOpenApiSpec,
            SpecCachedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _context.Plugins.AddRangeAsync(plugin1, plugin2);
        await _context.SaveChangesAsync();

        // Create agent with only plugin1
        var agent = new AgentEntity
        {
            Id = Guid.NewGuid(),
            Name = "Limited Agent",
            UserId = TestUserId,
            PluginIds = new[] { plugin1Id }
        };

        await _context.Agents.AddAsync(agent);
        await _context.SaveChangesAsync();

        // ACT: Query plugins configured for this agent
        var savedAgent = await _context.Agents.FindAsync(agent.Id);
        var configuredPlugins = await _context.Plugins
            .Where(p => savedAgent!.PluginIds.Contains(p.Id) && p.IsActive)
            .ToListAsync();

        // ASSERT
        configuredPlugins.Should().HaveCount(1);
        configuredPlugins.Should().Contain(p => p.SystemName == "plugin_1");
        configuredPlugins.Should().NotContain(p => p.SystemName == "plugin_2");
    }

    #endregion

    /// <summary>
    /// In-memory implementation of IPluginRepository for integration tests.
    /// </summary>
    private sealed class InMemoryPluginRepository : IPluginRepository
    {
        private readonly ApplicationDbContext _context;

        public InMemoryPluginRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PluginEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.Plugins.FindAsync(new object[] { id }, cancellationToken);
        }

        public async Task<PluginEntity?> GetBySystemNameAsync(string systemName, CancellationToken cancellationToken = default)
        {
            return await _context.Plugins
                .FirstOrDefaultAsync(p => p.SystemName == systemName, cancellationToken);
        }

        public async Task<List<PluginEntity>> ListAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Plugins.ToListAsync(cancellationToken);
        }

        public async Task<List<PluginEntity>> ListActiveAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Plugins
                .Where(p => p.IsActive)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<PluginEntity>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            // Plugins are global in this implementation, return all active plugins
            return await _context.Plugins
                .Where(p => p.IsActive)
                .ToListAsync(cancellationToken);
        }

        public async Task CreateAsync(PluginEntity plugin, CancellationToken cancellationToken = default)
        {
            await _context.Plugins.AddAsync(plugin, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateAsync(PluginEntity plugin, CancellationToken cancellationToken = default)
        {
            _context.Plugins.Update(plugin);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var plugin = await _context.Plugins.FindAsync(new object[] { id }, cancellationToken);
            if (plugin != null)
            {
                _context.Plugins.Remove(plugin);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task<List<PluginEntity>> ListByAgentAsync(Guid agentId, CancellationToken cancellationToken = default)
        {
            var agent = await _context.Agents.FindAsync(new object[] { agentId }, cancellationToken);
            if (agent?.PluginIds == null || agent.PluginIds.Length == 0)
            {
                return new List<PluginEntity>();
            }

            return await _context.Plugins
                .Where(p => agent.PluginIds.Contains(p.Id) && p.IsActive)
                .ToListAsync(cancellationToken);
        }
    }
}
