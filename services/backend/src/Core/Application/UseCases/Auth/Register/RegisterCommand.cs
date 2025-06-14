using Domain.Entities;
using MediatR;

namespace Application.UseCases.Auth.Register;

public record RegisterCommand(string Email, string Password, IEnumerable<string>? Roles) : IRequest<UserEntity>;