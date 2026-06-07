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
/// Each test waits for EVERY published event to be consumed (so no consume is in flight) and stops the bus
/// gracefully. The provider is intentionally NOT disposed: disposing the MassTransit 8 in-memory harness
/// can race a late consume that resolves a service from the disposing provider. The short-lived test
/// process reclaims it on exit. This makes the harness tests deterministic.
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

    /// <summary>Stops the in-memory bus gracefully so no consume is left in flight (provider is not disposed).</summary>
    private static Task StopAsync(ServiceProvider provider) =>
        provider.GetRequiredService<IBusControl>().StopAsync();

    [Fact]
    public async Task A_sequence_of_lifecycle_events_projects_to_the_latest_stage()
    {
        var (harness, provider, store) = await StartHarnessAsync();

        var orderId = Guid.NewGuid();
        await harness.Bus.Publish(NewPlaced(orderId));
        await harness.Bus.Publish(new OrderAccepted(orderId, Corr));
        await harness.Bus.Publish(new DriverAssigned(orderId, Corr, Guid.NewGuid(), "Alice", 5));
        await harness.Bus.Publish(new OrderPickedUp(orderId, Corr));
        await harness.Bus.Publish(new OrderDelivered(orderId, Corr));

        Assert.True(await harness.Consumed.Any<OrderPlaced>());
        Assert.True(await harness.Consumed.Any<OrderAccepted>());
        Assert.True(await harness.Consumed.Any<DriverAssigned>());
        Assert.True(await harness.Consumed.Any<OrderPickedUp>());
        Assert.True(await harness.Consumed.Any<OrderDelivered>());

        var view = await store.GetAsync(orderId);
        Assert.NotNull(view);
        Assert.Equal(TrackingStage.Delivered, view!.Stage);

        await StopAsync(provider);
    }

    [Fact]
    public async Task OrderRefunded_projects_the_refunded_terminal_stage()
    {
        var (harness, provider, store) = await StartHarnessAsync();

        var orderId = Guid.NewGuid();
        await harness.Bus.Publish(NewPlaced(orderId));
        await harness.Bus.Publish(new OrderRefunded(orderId, Corr));

        Assert.True(await harness.Consumed.Any<OrderPlaced>());
        Assert.True(await harness.Consumed.Any<OrderRefunded>());

        var view = await store.GetAsync(orderId);
        Assert.NotNull(view);
        Assert.Equal(TrackingStage.Refunded, view!.Stage);

        await StopAsync(provider);
    }

    [Fact]
    public async Task Redelivered_event_is_idempotent_and_does_not_regress_the_stage()
    {
        var (harness, provider, store) = await StartHarnessAsync();

        var orderId = Guid.NewGuid();
        await harness.Bus.Publish(NewPlaced(orderId));
        await harness.Bus.Publish(new OrderDelivered(orderId, Corr));

        // Redeliver the same (OrderId, CorrelationId) accepted event twice — must not move back to stage 2.
        var accepted = new OrderAccepted(orderId, Corr);
        await harness.Bus.Publish(accepted);
        await harness.Bus.Publish(accepted);

        Assert.True(await harness.Consumed.Any<OrderPlaced>());
        Assert.True(await harness.Consumed.Any<OrderDelivered>());
        // Both accepted redeliveries must be consumed before we assert + tear down.
        Assert.True(await harness.Consumed.SelectAsync<OrderAccepted>().Count() >= 2);

        var view = await store.GetAsync(orderId);
        Assert.NotNull(view);
        Assert.Equal(TrackingStage.Delivered, view!.Stage);

        await StopAsync(provider);
    }

    [Fact]
    public async Task PaymentSettled_keeps_stage_1_and_OrderReady_keeps_stage_2()
    {
        var (harness, provider, store) = await StartHarnessAsync();

        var orderId = Guid.NewGuid();
        await harness.Bus.Publish(NewPlaced(orderId));
        await harness.Bus.Publish(new PaymentSettled(orderId, Corr));

        Assert.True(await harness.Consumed.Any<OrderPlaced>());
        Assert.True(await harness.Consumed.Any<PaymentSettled>());
        var afterPayment = await store.GetAsync(orderId);
        Assert.NotNull(afterPayment);
        Assert.Equal(TrackingStage.OrderPlaced, afterPayment!.Stage);

        await harness.Bus.Publish(new OrderAccepted(orderId, Corr));
        await harness.Bus.Publish(new OrderReady(orderId, Corr));

        Assert.True(await harness.Consumed.Any<OrderAccepted>());
        Assert.True(await harness.Consumed.Any<OrderReady>());
        var afterReady = await store.GetAsync(orderId);
        Assert.NotNull(afterReady);
        Assert.Equal(TrackingStage.Preparing, afterReady!.Stage);

        await StopAsync(provider);
    }

    private static OrderPlaced NewPlaced(Guid orderId) => new(
        orderId, Corr, Guid.NewGuid(), Guid.NewGuid(), 42m,
        [new OrderLine(Guid.NewGuid(), "Pizza", 1, 42m)]);
}
