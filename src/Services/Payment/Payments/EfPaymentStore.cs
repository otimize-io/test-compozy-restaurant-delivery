using Microsoft.EntityFrameworkCore;

namespace RestaurantDelivery.Payment.Payments;

/// <summary>
/// EF Core / PostgreSQL implementation of <see cref="IPaymentStore"/> (ADR-006). Registered scoped because
/// it wraps a <see cref="PaymentDbContext"/>.
/// </summary>
public sealed class EfPaymentStore(PaymentDbContext db) : IPaymentStore
{
    public Task<PaymentRecord?> FindByIdempotencyKeyAsync(
        string idempotencyKey, CancellationToken cancellationToken = default) =>
        db.Payments.FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey, cancellationToken);

    public Task<PaymentRecord?> FindByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        db.Payments
            .Where(p => p.OrderId == orderId)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(PaymentRecord payment, CancellationToken cancellationToken = default)
    {
        db.Payments.Add(payment);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PaymentRecord payment, CancellationToken cancellationToken = default)
    {
        db.Payments.Update(payment);
        await db.SaveChangesAsync(cancellationToken);
    }
}
