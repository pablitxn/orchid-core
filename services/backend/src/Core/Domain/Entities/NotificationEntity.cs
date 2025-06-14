namespace Domain.Entities;

public class NotificationEntity
{
    public Guid Id { get; init; }
    
    public Guid UserId { get; set; }
    
    public string Type { get; set; } = string.Empty;
    
    public string Title { get; set; } = string.Empty;
    
    public string Message { get; set; } = string.Empty;
    
    public string? ActionUrl { get; set; }
    
    public string? ActionText { get; set; }
    
    public bool IsRead { get; set; } = false;
    
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    
    public DateTime? ReadAt { get; set; }
    
    public DateTime? ExpiresAt { get; set; }
    
    // Priority: 0 = Low, 1 = Normal, 2 = High, 3 = Critical
    public int Priority { get; set; } = 1;
    
    // JSON metadata for additional data
    public string? Metadata { get; set; }
    
    public void MarkAsRead()
    {
        if (!IsRead)
        {
            IsRead = true;
            ReadAt = DateTime.UtcNow;
        }
    }
    
    public bool IsExpired()
    {
        return ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
    }
}