using MassTransit;
using RestaurantDelivery.Contracts;
using RestaurantDelivery.Contracts.Events;
using RestaurantDelivery.Platform;

namespace RestaurantDelivery.Notification.Notifications;

/// <summary>
/// Base consumer that turns one order lifecycle event into exactly one fire-and-forget notification.
/// Idempotent on <c>(OrderId, CorrelationId)</c> via the Platform <see cref="IIdempotencyStore"/>
/// (ADR-004): a redelivered event yields no duplicate notification. Unhandled event types simply have
/// no consumer registered, so they produce no notification and no error.
/// </summary>
/// <typeparam name="TEvent">The integration event this consumer handles.</typeparam>
public abstract class OrderNotificationConsumer<TEvent>(INotificationPort port, IIdempotencyStore idempotency)
    : IConsumer<TEvent>
    where TEvent : class, IOrderMessage
{
    public async Task Consume(ConsumeContext<TEvent> context)
    {
        var message = context.Message;
        var key = IdempotencyKey.For(message.OrderId, message.CorrelationId);

        await idempotency.RunOnceAsync(
            key,
            () => port.SendAsync(
                new NotificationMessage(message.OrderId, message.CorrelationId, Render(message)),
                context.CancellationToken),
            context.CancellationToken);
    }

    /// <summary>Renders the human-readable notification body for this event.</summary>
    protected abstract string Render(TEvent message);
}

public sealed class OrderPlacedNotificationConsumer(INotificationPort port, IIdempotencyStore idempotency)
    : OrderNotificationConsumer<OrderPlaced>(port, idempotency)
{
    protected override string Render(OrderPlaced message) =>
        $"Your order {message.OrderId} has been placed.";
}

public sealed class OrderReadyNotificationConsumer(INotificationPort port, IIdempotencyStore idempotency)
    : OrderNotificationConsumer<OrderReady>(port, idempotency)
{
    protected override string Render(OrderReady message) =>
        $"Your order {message.OrderId} is ready.";
}

public sealed class DriverAssignedNotificationConsumer(INotificationPort port, IIdempotencyStore idempotency)
    : OrderNotificationConsumer<DriverAssigned>(port, idempotency)
{
    protected override string Render(DriverAssigned message) =>
        $"Driver {message.DriverName} is on the way for order {message.OrderId} (ETA {message.EtaMinutes} min).";
}

public sealed class OrderDeliveredNotificationConsumer(INotificationPort port, IIdempotencyStore idempotency)
    : OrderNotificationConsumer<OrderDelivered>(port, idempotency)
{
    protected override string Render(OrderDelivered message) =>
        $"Your order {message.OrderId} has been delivered. Enjoy!";
}

public sealed class OrderRefundedNotificationConsumer(INotificationPort port, IIdempotencyStore idempotency)
    : OrderNotificationConsumer<OrderRefunded>(port, idempotency)
{
    protected override string Render(OrderRefunded message) =>
        $"Your order {message.OrderId} was refunded.";
}
