using Application.UseCases.User.Common;
using MediatR;

namespace Application.UseCases.User.CreateUser;

public record CreateUserCommand(string Email, IEnumerable<string>? Roles) : IRequest<UserDto>;
