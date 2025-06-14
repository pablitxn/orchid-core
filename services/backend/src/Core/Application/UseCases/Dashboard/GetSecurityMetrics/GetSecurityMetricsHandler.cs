using Application.Interfaces;
using Core.Application.Interfaces;
using Core.Application.UseCases.Dashboard.Common;
using Core.Application.UseCases.Dashboard.GetSecurityMetrics;
using MediatR;

namespace Application.UseCases.Dashboard.GetSecurityMetrics;

public sealed class GetSecurityMetricsHandler(
    IUserRepository userRepository,
    ILoginHistoryRepository loginHistoryRepository)
    : IRequestHandler<GetSecurityMetricsQuery, SecurityMetricsDto>
{
    public async Task<SecurityMetricsDto> Handle(GetSecurityMetricsQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId)
            .ConfigureAwait(false);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID {request.UserId} not found");
        }

        // Get login history
        var loginHistory = await loginHistoryRepository.GetRecentByUserIdAsync(request.UserId, 10, cancellationToken);

        var recentLogins = loginHistory
            .Select(l => new LoginHistoryDto
            {
                Timestamp = l.Timestamp,
                IpAddress = l.IpAddress,
                UserAgent = l.UserAgent,
                Location = l.Location,
                Success = l.Success
            })
            .ToList();

        var failedAttempts = await loginHistoryRepository.GetFailedAttemptsCountAsync(
            request.UserId,
            DateTime.UtcNow.AddDays(-30),
            cancellationToken
        );

        // Get unique devices from successful logins
        var activeDevices = loginHistory
            .Where(l => l.Success && !string.IsNullOrEmpty(l.UserAgent))
            .Select(l => l.UserAgent!)
            .Distinct()
            .Take(5)
            .ToList();

        return new SecurityMetricsDto
        {
            LastLogin = loginHistory.FirstOrDefault(l => l.Success)?.Timestamp,
            RecentLogins = recentLogins,
            FailedLoginAttempts = failedAttempts,
            LastPasswordChange = user.PasswordChangedAt,
            ActiveDevices = activeDevices
        };
    }
}