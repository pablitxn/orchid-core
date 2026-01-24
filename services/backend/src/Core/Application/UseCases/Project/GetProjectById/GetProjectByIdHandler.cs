using Application.Interfaces;
using Application.UseCases.Project.Common;
using MediatR;

namespace Application.UseCases.Project.GetProjectById;

public class GetProjectByIdHandler(IProjectRepository projectRepository)
    : IRequestHandler<GetProjectByIdQuery, ProjectDto?>
{
    private readonly IProjectRepository _projectRepository = projectRepository;

    public async Task<ProjectDto?> Handle(GetProjectByIdQuery request, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(request.ProjectId, cancellationToken);
        return project != null ? ProjectDto.FromEntity(project) : null;
    }
}
