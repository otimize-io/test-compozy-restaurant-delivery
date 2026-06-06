using Microsoft.Extensions.Options;
using RestaurantDelivery.Payment.Payments;
using RestaurantDelivery.Payment.Ports;

namespace Payment.Tests;

/// <summary>
/// Unit tests for the mock <see cref="IPaymentPort"/> adapter (task_07 Tests): capture is accepted (not
/// terminal), idempotent on the key (same result, one charge), and a decline-flagged capture records the
/// decline plan that the settlement callback later turns into <c>PaymentDeclined</c>.
/// </summary>
public class MockPaymentAdapterTests
{
    private static MockPaymentAdapter CreateAdapter(IPaymentStore store, PaymentOptions? options = null) =>
        new(store, Options.Create(options ?? new PaymentOptions()));

    [Fact]
    public async Task CaptureAsync_returns_accepted_with_correlation_id_and_does_not_settle_inline()
    {
        var store = new InMemoryPaymentStore();
        var adapter = CreateAdapter(store);

        var accepted = await adapter.CaptureAsync(Guid.NewGuid(), 42.50m, "key-accept", CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(accepted.CorrelationId));
        // The capture is accepted, NOT terminal: the persisted record stays Accepted until the callback.
        Assert.Equal(1, store.Count);
        var record = await store.FindByIdempotencyKeyAsync("key-accept");
        Assert.NotNull(record);
        Assert.Equal(PaymentStatus.Accepted, record!.Status);
        Assert.Equal(PlannedSettlement.Settle, record.Plan);
    }

    [Fact]
    public async Task Repeated_capture_with_same_key_returns_same_result_and_charges_once()
    {
        var store = new InMemoryPaymentStore();
        var adapter = CreateAdapter(store);
        var orderId = Guid.NewGuid();

        var first = await adapter.CaptureAsync(orderId, 10m, "key-dup", CancellationToken.None);
        var second = await adapter.CaptureAsync(orderId, 10m, "key-dup", CancellationToken.None);

        Assert.Equal(first.CorrelationId, second.CorrelationId);
        Assert.Equal(1, store.Count); // exactly one charge recorded
    }

    [Fact]
    public async Task Capture_at_or_above_decline_threshold_is_flagged_to_decline()
    {
        var store = new InMemoryPaymentStore();
        var adapter = CreateAdapter(store, new PaymentOptions { DeclineAtOrAbove = 100m });

        await adapter.CaptureAsync(Guid.NewGuid(), 100m, "key-decline", CancellationToken.None);

        var record = await store.FindByIdempotencyKeyAsync("key-decline");
        Assert.NotNull(record);
        Assert.Equal(PlannedSettlement.Decline, record!.Plan);
        // Still only accepted at capture time — the decline surfaces via the callback, not inline.
        Assert.Equal(PaymentStatus.Accepted, record.Status);
    }

    [Fact]
    public async Task Capture_at_or_above_never_settle_threshold_is_flagged_never()
    {
        var store = new InMemoryPaymentStore();
        var adapter = CreateAdapter(store, new PaymentOptions { NeverSettleAtOrAbove = 500m });

        await adapter.CaptureAsync(Guid.NewGuid(), 999m, "key-never", CancellationToken.None);

        var record = await store.FindByIdempotencyKeyAsync("key-never");
        Assert.Equal(PlannedSettlement.Never, record!.Plan);
    }

    [Fact]
    public async Task RefundAsync_marks_the_order_payment_refunded()
    {
        var store = new InMemoryPaymentStore();
        var adapter = CreateAdapter(store);
        var orderId = Guid.NewGuid();
        await adapter.CaptureAsync(orderId, 25m, "key-refund", CancellationToken.None);

        await adapter.RefundAsync(orderId, "corr-refund", CancellationToken.None);

        var record = await store.FindByOrderIdAsync(orderId);
        Assert.Equal(PaymentStatus.Refunded, record!.Status);
    }

    [Fact]
    public async Task RefundAsync_for_unknown_order_is_a_no_op()
    {
        var store = new InMemoryPaymentStore();
        var adapter = CreateAdapter(store);

        await adapter.RefundAsync(Guid.NewGuid(), "corr-none", CancellationToken.None);

        Assert.Equal(0, store.Count);
    }
}
