using Domain.Entities;
using MediatR;

namespace Application.UseCases.Auth.Register;

public record RegisterCommand(string Email, string Password, IEnumerable<string>? Roles) : IRequest<RegisterDto>;

public record RegisterDto(bool IsSuccess, UserEntity? User = null, string? ErrorMessage = null)
{
    public static RegisterDto Success(UserEntity user) => new(true, user);
    public static RegisterDto Failure(string message) => new(false, null, message);
}