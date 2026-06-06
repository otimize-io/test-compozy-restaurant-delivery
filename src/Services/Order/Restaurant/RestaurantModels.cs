using RestaurantDelivery.Order.Orders;

namespace RestaurantDelivery.Order.Restaurant;

/// <summary>
/// Outcome of a restaurant transition command (accept/ready). The endpoint maps this onto an HTTP status:
/// <see cref="Accepted"/> → 202, <see cref="NotFound"/> → 404, <see cref="Conflict"/> → 409.
/// </summary>
public enum RestaurantTransitionResult
{
    /// <summary>The order does not exist.</summary>
    NotFound,

    /// <summary>The order is not in a valid prior state for this transition.</summary>
    Conflict,

    /// <summary>The transition event was published.</summary>
    Accepted,
}

/// <summary>A single row in the restaurant order queue (TechSpec <c>GET /api/restaurant/orders</c>).</summary>
public sealed record RestaurantQueueItem(Guid OrderId, OrderStatus Status, decimal Total, string CorrelationId);

/// <summary>
/// The restaurant order queue grouped by the three columns the restaurant view consumes
/// (TechSpec "API Endpoints"): <c>New</c> (paid, awaiting accept), <c>In-Progress</c> (accepted/preparing),
/// and <c>Ready</c> (ready for pickup / handed to the driver leg).
/// </summary>
public sealed record RestaurantQueueResponse(
    IReadOnlyList<RestaurantQueueItem> New,
    IReadOnlyList<RestaurantQueueItem> InProgress,
    IReadOnlyList<RestaurantQueueItem> Ready);
