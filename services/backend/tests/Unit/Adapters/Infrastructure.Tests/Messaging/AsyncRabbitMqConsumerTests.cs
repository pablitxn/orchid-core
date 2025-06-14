using System.Text;
using Infrastructure.Messaging;
using Infrastructure.Messaging.RabbitMq;
using RabbitMQ.Client;

namespace Infrastructure.Tests.Messaging;

public class AsyncRabbitMqConsumerTests
{
    private readonly RabbitMqSettings _settings = new()
    {
        HostName = "localhost",
        Port = 5672,
        UserName = "guest",
        Password = "guest",
        QueueName = "testQueueConsumer"
    };

    [Fact]
    public async Task Consumer_ShouldReceivePublishedMessage()
    {
        var consumer = new AsyncRabbitMqTaskConsumer(_settings);
        await consumer.InitializeAsync();

        string? receivedMessage = null;
        var waitHandle = new ManualResetEventSlim();

        consumer.OnTaskReceivedAsync += async message =>
        {
            receivedMessage = message;
            waitHandle.Set();
            await Task.CompletedTask;
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

        var messageReceived = waitHandle.Wait(5000);
        Assert.True(messageReceived, "Consumer did not receive the message in time.");
        Assert.Equal(testMessage, receivedMessage);
    }
}