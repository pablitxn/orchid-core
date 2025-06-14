namespace Application.Interfaces;

/// <summary>
/// Provides sandboxed execution environment for plugins
/// </summary>
public interface IPluginSandbox
{
    /// <summary>
    /// Executes a plugin method within a sandboxed environment
    /// </summary>
    Task<PluginExecutionResult> ExecuteAsync(
        string pluginId,
        string methodName,
        object? parameters,
        PluginExecutionContext context,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates a plugin before execution
    /// </summary>
    Task<PluginValidationResult> ValidateAsync(
        string pluginId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context for plugin execution with resource limits
/// </summary>
public class PluginExecutionContext
{
    /// <summary>
    /// Maximum execution time for the plugin
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Maximum memory the plugin can allocate (in bytes)
    /// </summary>
    public long MaxMemoryBytes { get; set; } = 100 * 1024 * 1024; // 100MB default
    
    /// <summary>
    /// Maximum CPU time allowed (in milliseconds)
    /// </summary>
    public int MaxCpuMilliseconds { get; set; } = 5000; // 5 seconds default
    
    /// <summary>
    /// Allowed capabilities for the plugin
    /// </summary>
    public HashSet<string> AllowedCapabilities { get; set; } = new();
    
    /// <summary>
    /// User context for the plugin execution
    /// </summary>
    public Guid UserId { get; set; }
    
    /// <summary>
    /// Session context
    /// </summary>
    public string? SessionId { get; set; }
}

/// <summary>
/// Result of plugin execution
/// </summary>
public class PluginExecutionResult
{
    public bool Success { get; set; }
    public object? Result { get; set; }
    public string? Error { get; set; }
    public PluginExecutionMetrics Metrics { get; set; } = new();
}

/// <summary>
/// Metrics collected during plugin execution
/// </summary>
public class PluginExecutionMetrics
{
    public long MemoryUsedBytes { get; set; }
    public int CpuTimeMilliseconds { get; set; }
    public TimeSpan WallClockTime { get; set; }
    public Dictionary<string, long> ResourceUsage { get; set; } = new();
}

/// <summary>
/// Result of plugin validation
/// </summary>
public class PluginValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public PluginSecurityInfo SecurityInfo { get; set; } = new();
}

/// <summary>
/// Security information about a plugin
/// </summary>
public class PluginSecurityInfo
{
    public bool IsSigned { get; set; }
    public string? SignatureAlgorithm { get; set; }
    public DateTime? SignatureDate { get; set; }
    public List<string> RequiredCapabilities { get; set; } = new();
    public int ThreatLevel { get; set; } // 0-10 scale
}