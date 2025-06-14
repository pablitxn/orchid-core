using AudioProcessingWorker.Messaging;
using MassTransit;
using Microsoft.Extensions.Logging;

// using Domain.Events;

namespace AudioProcessingWorker.Handlers;

public abstract class ProjectCreatedHandler(ILogger<ProjectCreatedHandler> logger, IPublishEndpoint publishEndpoint)
    : IConsumer<ProjectCreatedMessage>
{
    public async Task Consume(ConsumeContext<ProjectCreatedMessage> context)
    {
        var msg = context.Message;
        logger.LogInformation(
            "[Worker] Received message: ProjectCreatedMessage with ProjectId={ProjectId}, Name={ProjectName}, CreatedAt={CreatedAt}",
            msg.ProjectId, msg.ProjectName, msg.CreatedAt);

        // Simulate processing (e.g., audio normalization)
        await Task.Delay(500);

        // Publish AudioNormalizedEvent
        // var audioNormalizedEvent = new AudioNormalizedEvent(msg.ProjectId,
        //     msg.ProjectName,
        //     DateTime.Now,
        //     $"Audio normalized successfully for project {msg.ProjectId}");
        //
        // await publishEndpoint.Publish(audioNormalizedEvent);

        logger.LogInformation("[Worker] Processing completed for project {ProjectId}", msg.ProjectId);
    }
}