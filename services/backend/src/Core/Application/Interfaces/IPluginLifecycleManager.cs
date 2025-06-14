namespace Application.Interfaces;

/// <summary>
/// Manages the lifecycle of plugins including loading, unloading, and health monitoring
/// </summary>
public interface IPluginLifecycleManager
{
    /// <summary>
    /// Loads a plugin into memory
    /// </summary>
    Task<IPluginInstance> LoadPluginAsync(
        string pluginId, 
        PluginLoadContext context,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Unloads a plugin from memory
    /// </summary>
    Task UnloadPluginAsync(
        string pluginId, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks the health of a loaded plugin
    /// </summary>
    Task<PluginHealth> CheckHealthAsync(
        string pluginId, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all currently loaded plugins
    /// </summary>
    Task<IReadOnlyList<IPluginInstance>> GetLoadedPluginsAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Reloads a plugin (unload and load again)
    /// </summary>
    Task<IPluginInstance> ReloadPluginAsync(
        string pluginId,
        PluginLoadContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context for loading a plugin
/// </summary>
public class PluginLoadContext
{
    public Guid UserId { get; set; }
    public string? SessionId { get; set; }
    public Dictionary<string, string> Configuration { get; set; } = new();
    public PluginIsolationLevel IsolationLevel { get; set; } = PluginIsolationLevel.AppDomain;
}

/// <summary>
/// Plugin isolation levels
/// </summary>
public enum PluginIsolationLevel
{
    /// <summary>
    /// No isolation (not recommended)
    /// </summary>
    None,
    
    /// <summary>
    /// AppDomain isolation (medium security)
    /// </summary>
    AppDomain,
    
    /// <summary>
    /// AssemblyLoadContext isolation (recommended)
    /// </summary>
    AssemblyLoadContext,
    
    /// <summary>
    /// Process isolation (high security)
    /// </summary>
    Process,
    
    /// <summary>
    /// Container isolation (highest security)
    /// </summary>
    Container
}

/// <summary>
/// Represents a loaded plugin instance
/// </summary>
public interface IPluginInstance : IAsyncDisposable
{
    string PluginId { get; }
    string Name { get; }
    string Version { get; }
    PluginState State { get; }
    DateTime LoadedAt { get; }
    PluginIsolationLevel IsolationLevel { get; }
    
    /// <summary>
    /// Invokes a method on the plugin
    /// </summary>
    Task<T?> InvokeAsync<T>(
        string methodName, 
        object?[]? parameters = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Plugin states
/// </summary>
public enum PluginState
{
    Loading,
    Loaded,
    Running,
    Suspended,
    Unloading,
    Unloaded,
    Failed
}

/// <summary>
/// Plugin health status
/// </summary>
public class PluginHealth
{
    public string PluginId { get; set; } = string.Empty;
    public HealthStatus Status { get; set; }
    public DateTime CheckedAt { get; set; }
    public long MemoryUsageBytes { get; set; }
    public double CpuUsagePercent { get; set; }
    public int ActiveRequests { get; set; }
    public TimeSpan Uptime { get; set; }
    public Dictionary<string, object> Diagnostics { get; set; } = new();
}

/// <summary>
/// Health status levels
/// </summary>
public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy,
    Critical
}