using System.Text;
using Application.Interfaces;
using RabbitMQ.Client;

namespace Infrastructure.Messaging.RabbitMq;

/// <summary>
///     Asynchronous RabbitMQ publisher using updated .NET client API.
/// </summary>
public class RabbitMqSettings
{
    public required string HostName { get; set; } = "localhost";
    public required int Port { get; set; } = 5672;
    public required string UserName { get; set; } = "guest";
    public required string Password { get; set; } = "guest";
    public required string QueueName { get; set; } = "task_queue";
}

public class AsyncRabbitMqPublisher(RabbitMqSettings settings) : ITaskPublisher
{
    private readonly RabbitMqSettings _settings = settings; // fix me 
    private IConnection? _connection;

    /// <summary>
    ///     Publishes a task message asynchronously.
    /// </summary>
    /// <param name="taskMessage">Message to publish.</param>
    public async Task PublishTaskAsync(string taskMessage)
    {
        if (_connection == null)
            throw new InvalidOperationException("Publisher not initialized. Call InitializeAsync() first.");

        await using var channel = await _connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(
            _settings.QueueName,
            true,
            false,
            false,
            null
        );

        var body = Encoding.UTF8.GetBytes(taskMessage);

        await channel.BasicPublishAsync(
            string.Empty,
            _settings.QueueName,
            body
        );
    }

    public void PublishTask(string taskMessage)
    {
        PublishTaskAsync(taskMessage).GetAwaiter().GetResult();
    }

    /// <summary>
    ///     Initializes the publisher by creating an asynchronous connection.
    /// </summary>
    public async Task InitializeAsync()
    {
        var factory = new ConnectionFactory
        {
            HostName = _settings.HostName,
            Port = _settings.Port,
            UserName = _settings.UserName,
            Password = _settings.Password
        };
        _connection = await factory.CreateConnectionAsync();
    }
}