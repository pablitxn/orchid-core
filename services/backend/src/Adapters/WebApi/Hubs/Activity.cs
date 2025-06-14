namespace WebApi.Hubs;

/// <summary>
///     Represents a backend activity event sent to clients via SignalR.
/// </summary>
public sealed record Activity(string Type, object? Payload = null);