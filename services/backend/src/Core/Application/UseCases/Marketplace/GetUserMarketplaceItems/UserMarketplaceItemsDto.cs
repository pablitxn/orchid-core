namespace Application.UseCases.Marketplace.GetUserMarketplaceItems;

public class UserMarketplaceItemsDto
{
    public List<UserPluginDto> Plugins { get; set; } = new();
    public List<UserWorkflowDto> Workflows { get; set; } = new();
    public int TotalItemsCount => Plugins.Count + Workflows.Count;
    public int TotalCreditsSpent { get; set; }
}

public class UserPluginDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int PriceCredits { get; set; }
    public bool IsActive { get; set; }
    public DateTime PurchasedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public int UsageCount { get; set; }
}

public class UserWorkflowDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int PriceCredits { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateTime PurchasedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public int RunCount { get; set; }
}