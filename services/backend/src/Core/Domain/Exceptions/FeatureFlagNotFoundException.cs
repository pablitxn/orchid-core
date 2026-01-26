namespace Domain.Exceptions;

public class FeatureFlagNotFoundException : EntityNotFoundException
{
    public FeatureFlagNotFoundException(Guid featureFlagId)
        : base("FeatureFlag", featureFlagId)
    {
        FeatureFlagId = featureFlagId;
    }

    public FeatureFlagNotFoundException(string key)
        : base($"FeatureFlag with key '{key}' was not found.")
    {
        Key = key;
    }

    public Guid? FeatureFlagId { get; }
    public string? Key { get; }
}
