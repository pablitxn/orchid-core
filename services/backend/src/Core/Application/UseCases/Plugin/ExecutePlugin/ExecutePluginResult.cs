namespace Application.UseCases.Plugin.ExecutePlugin;

public sealed record ExecutePluginResult(
    bool Success,
    string? Result = null,
    string? Error = null,
    int? CreditsUsed = null
);