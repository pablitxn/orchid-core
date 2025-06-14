using Domain.Common;
using Domain.Events;

namespace Domain.Entities;

public class ProjectEntity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    // Parameterless constructor for EF Core
    private ProjectEntity()
    {
    }

    // Domain constructor used by business logic
    private ProjectEntity(string targetLanguage, AudioFileEntity audioFile)
    {
        Id = Guid.NewGuid();
        Name = "New Project"; // todo: fixme
        TargetLanguage = targetLanguage;
        AudioFile = audioFile;

        AddDomainEvent(new ProjectCreatedEvent(Id));
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string TargetLanguage { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public AudioFileEntity AudioFile { get; private set; }
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public static ProjectEntity Create(string targetLanguage, AudioFileEntity audioFile)
    {
        return new ProjectEntity(targetLanguage, audioFile);
    }

    private void AddDomainEvent(IDomainEvent eventItem)
    {
        _domainEvents.Add(eventItem);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}