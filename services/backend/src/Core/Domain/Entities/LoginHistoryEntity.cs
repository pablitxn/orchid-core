using System;
using Domain.Entities;

namespace Core.Domain.Entities;

public sealed class LoginHistoryEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime Timestamp { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public string? Location { get; set; }
    public bool Success { get; set; }
    public string? FailureReason { get; set; }

    // Navigation property
    public UserEntity? User { get; set; }
}