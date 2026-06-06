using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestaurantDelivery.Payment.Payments;

namespace RestaurantDelivery.Payment.Ports;

/// <summary>
/// The "stub-real" <see cref="IPaymentPort"/> adapter that proves the seam is swappable (ADR-001 Phase-2
/// gate). It is shaped like a real PSP integration — where a production build would issue an HTTP capture
/// and receive a provider transaction reference, this stub synthesises a <c>psp_</c>-prefixed reference and
/// records the charge — but it satisfies the identical <see cref="IPaymentPort"/> contract. Swapping it in
/// for <see cref="MockPaymentAdapter"/> in DI keeps the capture → settlement-callback flow working with no
/// change to any consumer, the callback endpoint, or any neighbouring service.
/// </summary>
public sealed class StubRealPaymentAdapter(
    IPaymentStore store,
    IOptions<PaymentOptions> options,
    ILogger<StubRealPaymentAdapter> logger) : IPaymentPort
{
    private readonly PaymentOptions _options = options.Value;

    public async Task<PaymentCaptureAccepted> CaptureAsync(
        Guid orderId, decimal amount, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var existing = await store.FindByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return new PaymentCaptureAccepted(existing.CorrelationId);
        }

        // A real adapter would POST to the PSP here with the idempotency key as the request key.
        logger.LogInformation(
            "Stub-real PSP capture for order {OrderId} amount {Amount} (idempotency {Key})",
            orderId, amount, idempotencyKey);

        var record = new PaymentRecord
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Amount = amount,
            IdempotencyKey = idempotencyKey,
            Status = PaymentStatus.Accepted,
            CorrelationId = $"psp_{Guid.NewGuid():N}",
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

        // A real adapter would POST a refund to the PSP here.
        logger.LogInformation("Stub-real PSP refund for order {OrderId}", orderId);
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
