using RestaurantDelivery.Order.Saga;

namespace RestaurantDelivery.Order.Orders;

/// <summary>
/// Maps the saga's persisted <c>CurrentState</c> name (<see cref="OrderStateMachine"/>) onto an
/// <see cref="OrderStatus"/> for the <c>GET /api/orders/{id}</c> read. The two vocabularies differ in a
/// couple of names (the state machine uses <c>AwaitingDriver</c>/<c>DriverAssignedState</c>; the status
/// enum uses <c>ReadyForPickup</c>/<c>DriverAssigned</c>), so the mapping is explicit. An order that has
/// a row but whose saga has not yet recorded a state reads as <see cref="OrderStatus.Placed"/>.
/// </summary>
public static class OrderStatusMap
{
    public static OrderStatus FromSagaState(string? currentState) => currentState switch
    {
        nameof(OrderStateMachine.AwaitingPayment) => OrderStatus.AwaitingPayment,
        nameof(OrderStateMachine.Paid) => OrderStatus.Paid,
        nameof(OrderStateMachine.Faulted) => OrderStatus.Faulted,
        nameof(OrderStateMachine.Preparing) => OrderStatus.Preparing,
        nameof(OrderStateMachine.AwaitingDriver) => OrderStatus.ReadyForPickup,
        nameof(OrderStateMachine.DriverAssignedState) => OrderStatus.DriverAssigned,
        nameof(OrderStateMachine.PickedUp) => OrderStatus.PickedUp,
        nameof(OrderStateMachine.Delivered) => OrderStatus.Delivered,
        nameof(OrderStateMachine.NoDriverRefunded) => OrderStatus.NoDriverRefunded,
        _ => OrderStatus.Placed,
    };
}
