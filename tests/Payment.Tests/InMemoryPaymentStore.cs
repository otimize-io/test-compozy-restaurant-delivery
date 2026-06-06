using RestaurantDelivery.Payment.Payments;

namespace Payment.Tests;

/// <summary>
/// Process-local <see cref="IPaymentStore"/> for unit tests: lets the adapter and settlement tests run
/// without standing up PostgreSQL. Mirrors the real store's contracts — find-by-idempotency-key (the
/// at-most-one-charge guarantee) and most-recent-by-order — and exposes <see cref="Count"/> so tests can
/// assert exactly one charge was recorded.
/// </summary>
public sealed class InMemoryPaymentStore : IPaymentStore
{
    private readonly List<PaymentRecord> _records = new();

    public int Count => _records.Count;

    public Task<PaymentRecord?> FindByIdempotencyKeyAsync(
        string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var match = _records.FirstOrDefault(r => r.IdempotencyKey == idempotencyKey);
        return Task.FromResult(match);
    }

    public Task<PaymentRecord?> FindByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var match = _records.LastOrDefault(r => r.OrderId == orderId);
        return Task.FromResult(match);
    }

    public Task AddAsync(PaymentRecord payment, CancellationToken cancellationToken = default)
    {
        _records.Add(payment);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(PaymentRecord payment, CancellationToken cancellationToken = default)
    {
        var index = _records.FindIndex(r => r.Id == payment.Id);
        if (index >= 0)
        {
            _records[index] = payment;
        }

        return Task.CompletedTask;
    }
}
