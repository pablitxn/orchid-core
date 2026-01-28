namespace Domain.Enums;

/// <summary>
/// Represents the model selection preference for AI completions.
/// The system will choose an appropriate model from the catalog based on this preference.
/// </summary>
public enum ModelPreference
{
    /// <summary>
    /// Prioritize output quality over cost. Selects premium tier models.
    /// </summary>
    Quality,

    /// <summary>
    /// Prioritize cost efficiency. Selects budget tier models.
    /// </summary>
    Cost,

    /// <summary>
    /// Balance between quality and cost. Selects standard tier models.
    /// </summary>
    Balanced
}
