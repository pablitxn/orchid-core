namespace Domain.Entities;

public class ServiceAccountEntity
{
    public Guid Id { get; init; }

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecretHash { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string[] Scopes { get; set; } = Array.Empty<string>();

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastUsedAt { get; set; }
}
