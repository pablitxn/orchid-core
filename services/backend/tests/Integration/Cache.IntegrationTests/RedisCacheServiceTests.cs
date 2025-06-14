using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Infrastructure.Cache;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cache.IntegrationTests;

public class RedisCacheServiceTests : IAsyncLifetime
{
    private readonly TestcontainersContainer _redis = new TestcontainersBuilder<TestcontainersContainer>()
        .WithImage("redis:7-alpine")
        .WithPortBinding(6379, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6379))
        .Build();

    private string _connection = string.Empty;

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        var port = _redis.GetMappedPublicPort(6379);
        _connection = $"localhost:{port}";
    }

    public async Task DisposeAsync()
    {
        await _redis.DisposeAsync();
    }

    [Fact]
    public async Task SetAndGet_Works()
    {
        var svc = new RedisCacheService(_connection, NullLogger<RedisCacheService>.Instance);
        await svc.StartAsync(CancellationToken.None);
        await svc.SetStringAsync("key", "value", TimeSpan.FromMinutes(1));
        var result = await svc.GetStringAsync("key");
        Assert.Equal("value", result);
    }
}