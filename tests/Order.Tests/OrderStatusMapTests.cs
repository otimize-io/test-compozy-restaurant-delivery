using RestaurantDelivery.Order.Orders;
using RestaurantDelivery.Order.Saga;

namespace Order.Tests;

/// <summary>
/// Unit tests for <see cref="OrderStatusMap"/>: every saga state name maps to the documented
/// <see cref="OrderStatus"/>, and unknown/null states fall back to <see cref="OrderStatus.Placed"/>.
/// </summary>
public class OrderStatusMapTests
{
    [Theory]
    [InlineData(nameof(OrderStateMachine.AwaitingPayment), OrderStatus.AwaitingPayment)]
    [InlineData(nameof(OrderStateMachine.Paid), OrderStatus.Paid)]
    [InlineData(nameof(OrderStateMachine.Faulted), OrderStatus.Faulted)]
    [InlineData(nameof(OrderStateMachine.Preparing), OrderStatus.Preparing)]
    [InlineData(nameof(OrderStateMachine.AwaitingDriver), OrderStatus.ReadyForPickup)]
    [InlineData(nameof(OrderStateMachine.DriverAssignedState), OrderStatus.DriverAssigned)]
    [InlineData(nameof(OrderStateMachine.PickedUp), OrderStatus.PickedUp)]
    [InlineData(nameof(OrderStateMachine.Delivered), OrderStatus.Delivered)]
    public void Maps_each_saga_state_name_to_its_status(string state, OrderStatus expected)
    {
        Assert.Equal(expected, OrderStatusMap.FromSagaState(state));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Initial")]
    [InlineData("Unknown")]
    public void Unknown_or_null_states_fall_back_to_Placed(string? state)
    {
        Assert.Equal(OrderStatus.Placed, OrderStatusMap.FromSagaState(state));
    }
}
