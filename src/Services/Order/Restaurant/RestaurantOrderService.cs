using MassTransit;
using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Contracts.Events;
using RestaurantDelivery.Order.Orders;
using RestaurantDelivery.Order.Saga;

namespace RestaurantDelivery.Order.Restaurant;

/// <summary>
/// Application service behind the restaurant endpoints (task_08). The accept/ready commands do not mutate the
/// saga directly — they publish the <see cref="OrderAccepted"/>/<see cref="OrderReady"/> integration events
/// the in-process saga (and Tracking/Dispatch) consume (ADR-004). Before publishing, each command guards the
/// order's current status so an invalid transition is rejected with 409 rather than emitting an event the
/// saga would silently ignore. The queue read groups orders by their live status into New/In-Progress/Ready.
/// </summary>
public sealed class RestaurantOrderService(OrderDbContext db, IPublishEndpoint publishEndpoint)
{
    /// <summary>
    /// Publishes <see cref="OrderAccepted"/> for a <see cref="OrderStatus.Paid"/> order, advancing the saga to
    /// Preparing. Returns <see cref="RestaurantTransitionResult.Conflict"/> when the order is not Paid.
    /// </summary>
    public async Task<RestaurantTransitionResult> AcceptAsync(
        Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await LookupAsync(orderId, cancellationToken);
        if (order is null)
        {
            return RestaurantTransitionResult.NotFound;
        }

        if (order.Value.Status != OrderStatus.Paid)
        {
            return RestaurantTransitionResult.Conflict;
        }

        await publishEndpoint.Publish(new OrderAccepted(orderId, order.Value.CorrelationId), cancellationToken);
        return RestaurantTransitionResult.Accepted;
    }

    /// <summary>
    /// Publishes <see cref="OrderReady"/> for an accepted/preparing order, advancing the saga to AwaitingDriver
    /// (ReadyForPickup). Returns <see cref="RestaurantTransitionResult.Conflict"/> when the order is not in a
    /// preparing state.
    /// </summary>
    public async Task<RestaurantTransitionResult> ReadyAsync(
        Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await LookupAsync(orderId, cancellationToken);
        if (order is null)
        {
            return RestaurantTransitionResult.NotFound;
        }

        if (order.Value.Status is not (OrderStatus.Accepted or OrderStatus.Preparing))
        {
            return RestaurantTransitionResult.Conflict;
        }

        await publishEndpoint.Publish(new OrderReady(orderId, order.Value.CorrelationId), cancellationToken);
        return RestaurantTransitionResult.Accepted;
    }

    /// <summary>
    /// Returns the restaurant order queue grouped into New (Paid), In-Progress (Accepted/Preparing), and Ready
    /// (ReadyForPickup). Orders before payment, terminal orders, and the driver/delivery leg are not shown.
    /// </summary>
    public async Task<RestaurantQueueResponse> GetQueueAsync(CancellationToken cancellationToken = default)
    {
        var live = await LiveOrdersAsync(cancellationToken);

        var newOrders = new List<RestaurantQueueItem>();
        var inProgress = new List<RestaurantQueueItem>();
        var ready = new List<RestaurantQueueItem>();

        foreach (var item in live)
        {
            switch (item.Status)
            {
                case OrderStatus.Paid:
                    newOrders.Add(item);
                    break;
                case OrderStatus.Accepted:
                case OrderStatus.Preparing:
                    inProgress.Add(item);
                    break;
                case OrderStatus.ReadyForPickup:
                    ready.Add(item);
                    break;
            }
        }

        return new RestaurantQueueResponse(newOrders, inProgress, ready);
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

    private async Task<IReadOnlyList<RestaurantQueueItem>> LiveOrdersAsync(CancellationToken cancellationToken)
    {
        // Project orders left-joined to their saga state so the live (saga-derived) status drives the grouping,
        // falling back to the persisted snapshot before the saga has recorded a state.
        var rows = await db.Orders
            .AsNoTracking()
            .GroupJoin(
                db.Set<OrderState>().AsNoTracking(),
                o => o.Id,
                s => s.CorrelationId,
                (o, states) => new { o.Id, o.Status, o.Total, o.CorrelationId, States = states })
            .SelectMany(
                x => x.States.DefaultIfEmpty(),
                (x, s) => new { x.Id, x.Status, x.Total, x.CorrelationId, SagaState = s!.CurrentState })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new RestaurantQueueItem(
                r.Id,
                r.SagaState is null ? r.Status : OrderStatusMap.FromSagaState(r.SagaState),
                r.Total,
                r.CorrelationId))
            .ToList();
    }
}
