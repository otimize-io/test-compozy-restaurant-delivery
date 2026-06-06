namespace RestaurantDelivery.Tracking.Projection;

/// <summary>
/// The consumer-facing order status as a small ordered set of stages (PRD F8, TechSpec
/// "Data Models → TrackingView"). The five forward stages map to the order lifecycle and advance
/// monotonically (1 → 5); <see cref="Refunded"/> is the terminal compensation state.
/// </summary>
/// <remarks>
/// Numeric values double as the wire/Redis representation. The forward stages keep ascending values so
/// the projection can advance with a simple "never move backwards" comparison, making out-of-order and
/// duplicate events safe. <see cref="Refunded"/> sits above the forward stages so, once reached, no
/// in-flight forward event can overwrite the terminal state.
/// </remarks>
public enum TrackingStage
{
    /// <summary>No event has been projected yet for the order.</summary>
    Unknown = 0,

    /// <summary>Stage 1 — the order was placed (← <c>OrderPlaced</c>).</summary>
    OrderPlaced = 1,

    /// <summary>Stage 2 — the restaurant is preparing the order (← <c>OrderAccepted</c>; <c>OrderReady</c> stays here).</summary>
    Preparing = 2,

    /// <summary>Stage 3 — a driver is assigned and en route to the restaurant (← <c>DriverAssigned</c>).</summary>
    DriverAssigned = 3,

    /// <summary>Stage 4 — the driver picked up the order and is out for delivery (← <c>OrderPickedUp</c>).</summary>
    OutForDelivery = 4,

    /// <summary>Stage 5 — the order was delivered (← <c>OrderDelivered</c>).</summary>
    Delivered = 5,

    /// <summary>Terminal — the order was refunded/cancelled (← <c>OrderRefunded</c>).</summary>
    Refunded = 99,
}
