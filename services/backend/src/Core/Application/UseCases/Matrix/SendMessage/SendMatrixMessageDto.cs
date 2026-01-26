namespace Application.UseCases.Matrix.SendMessage;

/// <summary>
/// Response DTO for Matrix message send operation.
/// </summary>
public sealed record SendMatrixMessageDto
{
    public required bool Success { get; init; }
    public string? Response { get; init; }
    public string? SessionId { get; init; }
    public int? CreditsUsed { get; init; }
    public int? CreditsRemaining { get; init; }
    public string? Error { get; init; }
    public string? Message { get; init; }
    public int? RetryAfter { get; init; }

    public static SendMatrixMessageDto Succeeded(
        string response,
        string sessionId,
        int creditsUsed,
        int creditsRemaining) => new()
    {
        Success = true,
        Response = response,
        SessionId = sessionId,
        CreditsUsed = creditsUsed,
        CreditsRemaining = creditsRemaining
    };

    public static SendMatrixMessageDto UserNotLinked() => new()
    {
        Success = false,
        Error = "USER_NOT_LINKED",
        Message = "Usuario no vinculado"
    };

    public static SendMatrixMessageDto InsufficientCredits(int creditsRemaining) => new()
    {
        Success = false,
        Error = "INSUFFICIENT_CREDITS",
        CreditsRemaining = creditsRemaining
    };

    public static SendMatrixMessageDto RateLimited(int retryAfter) => new()
    {
        Success = false,
        Error = "RATE_LIMITED",
        RetryAfter = retryAfter
    };

    public static SendMatrixMessageDto AgentNotFound() => new()
    {
        Success = false,
        Error = "AGENT_NOT_FOUND",
        Message = "Agente no encontrado"
    };

    public static SendMatrixMessageDto AgentAccessDenied(string? reason = null) => new()
    {
        Success = false,
        Error = "AGENT_ACCESS_DENIED",
        Message = reason ?? "Acceso al agente denegado"
    };

    public static SendMatrixMessageDto ProcessingError(string message) => new()
    {
        Success = false,
        Error = "PROCESSING_ERROR",
        Message = message
    };
}
