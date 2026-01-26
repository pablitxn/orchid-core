using Domain.Entities;

namespace Application.Interfaces;

public interface ICreditPurchaseRepository
{
    Task<CreditPurchaseEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CreditPurchaseEntity?> GetByStripeCheckoutSessionIdAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<CreditPurchaseEntity?> GetByStripePaymentIntentIdAsync(string paymentIntentId, CancellationToken cancellationToken = default);

    Task<IEnumerable<CreditPurchaseEntity>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<CreditPurchaseEntity> CreateAsync(CreditPurchaseEntity purchase, CancellationToken cancellationToken = default);

    Task UpdateAsync(CreditPurchaseEntity purchase, CancellationToken cancellationToken = default);
}
