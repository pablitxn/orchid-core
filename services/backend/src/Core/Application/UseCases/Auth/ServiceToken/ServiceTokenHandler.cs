using Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Auth.ServiceToken;

public class ServiceTokenHandler(
    IServiceAccountRepository repository,
    ITokenService tokenService,
    IPasswordHasher passwordHasher,
    ILogger<ServiceTokenHandler> logger)
    : IRequestHandler<ServiceTokenCommand, ServiceTokenResult>
{
    private readonly IServiceAccountRepository _repository = repository;
    private readonly ITokenService _tokenService = tokenService;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly ILogger<ServiceTokenHandler> _logger = logger;

    public async Task<ServiceTokenResult> Handle(ServiceTokenCommand request, CancellationToken cancellationToken)
    {
        var serviceAccount = await _repository.GetByClientIdAsync(request.ClientId, cancellationToken);

        if (serviceAccount == null)
        {
            _logger.LogWarning(
                "Service account login attempt for unknown ClientId from IP {IpAddress}",
                request.IpAddress);
            return new ServiceTokenResult(false, ErrorMessage: "Invalid credentials");
        }

        if (!serviceAccount.IsActive)
        {
            _logger.LogWarning(
                "Login attempt for disabled service account {ClientId}",
                request.ClientId);
            return new ServiceTokenResult(false, ErrorMessage: "Service account is disabled");
        }

        if (!_passwordHasher.VerifyPassword(serviceAccount.ClientSecretHash, request.ClientSecret))
        {
            _logger.LogWarning(
                "Invalid secret for service account {ClientId} from IP {IpAddress}",
                request.ClientId, request.IpAddress);
            return new ServiceTokenResult(false, ErrorMessage: "Invalid credentials");
        }

        // Update last used timestamp
        serviceAccount.LastUsedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(serviceAccount, cancellationToken);

        var token = _tokenService.GenerateServiceAccountToken(serviceAccount);

        _logger.LogInformation(
            "Service account {ClientId} authenticated successfully",
            request.ClientId);

        return new ServiceTokenResult(true, token, "Bearer", 3600);
    }
}
