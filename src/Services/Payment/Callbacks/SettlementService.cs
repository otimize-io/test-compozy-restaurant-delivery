using MassTransit;
using RestaurantDelivery.Contracts.Events;
using RestaurantDelivery.Payment.Payments;
using RestaurantDelivery.Platform;

namespace RestaurantDelivery.Payment.Callbacks;

/// <summary>The outcome of processing a settlement callback, surfaced to the HTTP layer and tests.</summary>
public enum SettlementResult
{
    /// <summary>No payment record exists for the order → the endpoint returns 404.</summary>
    NotFound,

    /// <summary>The payment was already terminal (settled/declined/refunded) → idempotent no-op.</summary>
    AlreadyResolved,

    /// <summary><c>PaymentSettled</c> was published and the record marked settled.</summary>
    Settled,

    /// <summary><c>PaymentDeclined</c> was published and the record marked declined.</summary>
    Declined,
}

/// <summary>
/// Resolves an accepted payment when the settlement callback fires: it publishes <see cref="PaymentSettled"/>
/// or <see cref="PaymentDeclined"/> (using the order's own correlation id) and moves the record to a terminal
/// state (TechSpec "Integration Points → Payment"; ADR-004). Idempotent: a payment that is already terminal
/// is left untouched, so a redelivered callback publishes at most once.
/// </summary>
public sealed class SettlementService(IPaymentStore store, IPublishEndpoint publishEndpoint)
{
    /// <summary>
    /// Settles like <see cref="SettleAsync"/>, but tolerates the capture race: the SPA posts the settlement
    /// immediately after placing the order, so it can arrive before <c>CapturePayment</c> has been consumed
    /// and the record written. When the first attempt finds no record, it re-checks every
    /// <paramref name="interval"/> up to <paramref name="timeout"/> before giving up with
    /// <see cref="SettlementResult.NotFound"/>. Any non-NotFound outcome returns immediately.
    /// </summary>
    public async Task<SettlementResult> SettleWaitingForCaptureAsync(
        SettlementCallbackRequest request,
        TimeSpan timeout,
        TimeSpan interval,
        CancellationToken cancellationToken = default)
    {
        var result = await SettleAsync(request, cancellationToken);
        if (result != SettlementResult.NotFound || timeout <= TimeSpan.Zero || interval <= TimeSpan.Zero)
        {
            return result;
        }

        var waited = TimeSpan.Zero;
        while (result == SettlementResult.NotFound && waited < timeout)
        {
            await Task.Delay(interval, cancellationToken);
            waited += interval;
            result = await SettleAsync(request, cancellationToken);
        }

        return result;
    }

    public async Task<SettlementResult> SettleAsync(
        SettlementCallbackRequest request, CancellationToken cancellationToken = default)
    {
        var record = await store.FindByOrderIdAsync(request.OrderId, cancellationToken);
        if (record is null)
        {
            return SettlementResult.NotFound;
        }

        if (record.Status != PaymentStatus.Accepted)
        {
            return record.Status == PaymentStatus.Declined
                ? SettlementResult.Declined
                : SettlementResult.AlreadyResolved;
        }

        var declined = ResolveDecline(record, request.Outcome);
        if (declined)
        {
            record.Status = PaymentStatus.Declined;
            await store.UpdateAsync(record, cancellationToken);
            await publishEndpoint.Publish(
                new PaymentDeclined(record.OrderId, record.CorrelationId, DeclineReasonFor(record)),
                cancellationToken);
            return SettlementResult.Declined;
        }

        record.Status = PaymentStatus.Settled;
        await store.UpdateAsync(record, cancellationToken);
        await publishEndpoint.Publish(
            new PaymentSettled(record.OrderId, record.CorrelationId),
            cancellationToken);
        return SettlementResult.Settled;
    }

    private static bool ResolveDecline(PaymentRecord record, string? outcome) => outcome?.Trim().ToLowerInvariant() switch
    {
        "decline" or "declined" or "fail" or "failed" => true,
        "settle" or "settled" or "success" or "succeeded" => false,
        // No explicit override → honour the outcome the adapter planned at capture time.
        _ => record.Plan == PlannedSettlement.Decline,
    };

    private static string DeclineReasonFor(PaymentRecord record) =>
        record.Plan == PlannedSettlement.Decline
            ? "Capture flagged for decline at authorization."
            : "Declined by settlement callback.";
}
