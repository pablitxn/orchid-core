using System.Diagnostics;
using System.Runtime.Loader;
using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// Executes plugins in a sandboxed environment with resource limits
/// </summary>
public class SandboxedPluginExecutor : IPluginSandbox
{
    private readonly IPluginLifecycleManager _lifecycleManager;
    private readonly ILogger<SandboxedPluginExecutor> _logger;
    private readonly Dictionary<string, SemaphoreSlim> _executionSemaphores = new();
    private readonly object _semaphoreLock = new();

    public SandboxedPluginExecutor(
        IPluginLifecycleManager lifecycleManager,
        ILogger<SandboxedPluginExecutor> logger)
    {
        _lifecycleManager = lifecycleManager;
        _logger = logger;
    }

    public async Task<PluginExecutionResult> ExecuteAsync(
        string pluginId,
        string methodName,
        object? parameters,
        PluginExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var metrics = new PluginExecutionMetrics();
        
        try
        {
            // Get or create semaphore for this plugin (limit concurrent executions)
            var semaphore = GetOrCreateSemaphore(pluginId);
            
            // Wait for availability (max 5 concurrent executions per plugin)
            if (!await semaphore.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken))
            {
                return new PluginExecutionResult
                {
                    Success = false,
                    Error = "Plugin execution queue is full. Please try again later."
                };
            }

            try
            {
                // Create timeout cancellation
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(context.Timeout);

                // Load the plugin if not already loaded
                var plugin = await _lifecycleManager.LoadPluginAsync(
                    pluginId,
                    new PluginLoadContext
                    {
                        UserId = context.UserId,
                        SessionId = context.SessionId,
                        IsolationLevel = PluginIsolationLevel.AssemblyLoadContext
                    },
                    timeoutCts.Token);

                // Validate plugin state
                if (plugin.State != PluginState.Loaded && plugin.State != PluginState.Running)
                {
                    return new PluginExecutionResult
                    {
                        Success = false,
                        Error = $"Plugin is in invalid state: {plugin.State}"
                    };
                }

                // Execute with monitoring
                var executionTask = Task.Run(async () =>
                {
                    // Monitor memory usage
                    var initialMemory = GC.GetTotalMemory(false);
                    
                    try
                    {
                        // Execute the plugin method
                        var result = await plugin.InvokeAsync<object>(
                            methodName,
                            parameters is object[] arr ? arr : new[] { parameters },
                            timeoutCts.Token);
                        
                        return result;
                    }
                    finally
                    {
                        // Collect metrics
                        var finalMemory = GC.GetTotalMemory(false);
                        metrics.MemoryUsedBytes = Math.Max(0, finalMemory - initialMemory);
                    }
                }, timeoutCts.Token);

                // Wait for completion or timeout
                var result = await executionTask;
                
                metrics.WallClockTime = stopwatch.Elapsed;
                
                return new PluginExecutionResult
                {
                    Success = true,
                    Result = result,
                    Metrics = metrics
                };
            }
            finally
            {
                semaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Plugin execution timed out. PluginId: {PluginId}, Method: {Method}", 
                pluginId, methodName);
                
            return new PluginExecutionResult
            {
                Success = false,
                Error = $"Plugin execution timed out after {context.Timeout.TotalSeconds} seconds",
                Metrics = metrics
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing plugin. PluginId: {PluginId}, Method: {Method}", 
                pluginId, methodName);
                
            return new PluginExecutionResult
            {
                Success = false,
                Error = $"Plugin execution failed: {ex.Message}",
                Metrics = metrics
            };
        }
    }

    public async Task<PluginValidationResult> ValidateAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        var result = new PluginValidationResult();
        
        try
        {
            // Check if plugin exists and is active
            // This would integrate with your plugin repository
            
            // For now, return a basic validation
            result.IsValid = true;
            result.SecurityInfo = new PluginSecurityInfo
            {
                IsSigned = false,
                ThreatLevel = 3, // Medium threat by default
                RequiredCapabilities = new List<string> { "file:read", "network:http" }
            };
            
            // Add warnings for unsigned plugins
            if (!result.SecurityInfo.IsSigned)
            {
                result.Warnings.Add("Plugin is not digitally signed");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating plugin {PluginId}", pluginId);
            result.IsValid = false;
            result.Errors.Add($"Validation failed: {ex.Message}");
            return result;
        }
    }

    private SemaphoreSlim GetOrCreateSemaphore(string pluginId)
    {
        lock (_semaphoreLock)
        {
            if (!_executionSemaphores.TryGetValue(pluginId, out var semaphore))
            {
                semaphore = new SemaphoreSlim(5, 5); // Max 5 concurrent executions
                _executionSemaphores[pluginId] = semaphore;
            }
            return semaphore;
        }
    }
}

/// <summary>
/// Plugin instance implementation with isolation
/// </summary>
public class IsolatedPluginInstance : IPluginInstance
{
    private readonly AssemblyLoadContext? _loadContext;
    private readonly object? _pluginInstance;
    private readonly Type? _pluginType;
    private readonly ILogger<IsolatedPluginInstance> _logger;
    private bool _disposed;

    public IsolatedPluginInstance(
        string pluginId,
        string name,
        string version,
        PluginIsolationLevel isolationLevel,
        ILogger<IsolatedPluginInstance> logger)
    {
        PluginId = pluginId;
        Name = name;
        Version = version;
        IsolationLevel = isolationLevel;
        LoadedAt = DateTime.UtcNow;
        State = PluginState.Loading;
        _logger = logger;
        
        if (isolationLevel == PluginIsolationLevel.AssemblyLoadContext)
        {
            _loadContext = new AssemblyLoadContext($"Plugin_{pluginId}", isCollectible: true);
        }
    }

    public string PluginId { get; }
    public string Name { get; }
    public string Version { get; }
    public PluginState State { get; private set; }
    public DateTime LoadedAt { get; }
    public PluginIsolationLevel IsolationLevel { get; }

    public async Task<T?> InvokeAsync<T>(string methodName, object?[]? parameters = null, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(IsolatedPluginInstance));
            
        if (State != PluginState.Loaded && State != PluginState.Running)
            throw new InvalidOperationException($"Cannot invoke method on plugin in state: {State}");
            
        State = PluginState.Running;
        
        try
        {
            if (_pluginInstance == null || _pluginType == null)
                throw new InvalidOperationException("Plugin not properly loaded");
                
            var method = _pluginType.GetMethod(methodName);
            if (method == null)
                throw new InvalidOperationException($"Method '{methodName}' not found in plugin");
                
            // Execute the method
            var task = Task.Run(() =>
            {
                var result = method.Invoke(_pluginInstance, parameters);
                
                // Handle async methods
                if (result is Task taskResult)
                {
                    taskResult.Wait(cancellationToken);
                    
                    if (taskResult.GetType().IsGenericType)
                    {
                        var resultProperty = taskResult.GetType().GetProperty("Result");
                        return (T?)resultProperty?.GetValue(taskResult);
                    }
                    
                    return default(T);
                }
                
                return (T?)result;
            }, cancellationToken);
            
            return await task;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking method {Method} on plugin {PluginId}", methodName, PluginId);
            State = PluginState.Failed;
            throw;
        }
        finally
        {
            if (State == PluginState.Running)
                State = PluginState.Loaded;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
            
        _disposed = true;
        State = PluginState.Unloading;
        
        try
        {
            // Dispose plugin instance if it implements IDisposable
            if (_pluginInstance is IDisposable disposable)
            {
                disposable.Dispose();
            }
            else if (_pluginInstance is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            
            // Unload the assembly context
            _loadContext?.Unload();
            
            State = PluginState.Unloaded;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing plugin {PluginId}", PluginId);
            State = PluginState.Failed;
        }
    }
}