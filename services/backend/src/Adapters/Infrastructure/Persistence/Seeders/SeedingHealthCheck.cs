using Application.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Infrastructure.Persistence.Seeders;

/// <summary>
/// Health check that reports the status of database seeding.
/// Returns Healthy when seeding is complete, Degraded while in progress.
/// </summary>
public sealed class SeedingHealthCheck : IHealthCheck
{
    private readonly ISeedingStatusService _seedingStatus;

    public SeedingHealthCheck(ISeedingStatusService seedingStatus)
    {
        _seedingStatus = seedingStatus;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_seedingStatus.IsSeedingComplete)
        {
            if (_seedingStatus.SeedingError != null)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    "Database seeding failed",
                    _seedingStatus.SeedingError,
                    new Dictionary<string, object>
                    {
                        ["error"] = _seedingStatus.SeedingError.Message
                    }));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                "Database seeding completed successfully",
                new Dictionary<string, object>
                {
                    ["status"] = "complete"
                }));
        }

        return Task.FromResult(HealthCheckResult.Degraded(
            "Database seeding is in progress",
            data: new Dictionary<string, object>
            {
                ["status"] = "in_progress"
            }));
    }
}
