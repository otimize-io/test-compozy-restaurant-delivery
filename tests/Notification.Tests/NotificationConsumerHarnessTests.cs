using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using RestaurantDelivery.Contracts;
using RestaurantDelivery.Contracts.Events;
using RestaurantDelivery.Notification.Notifications;
using RestaurantDelivery.Platform;

namespace Notification.Tests;

/// <summary>
/// Integration: drives the notification consumers through MassTransit's fully in-memory test harness
/// (no broker / Docker). A recording fake <see cref="INotificationPort"/> captures what each consumer
/// produced. Mirrors the harness style of <c>Contracts.Tests.MessagePublishHarnessTests</c>.
/// </summary>
[Trait("Category", "Integration")]
public class NotificationConsumerHarnessTests
{
    private static async Task<(ITestHarness Harness, RecordingNotificationPort Port, ServiceProvider Provider)>
        StartHarnessAsync()
    {
        var port = new RecordingNotificationPort();

        var provider = new ServiceCollection()
            .AddSingleton<INotificationPort>(port)
            .AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<OrderPlacedNotificationConsumer>();
                cfg.AddConsumer<OrderReadyNotificationConsumer>();
                cfg.AddConsumer<DriverAssignedNotificationConsumer>();
                cfg.AddConsumer<OrderDeliveredNotificationConsumer>();
                cfg.AddConsumer<OrderRefundedNotificationConsumer>();
            })
            .BuildServiceProvider(validateScopes: true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return (harness, port, provider);
    }

    [Fact]
    public async Task OrderDelivered_produces_exactly_one_notification_for_that_order()
    {
        var (harness, port, provider) = await StartHarnessAsync();
        await using var _ = provider;

        var orderId = Guid.NewGuid();
        await harness.Bus.Publish(new OrderDelivered(orderId, "corr-delivered"));

        Assert.True(await harness.Consumed.Any<OrderDelivered>());

        var sent = port.Sent;
        Assert.Single(sent);
        Assert.Equal(orderId, sent.Single().OrderId);
        Assert.Equal("corr-delivered", sent.Single().CorrelationId);
    }

    [Fact]
    public async Task OrderReady_produces_exactly_one_notification_for_that_order()
    {
        var (harness, port, provider) = await StartHarnessAsync();
        await using var _ = provider;

        var orderId = Guid.NewGuid();
        await harness.Bus.Publish(new OrderReady(orderId, "corr-ready"));

        Assert.True(await harness.Consumed.Any<OrderReady>());

        Assert.Single(port.Sent);
        Assert.Equal(orderId, port.Sent.Single().OrderId);
    }

    [Fact]
    public async Task Each_handled_lifecycle_event_yields_one_notification()
    {
        var (harness, port, provider) = await StartHarnessAsync();
        await using var _ = provider;

        var orderId = Guid.NewGuid();
        await harness.Bus.Publish(new OrderPlaced(
            orderId, "c1", Guid.NewGuid(), Guid.NewGuid(), 30m,
            [new OrderLine(Guid.NewGuid(), "Burger", 1, 30m)]));
        await harness.Bus.Publish(new OrderReady(orderId, "c2"));
        await harness.Bus.Publish(new DriverAssigned(orderId, "c3", Guid.NewGuid(), "Sam", 12));
        await harness.Bus.Publish(new OrderDelivered(orderId, "c4"));
        await harness.Bus.Publish(new OrderRefunded(orderId, "c5"));

        Assert.True(await harness.Consumed.Any<OrderPlaced>());
        Assert.True(await harness.Consumed.Any<OrderReady>());
        Assert.True(await harness.Consumed.Any<DriverAssigned>());
        Assert.True(await harness.Consumed.Any<OrderDelivered>());
        Assert.True(await harness.Consumed.Any<OrderRefunded>());

        Assert.Equal(5, port.Sent.Count);
        Assert.All(port.Sent, m => Assert.Equal(orderId, m.OrderId));
    }

    [Fact]
    public async Task Redelivered_event_is_idempotent_and_notifies_once()
    {
        var (harness, port, provider) = await StartHarnessAsync();
        await using var _ = provider;

        var orderId = Guid.NewGuid();
        var ready = new OrderReady(orderId, "corr-dup");

        await harness.Bus.Publish(ready);
        await harness.Bus.Publish(ready);

        Assert.True(await harness.Consumed.Any<OrderReady>());
        // Same (OrderId, CorrelationId) → idempotency store suppresses the duplicate notification.
        Assert.Single(port.Sent);
    }

    [Fact]
    public async Task Unhandled_event_type_produces_no_notification_and_no_error()
    {
        var (harness, port, provider) = await StartHarnessAsync();
        await using var _ = provider;

        // DriverUnavailable has no consumer registered in Notification — it must be a no-op.
        await harness.Bus.Publish(new DriverUnavailable(Guid.NewGuid(), "corr-none"));

        Assert.True(await harness.Published.Any<DriverUnavailable>());
        Assert.Empty(port.Sent);
    }
}
