using Domain.Entities;

namespace Application.UseCases.Credits.Common;

public sealed record CreditPackageDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Credits { get; init; }
    public int PriceInCents { get; init; }
    public string Currency { get; init; } = "usd";
    public int? ExpirationDays { get; init; }
    public int DisplayOrder { get; init; }

    public static CreditPackageDto FromEntity(CreditPackageEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Credits = entity.Credits,
        PriceInCents = entity.PriceInCents,
        Currency = entity.Currency,
        ExpirationDays = entity.ExpirationDays,
        DisplayOrder = entity.DisplayOrder
    };
}
