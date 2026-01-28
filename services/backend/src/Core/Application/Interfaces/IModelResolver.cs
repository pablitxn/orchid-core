using Application.Models;

namespace Application.Interfaces;

/// <summary>
/// Service for resolving which AI model to use based on a hierarchical configuration.
/// Resolution order: Request override -> Agent config -> Agent preference -> User preference -> System default -> Emergency fallback.
/// </summary>
public interface IModelResolver
{
    /// <summary>
    /// Resolves the appropriate model for the given context.
    /// </summary>
    /// <param name="context">The resolution context containing agent, user, and override information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolution result containing the model ID and the source that provided it.</returns>
    Task<ModelResolutionResult> ResolveAsync(ModelResolutionContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves an affordable model based on available credits.
    /// If the preferred model is too expensive and AllowAutoDegradation is true,
    /// will return a cheaper model that the user can afford.
    /// </summary>
    /// <param name="context">The resolution context with credit information.</param>
    /// <param name="availableCredits">Credits available to the user.</param>
    /// <param name="estimatedTokens">Estimated token count for cost calculation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolution result, potentially degraded to an affordable model.</returns>
    Task<ModelResolutionResult> ResolveAffordableModelAsync(
        ModelResolutionContext context,
        int availableCredits,
        int estimatedTokens,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the system default model ID from configuration.
    /// </summary>
    string GetSystemDefault();

    /// <summary>
    /// Validates if a model ID exists in the catalog.
    /// </summary>
    bool IsValidModel(string modelId);

    /// <summary>
    /// Gets all available models from the catalog.
    /// </summary>
    IReadOnlyList<ModelCatalogEntry> GetAvailableModels();

    /// <summary>
    /// Gets the cheapest available model from the catalog.
    /// </summary>
    ModelCatalogEntry? GetCheapestModel();

    /// <summary>
    /// Calculates the estimated credit cost for a model and token count.
    /// </summary>
    /// <param name="modelId">The model ID.</param>
    /// <param name="estimatedTokens">Estimated token count.</param>
    /// <returns>Estimated credit cost, or null if model not found.</returns>
    int? CalculateCreditCost(string modelId, int estimatedTokens);
}
