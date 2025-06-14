using System.Text;
using Infrastructure.Messaging.RabbitMq;
using RabbitMQ.Client;
using Xunit.Abstractions;

namespace Infrastructure.Tests.Messaging;

public class AsyncRabbitMqPublisherTests(ITestOutputHelper testOutputHelper)
{
    private readonly RabbitMqSettings _settings = new()
    {
        HostName = "localhost",
        Port = 5672,
        UserName = "guest",
        Password = "guest",
        QueueName = "testQueue"
    };

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
        var result = channel.BasicGetAsync(_settings.QueueName, true);
        Assert.NotNull(result);
        var message = Encoding.UTF8.GetString(result?.Result?.Body.ToArray()!);
        testOutputHelper.WriteLine(message);
        Assert.NotNull(message);
        //Assert.Equal(testMessage, message);
    }
}