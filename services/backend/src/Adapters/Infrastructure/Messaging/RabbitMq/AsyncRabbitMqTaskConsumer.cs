using System.Text;
using Infrastructure.Messaging.RabbitMq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Infrastructure.Messaging;

/// <summary>
///     Asynchronous RabbitMQ consumer using updated .NET client API.
/// </summary>
public class AsyncRabbitMqTaskConsumer(RabbitMqSettings settings)
{
    private IChannel? _channel;
    private IConnection? _connection;

    /// <summary>
    ///     Event triggered when a task message is received.
    /// </summary>
    public event Func<string, Task>? OnTaskReceivedAsync;

    /// <summary>
    ///     Initializes the consumer by creating an asynchronous connection and channel.
    /// </summary>
    public async Task InitializeAsync()
    {
        var factory = new ConnectionFactory
        {
            HostName = settings.HostName,
            Port = settings.Port,
            UserName = settings.UserName,
            Password = settings.Password
        };

        _connection = await factory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        // Declare the queue to ensure it exists
        await _channel.QueueDeclareAsync(
            settings.QueueName,
            true,
            false,
            false,
            null
        );
    }

    /// <summary>
    ///     Starts consuming messages from the queue.
    /// </summary>
    public void StartConsuming()
    {
        if (_channel == null)
            throw new InvalidOperationException("Consumer not initialized. Call InitializeAsync() first.");

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            if (OnTaskReceivedAsync != null)
                await OnTaskReceivedAsync.Invoke(message);

            await _channel.BasicAckAsync(ea.DeliveryTag, false);
        };

        _channel.BasicConsumeAsync(
            settings.QueueName,
            false,
            consumer
        );
    }
}