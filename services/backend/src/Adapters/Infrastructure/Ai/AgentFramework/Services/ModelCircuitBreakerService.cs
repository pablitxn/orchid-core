using System.Collections.Concurrent;
using Application.Interfaces;
using Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Orchid.Infrastructure.Ai.AgentFramework.Services;

/// <summary>
/// In-memory circuit breaker for AI model availability.
/// Tracks failures with a sliding window and prevents cascading errors.
/// </summary>
public sealed class ModelCircuitBreakerService : IModelCircuitBreaker
{
    private readonly ConcurrentDictionary<string, CircuitState> _circuits = new(StringComparer.OrdinalIgnoreCase);
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<ModelCircuitBreakerService> _logger;
    private readonly ModelCircuitBreakerSettings _settings;

    public ModelCircuitBreakerService(
        IEventPublisher eventPublisher,
        IOptions<ModelCircuitBreakerSettings> settings,
        ILogger<ModelCircuitBreakerService> logger)
    {
        _eventPublisher = eventPublisher;
        _settings = settings.Value;
        _logger = logger;
    }

    public Task<bool> IsCircuitOpenAsync(string modelId, CancellationToken ct = default)
    {
        if (!_settings.Enabled)
            return Task.FromResult(false);

        var state = GetOrCreateState(modelId);
        var isOpen = state.IsOpen && DateTime.UtcNow < state.CooldownEndsAt;

        // Check if we should transition to half-open
        if (state.IsOpen && DateTime.UtcNow >= state.CooldownEndsAt)
        {
            state.IsHalfOpen = true;
            _logger.LogInformation(
                "Circuit breaker for model {ModelId} transitioning to half-open state",
                modelId);
        }

        return Task.FromResult(isOpen && !state.IsHalfOpen);
    }

    public async Task RecordSuccessAsync(string modelId, CancellationToken ct = default)
    {
        if (!_settings.Enabled)
            return;

        var state = GetOrCreateState(modelId);

        if (state.IsHalfOpen)
        {
            state.HalfOpenSuccesses++;

            if (state.HalfOpenSuccesses >= _settings.SuccessThresholdInHalfOpen)
            {
                // Close the circuit
                state.IsOpen = false;
                state.IsHalfOpen = false;
                state.FailureTimestamps.Clear();
                state.HalfOpenSuccesses = 0;
                state.CooldownEndsAt = null;

                _logger.LogInformation(
                    "Circuit breaker for model {ModelId} closed after successful recovery",
                    modelId);

                await _eventPublisher.PublishAsync(
                    new ModelCircuitClosedEvent(modelId, DateTime.UtcNow),
                    ct);
            }
        }
        else
        {
            // Clear old failures in normal state
            CleanupOldFailures(state);
        }
    }

    public async Task RecordFailureAsync(string modelId, ModelFailureType failureType, CancellationToken ct = default)
    {
        if (!_settings.Enabled)
            return;

        var state = GetOrCreateState(modelId);
        var now = DateTime.UtcNow;

        // Reset half-open state on failure
        if (state.IsHalfOpen)
        {
            state.IsHalfOpen = false;
            state.HalfOpenSuccesses = 0;
            state.CooldownEndsAt = now.AddSeconds(_settings.CooldownPeriodSeconds);

            _logger.LogWarning(
                "Circuit breaker for model {ModelId} reopened from half-open state after {FailureType}",
                modelId, failureType);

            return;
        }

        // Add failure timestamp
        state.FailureTimestamps.Add(now);
        state.LastFailureType = failureType;

        // Cleanup old failures outside sliding window
        CleanupOldFailures(state);

        var failureCount = state.FailureTimestamps.Count;

        _logger.LogWarning(
            "Recorded {FailureType} failure for model {ModelId}. Count: {FailureCount}/{Threshold}",
            failureType, modelId, failureCount, _settings.FailureThreshold);

        // Check if we should open the circuit
        if (!state.IsOpen && failureCount >= _settings.FailureThreshold)
        {
            state.IsOpen = true;
            state.CooldownEndsAt = now.AddSeconds(_settings.CooldownPeriodSeconds);

            _logger.LogWarning(
                "Circuit breaker OPENED for model {ModelId} after {FailureCount} failures. Cooldown until {CooldownEndsAt}",
                modelId, failureCount, state.CooldownEndsAt);

            await _eventPublisher.PublishAsync(
                new ModelCircuitOpenedEvent(
                    modelId,
                    failureCount,
                    TimeSpan.FromSeconds(_settings.CooldownPeriodSeconds)),
                ct);
        }
    }

    public Task<ModelCircuitState> GetStateAsync(string modelId, CancellationToken ct = default)
    {
        var state = GetOrCreateState(modelId);
        CleanupOldFailures(state);

        return Task.FromResult(new ModelCircuitState(
            modelId,
            state.IsOpen && !state.IsHalfOpen,
            state.FailureTimestamps.Count,
            state.FailureTimestamps.LastOrDefault(),
            state.CooldownEndsAt));
    }

    public Task ResetAsync(string modelId, CancellationToken ct = default)
    {
        if (_circuits.TryGetValue(modelId, out var state))
        {
            state.IsOpen = false;
            state.IsHalfOpen = false;
            state.FailureTimestamps.Clear();
            state.HalfOpenSuccesses = 0;
            state.CooldownEndsAt = null;

            _logger.LogInformation("Circuit breaker for model {ModelId} manually reset", modelId);
        }

        return Task.CompletedTask;
    }

    private CircuitState GetOrCreateState(string modelId)
    {
        return _circuits.GetOrAdd(modelId, _ => new CircuitState());
    }

    private void CleanupOldFailures(CircuitState state)
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-_settings.SlidingWindowSeconds);
        state.FailureTimestamps.RemoveAll(t => t < cutoff);
    }

    private sealed class CircuitState
    {
        public bool IsOpen { get; set; }
        public bool IsHalfOpen { get; set; }
        public List<DateTime> FailureTimestamps { get; } = new();
        public DateTime? CooldownEndsAt { get; set; }
        public int HalfOpenSuccesses { get; set; }
        public ModelFailureType? LastFailureType { get; set; }
    }
}
