using MediatR;

namespace Application.UseCases.Marketplace.GetUserMarketplaceItems;

public record GetUserMarketplaceItemsQuery(Guid UserId) : IRequest<UserMarketplaceItemsDto>;