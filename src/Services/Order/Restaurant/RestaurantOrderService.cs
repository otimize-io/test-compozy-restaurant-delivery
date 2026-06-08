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
        // Flush the EF bus outbox: published from an HTTP scope (not a consumer), the event is only
        // delivered once the OrderDbContext is saved (UseBusOutbox). Without this it is silently dropped.
        await db.SaveChangesAsync(cancellationToken);
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
        await db.SaveChangesAsync(cancellationToken);
        return RestaurantTransitionResult.Accepted;
    }

    /// <summary>How many recently-delivered orders the board keeps in the Delivered column.</summary>
    private const int RecentDeliveredLimit = 10;

    /// <summary>
    /// Returns the restaurant order board grouped so the restaurant can follow each order end to end:
    /// New (Paid), Cooking (Accepted/Preparing), AwaitingDriver (ReadyForPickup or a driver has been
    /// assigned and is heading over), OutForDelivery (PickedUp), and the most recent Delivered orders.
    /// Orders before payment and the terminal failure/refund states are not shown.
    /// </summary>
    public async Task<RestaurantQueueResponse> GetQueueAsync(CancellationToken cancellationToken = default)
    {
        var live = await LiveOrdersAsync(cancellationToken);

        var newOrders = new List<LiveOrder>();
        var cooking = new List<LiveOrder>();
        var awaitingDriver = new List<LiveOrder>();
        var outForDelivery = new List<LiveOrder>();
        var delivered = new List<LiveOrder>();

        foreach (var order in live)
        {
            switch (order.Item.Status)
            {
                case OrderStatus.Paid:
                    newOrders.Add(order);
                    break;
                case OrderStatus.Accepted:
                case OrderStatus.Preparing:
                    cooking.Add(order);
                    break;
                case OrderStatus.ReadyForPickup:
                case OrderStatus.DriverAssigned:
                    awaitingDriver.Add(order);
                    break;
                case OrderStatus.PickedUp:
                    outForDelivery.Add(order);
                    break;
                case OrderStatus.Delivered:
                    delivered.Add(order);
                    break;
            }
        }

        // Active columns oldest-first (FIFO for the kitchen); Delivered shows only the most recent few.
        return new RestaurantQueueResponse(
            Oldest(newOrders),
            Oldest(cooking),
            Oldest(awaitingDriver),
            Oldest(outForDelivery),
            delivered.OrderByDescending(o => o.CreatedAt).Take(RecentDeliveredLimit)
                .Select(o => o.Item).ToList());
    }

    private static IReadOnlyList<RestaurantQueueItem> Oldest(IEnumerable<LiveOrder> orders) =>
        orders.OrderBy(o => o.CreatedAt).Select(o => o.Item).ToList();

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

    private async Task<IReadOnlyList<LiveOrder>> LiveOrdersAsync(CancellationToken cancellationToken)
    {
        // Project orders left-joined to their saga state so the live (saga-derived) status drives the grouping,
        // falling back to the persisted snapshot before the saga has recorded a state. The assigned driver
        // (name/ETA, set on DriverAssigned) rides along so the board can show who is handling the delivery.
        var rows = await db.Orders
            .AsNoTracking()
            .GroupJoin(
                db.Set<OrderState>().AsNoTracking(),
                o => o.Id,
                s => s.CorrelationId,
                (o, states) => new { Order = o, States = states })
            .SelectMany(
                x => x.States.DefaultIfEmpty(),
                (x, s) => new
                {
                    x.Order.Id,
                    x.Order.Status,
                    x.Order.Total,
                    x.Order.CorrelationId,
                    x.Order.CreatedAt,
                    SagaState = s!.CurrentState,
                    s.DriverName,
                    s.EtaMinutes,
                })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new LiveOrder(
                new RestaurantQueueItem(
                    r.Id,
                    r.SagaState is null ? r.Status : OrderStatusMap.FromSagaState(r.SagaState),
                    r.Total,
                    r.CorrelationId,
                    r.DriverName,
                    r.EtaMinutes),
                r.CreatedAt))
            .ToList();
    }

    /// <summary>An order projected for the board, with the creation time used to order/limit the columns.</summary>
    private sealed record LiveOrder(RestaurantQueueItem Item, DateTime CreatedAt);
}
