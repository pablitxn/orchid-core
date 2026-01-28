using Application.Models;
using Domain.Enums;

namespace Orchid.Infrastructure.Ai.AgentFramework.Services;

/// <summary>
/// Static catalog of available AI models.
/// In the future, this could be moved to configuration or database.
/// </summary>
public static class ModelCatalog
{
    private static readonly Dictionary<string, ModelCatalogEntry> Models = new(StringComparer.OrdinalIgnoreCase)
    {
        // Quality tier models
        ["gpt-5-2025-06-15"] = new ModelCatalogEntry
        {
            ModelId = "gpt-5-2025-06-15",
            DisplayName = "GPT-5",
            Provider = "openai",
            PricingTier = ModelPreference.Quality,
            ContextWindow = 128000,
            SupportsFunctionCalling = true,
            SupportsVision = true,
            CostMultiplier = 3.0m,
            IsActive = true
        },

        // Balanced tier models
        ["gpt-5-mini-2025-06-15"] = new ModelCatalogEntry
        {
            ModelId = "gpt-5-mini-2025-06-15",
            DisplayName = "GPT-5 Mini",
            Provider = "openai",
            PricingTier = ModelPreference.Balanced,
            ContextWindow = 64000,
            SupportsFunctionCalling = true,
            SupportsVision = true,
            CostMultiplier = 1.0m,
            IsActive = true
        },

        // Cost tier models
        ["gpt-5-nano-2025-08-07"] = new ModelCatalogEntry
        {
            ModelId = "gpt-5-nano-2025-08-07",
            DisplayName = "GPT-5 Nano",
            Provider = "openai",
            PricingTier = ModelPreference.Cost,
            ContextWindow = 32000,
            SupportsFunctionCalling = true,
            SupportsVision = false,
            CostMultiplier = 0.3m,
            IsActive = true
        }
    };

    /// <summary>
    /// Gets all active models in the catalog.
    /// </summary>
    public static IReadOnlyList<ModelCatalogEntry> GetAllModels()
        => Models.Values.Where(m => m.IsActive).ToList();

    /// <summary>
    /// Gets a model by ID, or null if not found.
    /// </summary>
    public static ModelCatalogEntry? GetModel(string modelId)
        => Models.TryGetValue(modelId, out var entry) ? entry : null;

    /// <summary>
    /// Checks if a model exists and is active.
    /// </summary>
    public static bool IsValidModel(string modelId)
        => Models.TryGetValue(modelId, out var entry) && entry.IsActive;

    /// <summary>
    /// Gets the default model for a given preference tier.
    /// </summary>
    public static ModelCatalogEntry? GetDefaultForPreference(ModelPreference preference)
        => Models.Values
            .Where(m => m.IsActive && m.PricingTier == preference)
            .OrderBy(m => m.CostMultiplier)
            .FirstOrDefault();

    /// <summary>
    /// Gets models filtered by provider.
    /// </summary>
    public static IReadOnlyList<ModelCatalogEntry> GetModelsByProvider(string provider)
        => Models.Values
            .Where(m => m.IsActive && m.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase))
            .ToList();
}
