using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RestaurantDelivery.Payment.Payments;
using RestaurantDelivery.Payment.Ports;

namespace Payment.Tests;

/// <summary>
/// Unit tests for the stub-real <see cref="IPaymentPort"/> adapter. It must satisfy the same seam contract
/// as the mock — accepted (not terminal) capture, idempotent on the key (one charge), and a recordable
/// refund — so the swap-contract test can prove the seam is replaceable with zero neighbour changes.
/// </summary>
public class StubRealPaymentAdapterTests
{
    private static StubRealPaymentAdapter CreateAdapter(IPaymentStore store, PaymentOptions? options = null) =>
        new(store, Options.Create(options ?? new PaymentOptions()), NullLogger<StubRealPaymentAdapter>.Instance);

    [Fact]
    public async Task CaptureAsync_records_an_accepted_charge_with_a_provider_reference()
    {
        var store = new InMemoryPaymentStore();
        var adapter = CreateAdapter(store);

        var accepted = await adapter.CaptureAsync(Guid.NewGuid(), 60m, "psp-accept", CancellationToken.None);

        Assert.StartsWith("psp_", accepted.CorrelationId);
        Assert.Equal(1, store.Count);
        Assert.Equal(PaymentStatus.Accepted, (await store.FindByIdempotencyKeyAsync("psp-accept"))!.Status);
    }

    [Fact]
    public async Task Repeated_capture_with_same_key_returns_same_result_and_charges_once()
    {
        var store = new InMemoryPaymentStore();
        var adapter = CreateAdapter(store);
        var orderId = Guid.NewGuid();

        var first = await adapter.CaptureAsync(orderId, 60m, "psp-dup", CancellationToken.None);
        var second = await adapter.CaptureAsync(orderId, 60m, "psp-dup", CancellationToken.None);

        Assert.Equal(first.CorrelationId, second.CorrelationId);
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public async Task Capture_honours_the_configured_decline_threshold()
    {
        var store = new InMemoryPaymentStore();
        var adapter = CreateAdapter(store, new PaymentOptions { DeclineAtOrAbove = 200m });

        await adapter.CaptureAsync(Guid.NewGuid(), 250m, "psp-decline", CancellationToken.None);

        Assert.Equal(PlannedSettlement.Decline, (await store.FindByIdempotencyKeyAsync("psp-decline"))!.Plan);
    }

    [Fact]
    public async Task RefundAsync_marks_the_order_payment_refunded()
    {
        var store = new InMemoryPaymentStore();
        var adapter = CreateAdapter(store);
        var orderId = Guid.NewGuid();
        await adapter.CaptureAsync(orderId, 60m, "psp-refund", CancellationToken.None);

        await adapter.RefundAsync(orderId, "psp-corr", CancellationToken.None);

        Assert.Equal(PaymentStatus.Refunded, (await store.FindByOrderIdAsync(orderId))!.Status);
    }

    [Fact]
    public async Task RefundAsync_for_unknown_order_is_a_no_op()
    {
        var store = new InMemoryPaymentStore();
        var adapter = CreateAdapter(store);

        await adapter.RefundAsync(Guid.NewGuid(), "psp-none", CancellationToken.None);

        Assert.Equal(0, store.Count);
    }
}
