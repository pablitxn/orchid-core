using MediatR;

namespace Application.UseCases.Auth.GoogleLogin;

public record GoogleLoginCommand(string Credential)
    : IRequest<GoogleLoginResult>;

public record GoogleLoginResult(bool IsSuccess, string Token, string ErrorMessage = "");