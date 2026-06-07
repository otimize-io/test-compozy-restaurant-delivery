using Microsoft.AspNetCore.SignalR;
using RestaurantDelivery.Contracts;
using RestaurantDelivery.Gateway.Hubs;

namespace RestaurantDelivery.Gateway.Realtime;

/// <summary>
/// Derives the live stage from an order-lifecycle event and pushes an <see cref="OrderStatusChanged"/> to that
/// order's SignalR group (ADR-007, task_14.3). The gateway consumes the lifecycle events directly off the bus
/// (it does not depend on a Tracking status message) and computes the stage locally via
/// <see cref="GatewayStageMapping"/>. Events with no tracking meaning (<see cref="GatewayStage.Unknown"/>) are
/// not pushed, so only the five forward stages and the refunded terminal stage reach clients.
/// </summary>
public sealed class OrderStatusBroadcaster(IHubContext<OrdersHub> hub)
{
    public Task BroadcastAsync(IIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var stage = GatewayStageMapping.ToStage(@event);
        if (stage == GatewayStage.Unknown)
        {
            return Task.CompletedTask;
        }

        var payload = new OrderStatusChanged(@event.OrderId, (int)stage, stage.ToString());
        return hub.Clients
            .Group(OrdersHub.GroupFor(@event.OrderId))
            .SendAsync(OrdersHub.StatusChangedMethod, payload, cancellationToken);
    }
}
