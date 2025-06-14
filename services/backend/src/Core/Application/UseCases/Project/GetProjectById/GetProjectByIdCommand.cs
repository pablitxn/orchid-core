using Domain.Entities;
using MediatR;

namespace Application.UseCases.Project.GetProjectById;

public record GetProjectByIdCommand(Guid ProjectId) : IRequest<ProjectEntity>;