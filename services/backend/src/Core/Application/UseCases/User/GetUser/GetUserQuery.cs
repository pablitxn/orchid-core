using Application.UseCases.User.Common;
using MediatR;

namespace Application.UseCases.User.GetUser;

public record GetUserQuery(string Email) : IRequest<UserDto?>;
