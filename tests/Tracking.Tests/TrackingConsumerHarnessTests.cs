using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using RestaurantDelivery.Contracts;
using RestaurantDelivery.Contracts.Events;
using RestaurantDelivery.Platform;
using RestaurantDelivery.Tracking.Consumers;
using RestaurantDelivery.Tracking.Projection;

namespace Tracking.Tests;

/// <summary>
/// Drives the tracking consumers through MassTransit's fully in-memory test harness (no broker / Docker),
/// backed by an in-memory <see cref="ITrackingStore"/>. Mirrors the harness style of
/// <c>Dispatch.Tests.DriverRequestedConsumerHarnessTests</c>: publishes lifecycle events and asserts the
/// projection in the store, including idempotency on redelivery.
/// </summary>
public class TrackingConsumerHarnessTests
{
    private const string Corr = "corr-h";

    private static async Task<(ITestHarness Harness, ServiceProvider Provider, ITrackingStore Store)> StartHarnessAsync()
    {
        var store = new InMemoryTrackingStore();
        var provider = new ServiceCollection()
            .AddSingleton<ITrackingStore>(store)
            .AddSingleton<TrackingProjector>()
            .AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<OrderPlacedConsumer>();
                cfg.AddConsumer<PaymentSettledConsumer>();
                cfg.AddConsumer<OrderAcceptedConsumer>();
                cfg.AddConsumer<OrderReadyConsumer>();
                cfg.AddConsumer<DriverAssignedConsumer>();
                cfg.AddConsumer<OrderPickedUpConsumer>();
                cfg.AddConsumer<OrderDeliveredConsumer>();
                cfg.AddConsumer<OrderRefundedConsumer>();
            })
            .BuildServiceProvider(validateScopes: true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return (harness, provider, store);
    }

    [Fact]
    public async Task A_sequence_of_lifecycle_events_projects_to_the_latest_stage()
    {
        var (harness, provider, store) = await StartHarnessAsync();
        await using var _ = provider;

        var orderId = Guid.NewGuid();
        await harness.Bus.Publish(NewPlaced(orderId));
        await harness.Bus.Publish(new OrderAccepted(orderId, Corr));
        await harness.Bus.Publish(new DriverAssigned(orderId, Corr, Guid.NewGuid(), "Alice", 5));
        await harness.Bus.Publish(new OrderPickedUp(orderId, Corr));
        await harness.Bus.Publish(new OrderDelivered(orderId, Corr));

        Assert.True(await harness.Consumed.Any<OrderDelivered>());

        var view = await store.GetAsync(orderId);
        Assert.NotNull(view);
        Assert.Equal(TrackingStage.Delivered, view!.Stage);
    }

    [Fact]
    public async Task OrderRefunded_projects_the_refunded_terminal_stage()
    {
        var (harness, provider, store) = await StartHarnessAsync();
        await using var _ = provider;

        var orderId = Guid.NewGuid();
        await harness.Bus.Publish(NewPlaced(orderId));
        await harness.Bus.Publish(new OrderRefunded(orderId, Corr));

        Assert.True(await harness.Consumed.Any<OrderRefunded>());

        var view = await store.GetAsync(orderId);
        Assert.NotNull(view);
        Assert.Equal(TrackingStage.Refunded, view!.Stage);
    }

    [Fact]
    public async Task Redelivered_event_is_idempotent_and_does_not_regress_the_stage()
    {
        var (harness, provider, store) = await StartHarnessAsync();
        await using var _ = provider;

        var orderId = Guid.NewGuid();
        await harness.Bus.Publish(NewPlaced(orderId));
        await harness.Bus.Publish(new OrderDelivered(orderId, Corr));

        // Redeliver the same (OrderId, CorrelationId) accepted event twice — must not move back to stage 2.
        var accepted = new OrderAccepted(orderId, Corr);
        await harness.Bus.Publish(accepted);
        await harness.Bus.Publish(accepted);

        Assert.True(await harness.Consumed.Any<OrderDelivered>());

        var view = await store.GetAsync(orderId);
        Assert.NotNull(view);
        Assert.Equal(TrackingStage.Delivered, view!.Stage);
    }

    [Fact]
    public async Task PaymentSettled_keeps_stage_1_and_OrderReady_keeps_stage_2()
    {
        var (harness, provider, store) = await StartHarnessAsync();
        await using var _ = provider;

        var orderId = Guid.NewGuid();
        await harness.Bus.Publish(NewPlaced(orderId));
        await harness.Bus.Publish(new PaymentSettled(orderId, Corr));

        Assert.True(await harness.Consumed.Any<PaymentSettled>());
        var afterPayment = await store.GetAsync(orderId);
        Assert.NotNull(afterPayment);
        Assert.Equal(TrackingStage.OrderPlaced, afterPayment!.Stage);

        await harness.Bus.Publish(new OrderAccepted(orderId, Corr));
        await harness.Bus.Publish(new OrderReady(orderId, Corr));

        Assert.True(await harness.Consumed.Any<OrderReady>());
        var afterReady = await store.GetAsync(orderId);
        Assert.NotNull(afterReady);
        Assert.Equal(TrackingStage.Preparing, afterReady!.Stage);
    }

    private static OrderPlaced NewPlaced(Guid orderId) => new(
        orderId, Corr, Guid.NewGuid(), Guid.NewGuid(), 42m,
        [new OrderLine(Guid.NewGuid(), "Pizza", 1, 42m)]);
}
