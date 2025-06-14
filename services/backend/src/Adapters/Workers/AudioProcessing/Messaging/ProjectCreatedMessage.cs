namespace AudioProcessingWorker.Messaging;

public record ProjectCreatedMessage
{
    public Guid ProjectId { get; init; }
    public string ProjectName { get; init; } = default!;

    public DateTime CreatedAt { get; init; }
    // Agrega aquí la info que quieras para procesar el proyecto
}