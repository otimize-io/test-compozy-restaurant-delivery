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

/// <summary>
/// A single row in the restaurant order queue (<c>GET /api/restaurant/orders</c>). Carries the assigned
/// driver's name and ETA once Dispatch has matched one (null before assignment) so the restaurant can follow
/// the order through pickup and delivery.
/// </summary>
public sealed record RestaurantQueueItem(
    Guid OrderId,
    OrderStatus Status,
    decimal Total,
    string CorrelationId,
    string? DriverName = null,
    int? EtaMinutes = null);

/// <summary>
/// The restaurant order board, grouped into the columns the restaurant view renders so it can follow each
/// order end to end (PRD F5): <c>New</c> (paid, awaiting accept), <c>Cooking</c> (accepted/preparing),
/// <c>AwaitingDriver</c> (ready for pickup / a driver has been assigned and is heading over),
/// <c>OutForDelivery</c> (the driver picked it up), and <c>Delivered</c> (recently completed). Orders before
/// payment and the terminal failure/refund states are not shown.
/// </summary>
public sealed record RestaurantQueueResponse(
    IReadOnlyList<RestaurantQueueItem> New,
    IReadOnlyList<RestaurantQueueItem> Cooking,
    IReadOnlyList<RestaurantQueueItem> AwaitingDriver,
    IReadOnlyList<RestaurantQueueItem> OutForDelivery,
    IReadOnlyList<RestaurantQueueItem> Delivered);
