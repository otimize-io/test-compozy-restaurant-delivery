namespace RestaurantDelivery.Payment.Payments;

/// <summary>
/// Persists payment records (ADR-006: Payment owns its PostgreSQL database). Kept behind a port so the
/// adapters and consumers do not depend on EF Core directly, and so unit tests can substitute an
/// in-memory store. The store is the source of truth for the idempotency guarantee: a capture with an
/// already-seen key returns the existing record rather than creating a second charge.
/// </summary>
public interface IPaymentStore
{
    /// <summary>Returns the payment for an idempotency key, or <c>null</c> when none has been recorded.</summary>
    Task<PaymentRecord?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>Returns the most recent payment for an order, or <c>null</c> when none exists.</summary>
    Task<PaymentRecord?> FindByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Adds a new payment record.</summary>
    Task AddAsync(PaymentRecord payment, CancellationToken cancellationToken = default);

    /// <summary>Persists changes made to a tracked or detached payment record.</summary>
    Task UpdateAsync(PaymentRecord payment, CancellationToken cancellationToken = default);
}
