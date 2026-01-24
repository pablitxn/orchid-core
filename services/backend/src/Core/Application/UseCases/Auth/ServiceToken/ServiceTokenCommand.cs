using MediatR;

namespace Application.UseCases.Auth.ServiceToken;

public record ServiceTokenCommand(
    string ClientId,
    string ClientSecret,
    string? IpAddress = null) : IRequest<ServiceTokenResult>;

public record ServiceTokenResult(
    bool IsSuccess,
    string AccessToken = "",
    string TokenType = "Bearer",
    int ExpiresIn = 3600,
    string ErrorMessage = "");
