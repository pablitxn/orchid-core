using MediatR;

namespace Application.UseCases.Auth.ServiceToken;

public record ServiceTokenCommand(
    string ClientId,
    string ClientSecret,
    string? IpAddress = null) : IRequest<ServiceTokenDto>;

public record ServiceTokenDto(
    bool IsSuccess,
    string AccessToken = "",
    string TokenType = "Bearer",
    int ExpiresIn = 3600,
    string ErrorMessage = "");
