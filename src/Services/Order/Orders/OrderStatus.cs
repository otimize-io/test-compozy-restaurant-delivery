namespace RestaurantDelivery.Order.Orders;

/// <summary>
/// Order lifecycle states (TechSpec "Core Interfaces → OrderStatus"). The Order saga's
/// <c>CurrentState</c> names map onto these values for the <c>GET /api/orders/{id}</c> read
/// (see <see cref="OrderStatusMap"/>). <see cref="Faulted"/> is the terminal payment-declined
/// outcome handled in this task; <see cref="NoDriverRefunded"/> is the compensation terminal
/// state added by task_11.
/// </summary>
public enum OrderStatus
{
    Placed,
    AwaitingPayment,
    Paid,
    Accepted,
    Preparing,
    ReadyForPickup,
    DriverAssigned,
    PickedUp,
    Delivered,
    Faulted,
    NoDriverRefunded,
}
