using System.Net.Http.Json;
using System.Text.Json;
using Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.External;

/// <summary>
/// Adapter for sending notifications via Matrix bridge (matrix-orchid-bot)
/// </summary>
public class MatrixBridgeAdapter : IMatrixBridgePort
{
    private readonly HttpClient _httpClient;
    private readonly IMatrixUserLinkRepository _matrixLinkRepository;
    private readonly ILogger<MatrixBridgeAdapter> _logger;
    private readonly string _apiKey;

    public MatrixBridgeAdapter(
        HttpClient httpClient,
        IMatrixUserLinkRepository matrixLinkRepository,
        IConfiguration configuration,
        ILogger<MatrixBridgeAdapter> logger)
    {
        _httpClient = httpClient;
        _matrixLinkRepository = matrixLinkRepository;
        _logger = logger;
        _apiKey = configuration["MatrixBridge:ApiKey"] ?? string.Empty;
    }

    public async Task<bool> SendNotificationAsync(
        Guid userId,
        string title,
        string message,
        string type,
        string? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var link = await _matrixLinkRepository.GetByUserIdAsync(userId, cancellationToken);
        if (link == null || !link.IsActive)
        {
            _logger.LogDebug("User {UserId} has no active Matrix link, skipping notification", userId);
            return false;
        }

        try
        {
            var payload = new
            {
                matrix_user_id = link.MatrixUserId,
                title,
                message,
                type,
                metadata = metadata != null ? JsonSerializer.Deserialize<object>(metadata) : null,
                timestamp = DateTime.UtcNow
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/notify");
            if (!string.IsNullOrEmpty(_apiKey))
            {
                request.Headers.Add("X-API-Key", _apiKey);
            }
            request.Content = JsonContent.Create(payload);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Matrix bridge returned {StatusCode} for user {UserId}",
                    response.StatusCode, userId);
                return false;
            }

            _logger.LogInformation(
                "Notification sent to Matrix for user {UserId}, type: {Type}",
                userId, type);
            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error sending Matrix notification for user {UserId}", userId);
            return false;
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken != cancellationToken)
        {
            _logger.LogWarning("Matrix bridge request timed out for user {UserId}", userId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Matrix notification for user {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> IsUserLinkedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _matrixLinkRepository.UserHasActiveLinkAsync(userId, cancellationToken);
    }
}
