using MassTransit;
using RestaurantDelivery.Contracts;
using RestaurantDelivery.Contracts.Events;

namespace RestaurantDelivery.Gateway.Realtime;

/// <summary>
/// The gateway's bus consumers for the order lifecycle (ADR-007, task_14.3). The gateway subscribes to the
/// same lifecycle events the saga emits and Tracking projects, and — for each — derives the stage locally and
/// pushes <c>OrderStatusChanged</c> to the order's SignalR group via <see cref="OrderStatusBroadcaster"/>.
/// One consumer per event type so MassTransit's automatic endpoint configuration binds each event's exchange;
/// all of them funnel through the shared broadcaster. No idempotency guard is needed: a duplicate push only
/// re-announces a stage the client already shows, and the client's stage bar is monotonic.
/// </summary>
public static class GatewayEventConsumer
{
    public static Task PushAsync<TEvent>(OrderStatusBroadcaster broadcaster, ConsumeContext<TEvent> context)
        where TEvent : class, IIntegrationEvent =>
        broadcaster.BroadcastAsync(context.Message, context.CancellationToken);
}

/// <summary>Pushes the order-placed stage (stage 1) to the order group.</summary>
public sealed class OrderPlacedHubConsumer(OrderStatusBroadcaster broadcaster) : IConsumer<OrderPlaced>
{
    public Task Consume(ConsumeContext<OrderPlaced> context) => GatewayEventConsumer.PushAsync(broadcaster, context);
}

/// <summary>Pushes the payment-settled stage (stays at stage 1) to the order group.</summary>
public sealed class PaymentSettledHubConsumer(OrderStatusBroadcaster broadcaster) : IConsumer<PaymentSettled>
{
    public Task Consume(ConsumeContext<PaymentSettled> context) => GatewayEventConsumer.PushAsync(broadcaster, context);
}

/// <summary>Pushes the preparing stage (stage 2) to the order group.</summary>
public sealed class OrderAcceptedHubConsumer(OrderStatusBroadcaster broadcaster) : IConsumer<OrderAccepted>
{
    public Task Consume(ConsumeContext<OrderAccepted> context) => GatewayEventConsumer.PushAsync(broadcaster, context);
}

/// <summary>Pushes the ready/preparing stage (stays at stage 2) to the order group.</summary>
public sealed class OrderReadyHubConsumer(OrderStatusBroadcaster broadcaster) : IConsumer<OrderReady>
{
    public Task Consume(ConsumeContext<OrderReady> context) => GatewayEventConsumer.PushAsync(broadcaster, context);
}

/// <summary>Pushes the driver-assigned stage (stage 3) to the order group.</summary>
public sealed class DriverAssignedHubConsumer(OrderStatusBroadcaster broadcaster) : IConsumer<DriverAssigned>
{
    public Task Consume(ConsumeContext<DriverAssigned> context) => GatewayEventConsumer.PushAsync(broadcaster, context);
}

/// <summary>Pushes the out-for-delivery stage (stage 4) to the order group.</summary>
public sealed class OrderPickedUpHubConsumer(OrderStatusBroadcaster broadcaster) : IConsumer<OrderPickedUp>
{
    public Task Consume(ConsumeContext<OrderPickedUp> context) => GatewayEventConsumer.PushAsync(broadcaster, context);
}

/// <summary>Pushes the delivered stage (stage 5) to the order group.</summary>
public sealed class OrderDeliveredHubConsumer(OrderStatusBroadcaster broadcaster) : IConsumer<OrderDelivered>
{
    public Task Consume(ConsumeContext<OrderDelivered> context) => GatewayEventConsumer.PushAsync(broadcaster, context);
}

/// <summary>Pushes the refunded terminal stage to the order group (compensation path).</summary>
public sealed class OrderRefundedHubConsumer(OrderStatusBroadcaster broadcaster) : IConsumer<OrderRefunded>
{
    public Task Consume(ConsumeContext<OrderRefunded> context) => GatewayEventConsumer.PushAsync(broadcaster, context);
}
