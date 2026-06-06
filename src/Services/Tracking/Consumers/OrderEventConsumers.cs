using MassTransit;
using RestaurantDelivery.Contracts;
using RestaurantDelivery.Contracts.Events;
using RestaurantDelivery.Platform;
using RestaurantDelivery.Tracking.Projection;

namespace RestaurantDelivery.Tracking.Consumers;

/// <summary>
/// Shared projection logic for every tracking consumer: idempotently project one lifecycle event onto
/// its order's <see cref="TrackingView"/> (task_12). Idempotent on <c>(OrderId, CorrelationId)</c> via
/// the Platform <see cref="IIdempotencyStore"/> so a redelivered event is projected at most once; the
/// projector's monotonic stage advancement additionally makes out-of-order events safe.
/// </summary>
public static class TrackingProjectionHandler
{
    public static Task ProjectAsync<TEvent>(
        TrackingProjector projector,
        IIdempotencyStore idempotency,
        ConsumeContext<TEvent> context)
        where TEvent : class, IIntegrationEvent
    {
        var message = context.Message;
        var key = IdempotencyKey.For(message.OrderId, $"{typeof(TEvent).Name}:{message.CorrelationId}");

        return idempotency.RunOnceAsync(
            key,
            () => projector.ApplyAsync(message, context.CancellationToken),
            context.CancellationToken);
    }
}

/// <summary>Projects <see cref="OrderPlaced"/> → stage 1 (Order placed).</summary>
public sealed class OrderPlacedConsumer(TrackingProjector projector, IIdempotencyStore idempotency)
    : IConsumer<OrderPlaced>
{
    public Task Consume(ConsumeContext<OrderPlaced> context) =>
        TrackingProjectionHandler.ProjectAsync(projector, idempotency, context);
}

/// <summary>Projects <see cref="PaymentSettled"/> (stays at stage 1; payment is a sub-step of "order placed").</summary>
public sealed class PaymentSettledConsumer(TrackingProjector projector, IIdempotencyStore idempotency)
    : IConsumer<PaymentSettled>
{
    public Task Consume(ConsumeContext<PaymentSettled> context) =>
        TrackingProjectionHandler.ProjectAsync(projector, idempotency, context);
}

/// <summary>Projects <see cref="OrderAccepted"/> → stage 2 (Preparing).</summary>
public sealed class OrderAcceptedConsumer(TrackingProjector projector, IIdempotencyStore idempotency)
    : IConsumer<OrderAccepted>
{
    public Task Consume(ConsumeContext<OrderAccepted> context) =>
        TrackingProjectionHandler.ProjectAsync(projector, idempotency, context);
}

/// <summary>Projects <see cref="OrderReady"/> (stays at stage 2; a sub-step of Preparing).</summary>
public sealed class OrderReadyConsumer(TrackingProjector projector, IIdempotencyStore idempotency)
    : IConsumer<OrderReady>
{
    public Task Consume(ConsumeContext<OrderReady> context) =>
        TrackingProjectionHandler.ProjectAsync(projector, idempotency, context);
}

/// <summary>Projects <see cref="DriverAssigned"/> → stage 3 (Driver assigned / en route).</summary>
public sealed class DriverAssignedConsumer(TrackingProjector projector, IIdempotencyStore idempotency)
    : IConsumer<DriverAssigned>
{
    public Task Consume(ConsumeContext<DriverAssigned> context) =>
        TrackingProjectionHandler.ProjectAsync(projector, idempotency, context);
}

/// <summary>Projects <see cref="OrderPickedUp"/> → stage 4 (Out for delivery).</summary>
public sealed class OrderPickedUpConsumer(TrackingProjector projector, IIdempotencyStore idempotency)
    : IConsumer<OrderPickedUp>
{
    public Task Consume(ConsumeContext<OrderPickedUp> context) =>
        TrackingProjectionHandler.ProjectAsync(projector, idempotency, context);
}

/// <summary>Projects <see cref="OrderDelivered"/> → stage 5 (Delivered).</summary>
public sealed class OrderDeliveredConsumer(TrackingProjector projector, IIdempotencyStore idempotency)
    : IConsumer<OrderDelivered>
{
    public Task Consume(ConsumeContext<OrderDelivered> context) =>
        TrackingProjectionHandler.ProjectAsync(projector, idempotency, context);
}

/// <summary>Projects <see cref="OrderRefunded"/> → the refunded terminal stage.</summary>
public sealed class OrderRefundedConsumer(TrackingProjector projector, IIdempotencyStore idempotency)
    : IConsumer<OrderRefunded>
{
    public Task Consume(ConsumeContext<OrderRefunded> context) =>
        TrackingProjectionHandler.ProjectAsync(projector, idempotency, context);
}
