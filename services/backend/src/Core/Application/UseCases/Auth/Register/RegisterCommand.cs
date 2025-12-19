using Domain.Entities;
using MediatR;

namespace Application.UseCases.Auth.Register;

public record RegisterCommand(string Email, string Password, IEnumerable<string>? Roles) : IRequest<RegisterResult>;

public record RegisterResult(bool IsSuccess, UserEntity? User = null, string? ErrorMessage = null)
{
    public static RegisterResult Success(UserEntity user) => new(true, user);
    public static RegisterResult Failure(string message) => new(false, null, message);
}