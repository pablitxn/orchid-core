using Domain.Entities;
using MediatR;

namespace Application.UseCases.User.GetUser;

public record GetUserCommand(string Email) : IRequest<UserEntity>;