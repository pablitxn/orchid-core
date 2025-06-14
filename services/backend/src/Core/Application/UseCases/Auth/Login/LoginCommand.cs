using MediatR;

namespace Application.UseCases.Auth.Login;

public record LoginCommand(string Email, string? Password = null) : IRequest<LoginResult>;

public record LoginResult(bool IsSuccess, string Token, IEnumerable<string> Roles, string ErrorMessage = "");