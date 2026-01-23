using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.UseCases.Project.GetProjectById;

public class GetProjectByIdHandler(IProjectRepository projectRepository)
    : IRequestHandler<GetProjectByIdQuery, ProjectEntity>
{
    private readonly IProjectRepository _projectRepository = projectRepository;

    public async Task<ProjectEntity> Handle(GetProjectByIdQuery request, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(request.ProjectId, cancellationToken);
        return project!;
    }
}