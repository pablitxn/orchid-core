using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Infrastructure.Messaging;
using Infrastructure.Messaging.RabbitMq;
using RabbitMQ.Client;

namespace Messaging.IntegrationTests;

public class AsyncRabbitMqConsumerTests : IAsyncLifetime
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
            QueueName = "testQueueConsumer"
        };
    }

    public async Task DisposeAsync()
    {
        await _rabbitmq.DisposeAsync();
    }

    [Fact]
    public async Task Consumer_ShouldReceivePublishedMessage()
    {
        var consumer = new AsyncRabbitMqTaskConsumer(_settings);
        await consumer.InitializeAsync();

        string? receivedMessage = null;
        var waitHandle = new ManualResetEventSlim();

        consumer.OnTaskReceivedAsync += message =>
        {
            receivedMessage = message;
            waitHandle.Set();
            return Task.CompletedTask;
        };

        consumer.StartConsuming();

        var factory = new ConnectionFactory
        {
            HostName = _settings.HostName,
            Port = _settings.Port,
            UserName = _settings.UserName,
            Password = _settings.Password
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.QueueDeclareAsync(
            _settings.QueueName,
            true,
            false,
            false,
            null
        );

        const string testMessage = "Async Task from publisher";
        var body = Encoding.UTF8.GetBytes(testMessage);
        await channel.BasicPublishAsync(
            "",
            _settings.QueueName,
            body
        );

        var messageReceived = waitHandle.Wait(TimeSpan.FromSeconds(10));
        Assert.True(messageReceived, "Consumer did not receive the message in time.");
        Assert.Equal(testMessage, receivedMessage);
    }
}