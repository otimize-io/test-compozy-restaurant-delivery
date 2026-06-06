using RestaurantDelivery.Order.Orders;

namespace RestaurantDelivery.Order.Driver;

/// <summary>
/// Outcome of a driver transition command (pickup/deliver). The endpoint maps this onto an HTTP status:
/// <see cref="Accepted"/> → 202, <see cref="NotFound"/> → 404, <see cref="Conflict"/> → 409.
/// </summary>
public enum DriverTransitionResult
{
    /// <summary>The order does not exist.</summary>
    NotFound,

    /// <summary>The order is not in a valid prior state for this transition.</summary>
    Conflict,

    /// <summary>The transition event was published.</summary>
    Accepted,
}

/// <summary>
/// A driver assignment row (TechSpec <c>GET /api/driver/assignments</c>): an order currently assigned to a
/// driver and not yet delivered. The driver details come from the saga instance captured on
/// <c>DriverAssigned</c>.
/// </summary>
public sealed record DriverAssignmentItem(
    Guid OrderId,
    OrderStatus Status,
    Guid DriverId,
    string DriverName,
    int EtaMinutes,
    string CorrelationId);
