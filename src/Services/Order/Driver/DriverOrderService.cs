using MassTransit;
using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Contracts.Events;
using RestaurantDelivery.Order.Orders;
using RestaurantDelivery.Order.Saga;

namespace RestaurantDelivery.Order.Driver;

/// <summary>
/// Application service behind the driver endpoints (task_10). Pickup/deliver publish the
/// <see cref="OrderPickedUp"/>/<see cref="OrderDelivered"/> integration events the in-process saga (and
/// Tracking) consume (ADR-004) — they do not mutate the saga directly. Each command guards the order's current
/// status so an invalid transition is rejected with 409. The assignments read lists orders currently assigned
/// to a driver (from the saga instance) that have not yet been delivered.
/// </summary>
public sealed class DriverOrderService(OrderDbContext db, IPublishEndpoint publishEndpoint)
{
    /// <summary>
    /// Publishes <see cref="OrderPickedUp"/> for a <see cref="OrderStatus.DriverAssigned"/> order, advancing the
    /// saga to PickedUp. Returns <see cref="DriverTransitionResult.Conflict"/> when the order is not assigned.
    /// </summary>
    public async Task<DriverTransitionResult> PickupAsync(
        Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await LookupAsync(orderId, cancellationToken);
        if (order is null)
        {
            return DriverTransitionResult.NotFound;
        }

        if (order.Value.Status != OrderStatus.DriverAssigned)
        {
            return DriverTransitionResult.Conflict;
        }

        await publishEndpoint.Publish(new OrderPickedUp(orderId, order.Value.CorrelationId), cancellationToken);
        return DriverTransitionResult.Accepted;
    }

    /// <summary>
    /// Publishes <see cref="OrderDelivered"/> for a <see cref="OrderStatus.PickedUp"/> order, advancing the saga
    /// to the terminal Delivered state. Returns <see cref="DriverTransitionResult.Conflict"/> when the order has
    /// not been picked up.
    /// </summary>
    public async Task<DriverTransitionResult> DeliverAsync(
        Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await LookupAsync(orderId, cancellationToken);
        if (order is null)
        {
            return DriverTransitionResult.NotFound;
        }

        if (order.Value.Status != OrderStatus.PickedUp)
        {
            return DriverTransitionResult.Conflict;
        }

        await publishEndpoint.Publish(new OrderDelivered(orderId, order.Value.CorrelationId), cancellationToken);
        return DriverTransitionResult.Accepted;
    }

    /// <summary>
    /// Lists orders currently assigned to a driver and in flight (DriverAssigned or PickedUp), reading the
    /// captured driver from the saga instance. Delivered and pre-assignment orders are excluded.
    /// </summary>
    public async Task<IReadOnlyList<DriverAssignmentItem>> GetAssignmentsAsync(
        CancellationToken cancellationToken = default)
    {
        var sagas = await db.Set<OrderState>()
            .AsNoTracking()
            .Where(s => s.DriverId != null
                && (s.CurrentState == nameof(OrderStateMachine.DriverAssignedState)
                    || s.CurrentState == nameof(OrderStateMachine.PickedUp)))
            .Select(s => new
            {
                s.CorrelationId,
                s.CurrentState,
                s.DriverId,
                s.DriverName,
                s.EtaMinutes,
                s.OrderCorrelationId,
            })
            .ToListAsync(cancellationToken);

        return sagas
            .Select(s => new DriverAssignmentItem(
                s.CorrelationId,
                OrderStatusMap.FromSagaState(s.CurrentState),
                s.DriverId!.Value,
                s.DriverName ?? string.Empty,
                s.EtaMinutes ?? 0,
                s.OrderCorrelationId))
            .ToList();
    }

    private async Task<(OrderStatus Status, string CorrelationId)?> LookupAsync(
        Guid orderId, CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new { o.Status, o.CorrelationId })
            .FirstOrDefaultAsync(cancellationToken);
        if (order is null)
        {
            return null;
        }

        var sagaState = await db.Set<OrderState>()
            .AsNoTracking()
            .Where(s => s.CorrelationId == orderId)
            .Select(s => s.CurrentState)
            .FirstOrDefaultAsync(cancellationToken);

        var status = sagaState is null ? order.Status : OrderStatusMap.FromSagaState(sagaState);
        return (status, order.CorrelationId);
    }
}
