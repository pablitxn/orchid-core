using Application.UseCases.Project.Common;
using MediatR;

namespace Application.UseCases.Project.GetProjectById;

public record GetProjectByIdQuery(Guid ProjectId) : IRequest<ProjectDto?>;
