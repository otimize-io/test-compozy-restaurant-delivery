using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using RestaurantDelivery.Contracts.Events;
using RestaurantDelivery.Payment.Callbacks;
using RestaurantDelivery.Payment.Payments;

namespace Payment.Tests;

/// <summary>
/// Unit tests for the settlement webhook logic via MassTransit's fully in-memory harness (no broker /
/// Docker). Drives <see cref="SettlementService"/> exactly as the <c>/api/payments/callback</c> endpoint
/// does and asserts it publishes <see cref="PaymentSettled"/> / <see cref="PaymentDeclined"/> on the bus.
/// </summary>
public class SettlementServiceTests
{
    private static async Task<(ITestHarness Harness, ServiceProvider Provider, InMemoryPaymentStore Store)> StartAsync()
    {
        var store = new InMemoryPaymentStore();
        var provider = new ServiceCollection()
            .AddSingleton<IPaymentStore>(store)
            .AddMassTransitTestHarness()
            .BuildServiceProvider(validateScopes: true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return (harness, provider, store);
    }

    private static async Task<PaymentRecord> SeedAcceptedAsync(
        InMemoryPaymentStore store, Guid orderId, PlannedSettlement plan)
    {
        var record = new PaymentRecord
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Amount = 30m,
            IdempotencyKey = $"key-{orderId:N}",
            Status = PaymentStatus.Accepted,
            CorrelationId = $"corr-{orderId:N}",
            Plan = plan,
        };
        await store.AddAsync(record);
        return record;
    }

    [Fact]
    public async Task Callback_for_settle_planned_payment_publishes_PaymentSettled()
    {
        var (harness, provider, store) = await StartAsync();
        await using var _ = provider;
        var orderId = Guid.NewGuid();
        var record = await SeedAcceptedAsync(store, orderId, PlannedSettlement.Settle);

        var service = new SettlementService(store, harness.Bus);
        var result = await service.SettleAsync(new SettlementCallbackRequest(orderId));

        Assert.Equal(SettlementResult.Settled, result);
        Assert.True(await harness.Published.Any<PaymentSettled>(p =>
            p.Context!.Message.OrderId == orderId && p.Context.Message.CorrelationId == record.CorrelationId));
        Assert.False(await harness.Published.Any<PaymentDeclined>());
        Assert.Equal(PaymentStatus.Settled, (await store.FindByOrderIdAsync(orderId))!.Status);
    }

    [Fact]
    public async Task Callback_for_decline_planned_payment_publishes_PaymentDeclined()
    {
        var (harness, provider, store) = await StartAsync();
        await using var _ = provider;
        var orderId = Guid.NewGuid();
        var record = await SeedAcceptedAsync(store, orderId, PlannedSettlement.Decline);

        var service = new SettlementService(store, harness.Bus);
        var result = await service.SettleAsync(new SettlementCallbackRequest(orderId));

        Assert.Equal(SettlementResult.Declined, result);
        Assert.True(await harness.Published.Any<PaymentDeclined>(p =>
            p.Context!.Message.OrderId == orderId
            && p.Context.Message.CorrelationId == record.CorrelationId
            && !string.IsNullOrWhiteSpace(p.Context.Message.Reason)));
        Assert.False(await harness.Published.Any<PaymentSettled>());
        Assert.Equal(PaymentStatus.Declined, (await store.FindByOrderIdAsync(orderId))!.Status);
    }

    [Fact]
    public async Task Explicit_decline_outcome_overrides_a_settle_plan()
    {
        var (harness, provider, store) = await StartAsync();
        await using var _ = provider;
        var orderId = Guid.NewGuid();
        await SeedAcceptedAsync(store, orderId, PlannedSettlement.Settle);

        var service = new SettlementService(store, harness.Bus);
        var result = await service.SettleAsync(new SettlementCallbackRequest(orderId, "decline"));

        Assert.Equal(SettlementResult.Declined, result);
        Assert.True(await harness.Published.Any<PaymentDeclined>());
    }

    [Fact]
    public async Task Callback_for_unknown_order_returns_NotFound_and_publishes_nothing()
    {
        var (harness, provider, store) = await StartAsync();
        await using var _ = provider;

        var service = new SettlementService(store, harness.Bus);
        var result = await service.SettleAsync(new SettlementCallbackRequest(Guid.NewGuid()));

        Assert.Equal(SettlementResult.NotFound, result);
        Assert.False(await harness.Published.Any<PaymentSettled>());
        Assert.False(await harness.Published.Any<PaymentDeclined>());
    }

    [Fact]
    public async Task Callback_waits_for_a_late_capture_then_settles()
    {
        // The capture race: the settlement callback arrives before CapturePayment has been consumed, so the
        // record is not visible on the first lookup. SettleWaitingForCaptureAsync must re-check and settle
        // once the capture lands (here, on the 3rd lookup) instead of returning a spurious NotFound/404.
        var (harness, provider, _) = await StartAsync();
        await using var _ = provider;
        var orderId = Guid.NewGuid();
        var record = new PaymentRecord
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Amount = 30m,
            IdempotencyKey = $"key-{orderId:N}",
            Status = PaymentStatus.Accepted,
            CorrelationId = $"corr-{orderId:N}",
            Plan = PlannedSettlement.Settle,
        };
        var store = new CaptureAfterNLookupsStore(record, appearAfter: 3);

        var service = new SettlementService(store, harness.Bus);
        var result = await service.SettleWaitingForCaptureAsync(
            new SettlementCallbackRequest(orderId),
            timeout: TimeSpan.FromSeconds(2),
            interval: TimeSpan.FromMilliseconds(10));

        Assert.Equal(SettlementResult.Settled, result);
        Assert.True(await harness.Published.Any<PaymentSettled>(p => p.Context!.Message.OrderId == orderId));
    }

    [Fact]
    public async Task Callback_for_a_capture_that_never_arrives_times_out_to_NotFound()
    {
        // When no capture ever lands the wait is bounded: it returns NotFound (→ 404) after the timeout.
        var (harness, provider, store) = await StartAsync();
        await using var _ = provider;

        var service = new SettlementService(store, harness.Bus);
        var result = await service.SettleWaitingForCaptureAsync(
            new SettlementCallbackRequest(Guid.NewGuid()),
            timeout: TimeSpan.FromMilliseconds(60),
            interval: TimeSpan.FromMilliseconds(20));

        Assert.Equal(SettlementResult.NotFound, result);
        Assert.False(await harness.Published.Any<PaymentSettled>());
    }

    [Fact]
    public async Task Redelivered_callback_is_idempotent_and_publishes_once()
    {
        var (harness, provider, store) = await StartAsync();
        await using var _ = provider;
        var orderId = Guid.NewGuid();
        await SeedAcceptedAsync(store, orderId, PlannedSettlement.Settle);

        var service = new SettlementService(store, harness.Bus);
        var first = await service.SettleAsync(new SettlementCallbackRequest(orderId));
        var second = await service.SettleAsync(new SettlementCallbackRequest(orderId));

        Assert.Equal(SettlementResult.Settled, first);
        // Second call sees a terminal (already-settled) record → idempotent no-op, no second publish.
        Assert.Equal(SettlementResult.AlreadyResolved, second);
        Assert.Equal(1, await harness.Published.SelectAsync<PaymentSettled>().Count());
    }

    /// <summary>
    /// An <see cref="IPaymentStore"/> that hides the seeded record from <see cref="FindByOrderIdAsync"/> until
    /// the <c>appearAfter</c>-th lookup, simulating the capture landing after the settlement callback has
    /// already started polling. Lets the capture-race wait be tested deterministically without timing luck.
    /// </summary>
    private sealed class CaptureAfterNLookupsStore(PaymentRecord record, int appearAfter) : IPaymentStore
    {
        private int _lookups;

        public Task<PaymentRecord?> FindByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
        {
            var n = Interlocked.Increment(ref _lookups);
            return Task.FromResult(n >= appearAfter && orderId == record.OrderId ? record : null);
        }

        public Task<PaymentRecord?> FindByIdempotencyKeyAsync(
            string idempotencyKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<PaymentRecord?>(null);

        public Task AddAsync(PaymentRecord payment, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateAsync(PaymentRecord payment, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
