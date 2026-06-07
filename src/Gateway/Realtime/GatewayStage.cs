using RestaurantDelivery.Contracts;
using RestaurantDelivery.Contracts.Events;

namespace RestaurantDelivery.Gateway.Realtime;

/// <summary>
/// The gateway's local copy of the 5-stage tracking model (ADR-007). It deliberately mirrors the Tracking
/// service's <c>TrackingStage</c> values so a client resyncing over <c>GET /api/orders/{id}/status</c>
/// (proxied to Tracking) sees the same numbers the live SignalR push uses. Kept in the gateway — and NOT in
/// shared Contracts — because the status push is a BFF concern, not a cross-service message (task_14.3).
/// </summary>
public enum GatewayStage
{
    /// <summary>No mapped event yet.</summary>
    Unknown = 0,

    /// <summary>Stage 1 — order placed (← <c>OrderPlaced</c>; <c>PaymentSettled</c> stays here).</summary>
    OrderPlaced = 1,

    /// <summary>Stage 2 — preparing (← <c>OrderAccepted</c>; <c>OrderReady</c> stays here).</summary>
    Preparing = 2,

    /// <summary>Stage 3 — a driver is assigned (← <c>DriverAssigned</c>).</summary>
    DriverAssigned = 3,

    /// <summary>Stage 4 — out for delivery (← <c>OrderPickedUp</c>).</summary>
    OutForDelivery = 4,

    /// <summary>Stage 5 — delivered (← <c>OrderDelivered</c>).</summary>
    Delivered = 5,

    /// <summary>Terminal — refunded/cancelled (← <c>OrderRefunded</c>).</summary>
    Refunded = 99,
}

/// <summary>
/// Maps an order-lifecycle integration event to the <see cref="GatewayStage"/> it represents. This is the
/// gateway-local mirror of Tracking's <c>StageMapping</c> — the gateway derives the stage from the event
/// type it consumes off the bus rather than depending on a cross-service status message (task_14.3).
/// </summary>
public static class GatewayStageMapping
{
    /// <summary>
    /// Returns the stage an event maps to, or <see cref="GatewayStage.Unknown"/> when the event has no
    /// tracking meaning (the hub does not push for unmapped events).
    /// </summary>
    public static GatewayStage ToStage(IIntegrationEvent @event) => @event switch
    {
        OrderPlaced => GatewayStage.OrderPlaced,
        PaymentSettled => GatewayStage.OrderPlaced,
        OrderAccepted => GatewayStage.Preparing,
        OrderReady => GatewayStage.Preparing,
        DriverAssigned => GatewayStage.DriverAssigned,
        OrderPickedUp => GatewayStage.OutForDelivery,
        OrderDelivered => GatewayStage.Delivered,
        OrderRefunded => GatewayStage.Refunded,
        _ => GatewayStage.Unknown,
    };
}
