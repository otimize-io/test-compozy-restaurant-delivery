using Microsoft.Extensions.Options;
using RestaurantDelivery.Payment.Payments;

namespace RestaurantDelivery.Payment.Ports;

/// <summary>
/// V1 mock <see cref="IPaymentPort"/> (ADR-001: async-shaped, idempotent, declinable). A capture records a
/// charge in <see cref="PaymentStatus.Accepted"/> and returns immediately with a provider correlation id;
/// the terminal outcome is decided here (from <see cref="PaymentOptions"/>) but only published later, when
/// the settlement callback runs. Idempotency is enforced by the store: a capture whose idempotency key was
/// already seen returns the original record's correlation id and never writes a second charge.
/// </summary>
public sealed class MockPaymentAdapter(IPaymentStore store, IOptions<PaymentOptions> options) : IPaymentPort
{
    private readonly PaymentOptions _options = options.Value;

    public async Task<PaymentCaptureAccepted> CaptureAsync(
        Guid orderId, decimal amount, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var existing = await store.FindByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            // Same idempotency key → same result, one charge (TechSpec; requirement 2).
            return new PaymentCaptureAccepted(existing.CorrelationId);
        }

        var record = new PaymentRecord
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Amount = amount,
            IdempotencyKey = idempotencyKey,
            Status = PaymentStatus.Accepted,
            CorrelationId = Guid.NewGuid().ToString(),
            Plan = PlanFor(amount),
        };

        await store.AddAsync(record, cancellationToken);
        return new PaymentCaptureAccepted(record.CorrelationId);
    }

    public async Task RefundAsync(Guid orderId, string correlationId, CancellationToken cancellationToken = default)
    {
        var record = await store.FindByOrderIdAsync(orderId, cancellationToken);
        if (record is null)
        {
            return;
        }

        record.Status = PaymentStatus.Refunded;
        await store.UpdateAsync(record, cancellationToken);
    }

    private PlannedSettlement PlanFor(decimal amount)
    {
        if (_options.NeverSettleAtOrAbove is { } never && amount >= never)
        {
            return PlannedSettlement.Never;
        }

        if (_options.DeclineAtOrAbove is { } decline && amount >= decline)
        {
            return PlannedSettlement.Decline;
        }

        return PlannedSettlement.Settle;
    }
}
