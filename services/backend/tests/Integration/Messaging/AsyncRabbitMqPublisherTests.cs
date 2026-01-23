using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Infrastructure.Messaging.RabbitMq;
using RabbitMQ.Client;

namespace Messaging.IntegrationTests;

public class AsyncRabbitMqPublisherTests : IAsyncLifetime
{
    private readonly IContainer _rabbitmq = new ContainerBuilder()
        .WithImage("rabbitmq:3-alpine")
        .WithPortBinding(5672, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5672))
        .Build();

    private RabbitMqSettings _settings = null!;

    public async Task InitializeAsync()
    {
        await _rabbitmq.StartAsync();
        var port = _rabbitmq.GetMappedPublicPort(5672);
        _settings = new RabbitMqSettings
        {
            HostName = "localhost",
            Port = port,
            UserName = "guest",
            Password = "guest",
            QueueName = "testQueue"
        };
    }

    public async Task DisposeAsync()
    {
        await _rabbitmq.DisposeAsync();
    }

    [Fact]
    public async Task PublishTaskAsync_ShouldPublishMessageToQueue()
    {
        var publisher = new AsyncRabbitMqPublisher(_settings);
        await publisher.InitializeAsync();

        const string testMessage = "Hello RabbitMQ Async";
        await publisher.PublishTaskAsync(testMessage);

        var factory = new ConnectionFactory
        {
            HostName = _settings.HostName,
            Port = _settings.Port,
            UserName = _settings.UserName,
            Password = _settings.Password
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        var result = await channel.BasicGetAsync(_settings.QueueName, true);

        Assert.NotNull(result);
        var message = Encoding.UTF8.GetString(result.Body.ToArray());
        Assert.Equal(testMessage, message);
    }
}