using Application.Interfaces;
using Core.Application.Interfaces;
using Core.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Auth.Login;

public class LoginHandler(
    IUserRepository userRepository,
    ITokenService tokenService,
    IPasswordHasher passwordHasher,
    ILoginHistoryRepository loginHistoryRepository,
    ILogger<LoginHandler> logger)
    : IRequestHandler<LoginCommand, LoginResult>
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(15);

    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly ITokenService _tokenService = tokenService;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly ILoginHistoryRepository _loginHistoryRepository = loginHistoryRepository;
    private readonly ILogger<LoginHandler> _logger = logger;

    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user == null)
        {
            // Don't reveal that user doesn't exist
            return new LoginResult(false, string.Empty, [], ErrorMessage: "Invalid credentials");
        }

        // Check for account lockout
        var failedAttempts = await _loginHistoryRepository.GetFailedAttemptsCountAsync(
            user.Id,
            DateTime.UtcNow.Subtract(LockoutWindow),
            cancellationToken);

        if (failedAttempts >= MaxFailedAttempts)
        {
            // Log lockout event for security monitoring
            _logger.LogWarning(
                "Account lockout triggered for UserId {UserId} from IP {IpAddress}. Failed attempts: {FailedAttempts}",
                user.Id, request.IpAddress, failedAttempts);

            // Record lockout attempt in history
            await RecordLoginAttemptAsync(user.Id, request, false, $"Account locked - {failedAttempts} failed attempts", cancellationToken);

            return new LoginResult(false, string.Empty, [], ErrorMessage: "Account temporarily locked. Please try again later.");
        }

        // Password is always required
        if (string.IsNullOrEmpty(request.Password))
        {
            await RecordLoginAttemptAsync(user.Id, request, false, "Password not provided", cancellationToken);
            return new LoginResult(false, string.Empty, [], ErrorMessage: "Password is required");
        }

        if (!_passwordHasher.VerifyPassword(user.PasswordHash, request.Password))
        {
            await RecordLoginAttemptAsync(user.Id, request, false, "Invalid password", cancellationToken);
            return new LoginResult(false, string.Empty, [], ErrorMessage: "Invalid credentials");
        }

        // Successful login
        await RecordLoginAttemptAsync(user.Id, request, true, null, cancellationToken);

        var roles = user.UserRoles?.Select(ur => ur.Role.Name) ?? [];
        var enumerable = roles as string[] ?? roles.ToArray();
        var token = _tokenService.GenerateToken(user, enumerable);
        return new LoginResult(true, token, enumerable, user.Id);
    }

    private async Task RecordLoginAttemptAsync(
        Guid userId,
        LoginCommand request,
        bool success,
        string? failureReason,
        CancellationToken cancellationToken)
    {
        var loginHistory = new LoginHistoryEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Timestamp = DateTime.UtcNow,
            IpAddress = request.IpAddress ?? "unknown",
            UserAgent = request.UserAgent,
            Success = success,
            FailureReason = failureReason
        };

        await _loginHistoryRepository.CreateAsync(loginHistory, cancellationToken);
    }
}
