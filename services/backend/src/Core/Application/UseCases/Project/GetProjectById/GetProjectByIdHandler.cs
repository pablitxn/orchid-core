using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.UseCases.Project.GetProjectById;

public class GetProjectByIdHandler(IProjectRepository projectRepository)
    : IRequestHandler<GetProjectByIdCommand, ProjectEntity>
{
    private readonly IProjectRepository _projectRepository = projectRepository;

    public async Task<ProjectEntity> Handle(GetProjectByIdCommand request, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(request.ProjectId, cancellationToken);
        return project!;
    }
}