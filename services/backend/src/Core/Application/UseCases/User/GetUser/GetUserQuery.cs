using Domain.Entities;
using MediatR;

namespace Application.UseCases.User.GetUser;

public record GetUserQuery(string Email) : IRequest<UserEntity>;
