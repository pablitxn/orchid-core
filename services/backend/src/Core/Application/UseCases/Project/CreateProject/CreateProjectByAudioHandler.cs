using Application.Interfaces;
using Application.UseCases.Project.Common;
using Domain.Entities;
using Domain.Events;
using MediatR;

namespace Application.UseCases.Project.CreateProject;

public class CreateProjectByAudioHandler(
    IProjectRepository projectRepository,
    IEventPublisher eventPublisher
) : IRequestHandler<CreateProjectByAudioCommand, ProjectDto>
{
    private readonly IEventPublisher _eventPublisher = eventPublisher;
    private readonly IProjectRepository _projectRepository = projectRepository;

    public async Task<ProjectDto> Handle(CreateProjectByAudioCommand request, CancellationToken cancellationToken)
    {
        var audioFile = new AudioFileEntity
        {
            Id = Guid.NewGuid(),
            FileName = request.FileName,
            ContentType = request.ContentType,
            FileSize = request.FileSize,
            Url = request.Url,
            CreatedAt = DateTime.UtcNow
        };
        var project = ProjectEntity.Create(request.TargetLanguage, audioFile);
        audioFile.Project = project;

        await _projectRepository.SaveAsync(project, cancellationToken);

        var evt = new ProjectCreatedEvent(project.Id);
        await _eventPublisher.PublishAsync(evt);

        return ProjectDto.FromEntity(project);
    }
}
