using Domain.Entities;
using MediatR;

namespace Application.UseCases.Project.GetProjectById;

public record GetProjectByIdQuery(Guid ProjectId) : IRequest<ProjectEntity>;
