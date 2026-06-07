using Microsoft.AspNetCore.SignalR;

namespace RestaurantDelivery.Gateway.Hubs;

/// <summary>
/// The SignalR hub at <c>/hubs/orders</c> (ADR-007, task_14.3). Clients call <see cref="Subscribe"/> with an
/// order id to join that order's group, and the gateway's bus consumer pushes <c>OrderStatusChanged</c> to the
/// group as lifecycle events arrive — so the consumer's 5-stage bar and all three role views update live.
/// On (re)connect a client resyncs the current stage over REST (<c>GET /api/orders/{id}/status</c>, proxied
/// to Tracking) to recover any event missed while disconnected (ADR-007 reconnect mitigation, task_14.4).
/// The hub holds no business logic: it only manages per-order group membership.
/// </summary>
public sealed class OrdersHub : Hub
{
    /// <summary>The client method invoked with each <c>OrderStatusChanged</c> push.</summary>
    public const string StatusChangedMethod = "OrderStatusChanged";

    /// <summary>Builds the deterministic per-order group name a subscriber joins and the consumer pushes to.</summary>
    public static string GroupFor(Guid orderId) => $"order-{orderId:N}";

    /// <summary>Joins the caller to the per-order group so it receives that order's live status pushes.</summary>
    public Task Subscribe(Guid orderId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(orderId));

    /// <summary>Removes the caller from the per-order group (e.g. when it stops tracking an order).</summary>
    public Task Unsubscribe(Guid orderId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(orderId));
}
