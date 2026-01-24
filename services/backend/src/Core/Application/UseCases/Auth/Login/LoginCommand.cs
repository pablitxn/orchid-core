using MediatR;

namespace Application.UseCases.Auth.Login;

public record LoginCommand(
    string Email,
    string Password,
    string? IpAddress = null,
    string? UserAgent = null) : IRequest<LoginDto>;

public record LoginDto(bool IsSuccess, string Token, IEnumerable<string> Roles, Guid UserId = default, string ErrorMessage = "");