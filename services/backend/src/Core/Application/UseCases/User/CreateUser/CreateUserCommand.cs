using Domain.Entities;
using MediatR;

namespace Application.UseCases.User.CreateUser;

public record CreateUserCommand(string Email, IEnumerable<string>? Roles) : IRequest<UserEntity>;