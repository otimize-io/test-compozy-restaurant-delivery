extern alias OrderApp;

using OrderStatusEnum = OrderApp::RestaurantDelivery.Order.Orders.OrderStatus;

namespace E2E.Tests;

/// <summary>
/// Resolves an <c>OrderStatus</c> name to the numeric value the Order service serializes it as. The Order
/// endpoints return the enum with the default numeric JSON conversion, so the E2E compares by ordinal. Using
/// the real enum (via the OrderApp alias) keeps the mapping in lock-step with the service — if a status is
/// renamed/reordered, this fails to compile rather than drifting.
/// </summary>
public static class OrderStatusValues
{
    public static int Of(string statusName) =>
        (int)Enum.Parse<OrderStatusEnum>(statusName, ignoreCase: true);
}
