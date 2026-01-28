using Application.Interfaces;
using Application.Models;
using Domain.Enums;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Orchid.Infrastructure.Ai.AgentFramework.Services;

/// <summary>
/// Hierarchical model resolution service.
/// Resolution order: Session override -> Request override -> Agent config -> Agent preference -> User preference -> System default -> Emergency fallback.
/// </summary>
public sealed class ModelResolverService : IModelResolver
{
    private readonly IAgentRepository _agentRepository;
    private readonly IUserRepository _userRepository;
    private readonly IChatSessionRepository _chatSessionRepository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ModelResolverService> _logger;
    private readonly ModelResolutionSettings _settings;

    private const string CacheKeyPrefix = "model_pref:";

    public ModelResolverService(
        IAgentRepository agentRepository,
        IUserRepository userRepository,
        IChatSessionRepository chatSessionRepository,
        IMemoryCache cache,
        IOptions<ModelResolutionSettings> settings,
        ILogger<ModelResolverService> logger)
    {
        _agentRepository = agentRepository;
        _userRepository = userRepository;
        _chatSessionRepository = chatSessionRepository;
        _cache = cache;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<ModelResolutionResult> ResolveAsync(
        ModelResolutionContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 0. Session override (highest priority)
            if (!string.IsNullOrWhiteSpace(context.SessionId))
            {
                var sessionOverride = await _chatSessionRepository.GetModelOverrideAsync(
                    context.SessionId, cancellationToken);

                if (!string.IsNullOrWhiteSpace(sessionOverride) && IsValidModel(sessionOverride))
                {
                    LogResolution(context, sessionOverride, ModelResolutionSource.SessionOverride);
                    return new ModelResolutionResult
                    {
                        ModelId = sessionOverride,
                        Source = ModelResolutionSource.SessionOverride
                    };
                }
            }

            // 1. Request override
            if (!string.IsNullOrWhiteSpace(context.RequestOverride))
            {
                if (IsValidModel(context.RequestOverride))
                {
                    LogResolution(context, context.RequestOverride, ModelResolutionSource.RequestOverride);
                    return new ModelResolutionResult
                    {
                        ModelId = context.RequestOverride,
                        Source = ModelResolutionSource.RequestOverride
                    };
                }
                _logger.LogWarning("Invalid model override requested: {ModelId}", context.RequestOverride);
            }

            // 2. Agent configuration
            if (context.AgentId.HasValue)
            {
                var agent = await _agentRepository.GetByIdAsync(context.AgentId.Value, cancellationToken);
                if (agent != null)
                {
                    // 2a. Direct model configuration
                    if (!string.IsNullOrWhiteSpace(agent.Model) && IsValidModel(agent.Model))
                    {
                        LogResolution(context, agent.Model, ModelResolutionSource.AgentConfiguration);
                        return new ModelResolutionResult
                        {
                            ModelId = agent.Model,
                            Source = ModelResolutionSource.AgentConfiguration
                        };
                    }

                    // 2b. Agent's model preference
                    if (agent.ModelPreference.HasValue)
                    {
                        var preferredModel = ModelCatalog.GetDefaultForPreference(agent.ModelPreference.Value);
                        if (preferredModel != null)
                        {
                            LogResolution(context, preferredModel.ModelId, ModelResolutionSource.AgentPreference);
                            return new ModelResolutionResult
                            {
                                ModelId = preferredModel.ModelId,
                                Source = ModelResolutionSource.AgentPreference,
                                AppliedPreference = agent.ModelPreference.Value
                            };
                        }
                    }

                    // 2c. Fallback model on agent
                    if (!string.IsNullOrWhiteSpace(agent.FallbackModel) && IsValidModel(agent.FallbackModel))
                    {
                        LogResolution(context, agent.FallbackModel, ModelResolutionSource.AgentConfiguration);
                        return new ModelResolutionResult
                        {
                            ModelId = agent.FallbackModel,
                            Source = ModelResolutionSource.AgentConfiguration
                        };
                    }
                }
            }

            // 3. User preference (with caching)
            if (context.UserId.HasValue)
            {
                var userPref = await GetCachedUserPreferenceAsync(context.UserId.Value, cancellationToken);
                if (userPref.HasValue)
                {
                    var preferredModel = ModelCatalog.GetDefaultForPreference(userPref.Value);
                    if (preferredModel != null)
                    {
                        LogResolution(context, preferredModel.ModelId, ModelResolutionSource.UserPreference);
                        return new ModelResolutionResult
                        {
                            ModelId = preferredModel.ModelId,
                            Source = ModelResolutionSource.UserPreference,
                            AppliedPreference = userPref.Value
                        };
                    }
                }
            }

            // 4. System default
            if (IsValidModel(_settings.DefaultModel))
            {
                LogResolution(context, _settings.DefaultModel, ModelResolutionSource.SystemDefault);
                return new ModelResolutionResult
                {
                    ModelId = _settings.DefaultModel,
                    Source = ModelResolutionSource.SystemDefault
                };
            }

            // 5. Emergency fallback
            LogResolution(context, _settings.EmergencyFallback, ModelResolutionSource.EmergencyFallback);
            return new ModelResolutionResult
            {
                ModelId = _settings.EmergencyFallback,
                Source = ModelResolutionSource.EmergencyFallback
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during model resolution, using emergency fallback");
            return new ModelResolutionResult
            {
                ModelId = _settings.EmergencyFallback,
                Source = ModelResolutionSource.EmergencyFallback
            };
        }
    }

    public async Task<ModelResolutionResult> ResolveAffordableModelAsync(
        ModelResolutionContext context,
        int availableCredits,
        int estimatedTokens,
        CancellationToken cancellationToken = default)
    {
        // First, resolve the preferred model normally
        var normalResult = await ResolveAsync(context, cancellationToken);

        // Calculate cost for the resolved model
        var estimatedCost = CalculateCreditCost(normalResult.ModelId, estimatedTokens);

        // If we can afford it, return as-is with cost info
        if (!estimatedCost.HasValue || estimatedCost.Value <= availableCredits)
        {
            return normalResult with { EstimatedCreditCost = estimatedCost };
        }

        // Can't afford preferred model
        _logger.LogInformation(
            "User cannot afford model {ModelId} (cost: {Cost}, available: {Available})",
            normalResult.ModelId, estimatedCost.Value, availableCredits);

        // If degradation not allowed, return original (caller will handle the credit failure)
        if (!context.AllowAutoDegradation)
        {
            _logger.LogInformation("Auto-degradation disabled, returning original model");
            return normalResult with { EstimatedCreditCost = estimatedCost };
        }

        // Find an affordable model
        var affordableModel = FindAffordableModel(availableCredits, estimatedTokens, context.ExcludedModels);

        if (affordableModel == null)
        {
            _logger.LogWarning("No affordable model found for {Credits} credits", availableCredits);
            return normalResult with { EstimatedCreditCost = estimatedCost };
        }

        var degradedCost = CalculateCreditCost(affordableModel.ModelId, estimatedTokens);

        _logger.LogInformation(
            "Degrading from {Original} to {Fallback} due to credit constraints (cost: {OriginalCost} -> {NewCost})",
            normalResult.ModelId, affordableModel.ModelId, estimatedCost, degradedCost);

        return new ModelResolutionResult
        {
            ModelId = affordableModel.ModelId,
            Source = ModelResolutionSource.CreditConstraint,
            IsDegraded = true,
            OriginalModelId = normalResult.ModelId,
            DegradationReason = ModelDegradationReason.InsufficientCredits,
            EstimatedCreditCost = degradedCost
        };
    }

    public string GetSystemDefault() => _settings.DefaultModel;

    public bool IsValidModel(string modelId) => ModelCatalog.IsValidModel(modelId);

    public IReadOnlyList<ModelCatalogEntry> GetAvailableModels() => ModelCatalog.GetAllModels();

    public ModelCatalogEntry? GetCheapestModel()
    {
        return ModelCatalog.GetAllModels()
            .Where(m => m.IsActive)
            .OrderBy(m => m.CostMultiplier)
            .FirstOrDefault();
    }

    public int? CalculateCreditCost(string modelId, int estimatedTokens)
    {
        var model = ModelCatalog.GetModel(modelId);
        if (model == null) return null;

        // Base cost: 1 credit per 100 tokens, minimum 1
        const int tokensPerCredit = 100;
        const int minimumCredits = 1;

        var baseCost = Math.Max(minimumCredits, (int)Math.Ceiling((double)estimatedTokens / tokensPerCredit));
        var adjustedCost = (int)Math.Ceiling(baseCost * model.CostMultiplier);

        return Math.Max(minimumCredits, adjustedCost);
    }

    private ModelCatalogEntry? FindAffordableModel(
        int availableCredits,
        int estimatedTokens,
        IReadOnlySet<string>? excludedModels)
    {
        // Get models sorted by cost multiplier (cheapest first)
        var candidates = ModelCatalog.GetAllModels()
            .Where(m => m.IsActive)
            .Where(m => excludedModels == null || !excludedModels.Contains(m.ModelId))
            .OrderBy(m => m.CostMultiplier)
            .ToList();

        foreach (var model in candidates)
        {
            var cost = CalculateCreditCost(model.ModelId, estimatedTokens);
            if (cost.HasValue && cost.Value <= availableCredits)
            {
                return model;
            }
        }

        return null;
    }

    private async Task<ModelPreference?> GetCachedUserPreferenceAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{CacheKeyPrefix}user:{userId}";

        if (_cache.TryGetValue<ModelPreference?>(cacheKey, out var cachedPref))
        {
            return cachedPref;
        }

        try
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            var preference = user?.ModelPreference;

            _cache.Set(cacheKey, preference, TimeSpan.FromMinutes(_settings.UserPreferenceCacheMinutes));
            return preference;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user model preference for {UserId}", userId);
            return null;
        }
    }

    private void LogResolution(ModelResolutionContext context, string modelId, ModelResolutionSource source)
    {
        if (_settings.EnableDetailedLogging)
        {
            _logger.LogInformation(
                "Model resolved: {ModelId} from {Source}. Context: SessionId={SessionId}, AgentId={AgentId}, UserId={UserId}, Override={Override}",
                modelId, source, context.SessionId, context.AgentId, context.UserId, context.RequestOverride);
        }
    }
}
