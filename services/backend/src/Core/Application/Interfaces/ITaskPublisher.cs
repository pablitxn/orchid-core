// File: src/MyApp.Application/Interfaces/ITaskPublisher.cs

namespace Application.Interfaces;

/// <summary>
///     Port for publishing tasks/messages.
/// </summary>
public interface ITaskPublisher
{
    /// <summary>
    ///     Publishes a task message.
    /// </summary>
    /// <param name="taskMessage">The message to be published.</param>
    void PublishTask(string taskMessage);

    Task PublishTaskAsync(string taskMessage);
}