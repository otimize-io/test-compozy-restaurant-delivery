using MassTransit;
using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Contracts;
using RestaurantDelivery.Contracts.Events;
using RestaurantDelivery.Order.Saga;

namespace RestaurantDelivery.Order.Orders;

/// <summary>
/// Application service behind the order endpoints. On placement it computes the total, persists the order
/// aggregate, and publishes <see cref="OrderPlaced"/> (through the transactional outbox, so the row and the
/// event commit atomically — ADR-004). On read it reports the current <see cref="OrderStatus"/>, preferring
/// the live saga state and falling back to the persisted snapshot before the saga has recorded a state.
/// </summary>
public sealed class OrderService(OrderDbContext db, IPublishEndpoint publishEndpoint)
{
    /// <summary>
    /// Creates the order in <see cref="OrderStatus.Placed"/>, saves it, and publishes <c>OrderPlaced</c> to
    /// start the saga. The save and the publish share one transaction via the outbox.
    /// </summary>
    public async Task<PlaceOrderResponse> PlaceAsync(
        PlaceOrderRequest request, CancellationToken cancellationToken = default)
    {
        var orderId = Guid.NewGuid();
        var correlationId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;
        var items = request.Items
            .Select(i => new OrderLine(i.ItemId, i.Name, i.Quantity, i.UnitPrice))
            .ToList();
        var total = items.Sum(i => i.UnitPrice * i.Quantity);
        var restaurantLocation = request.RestaurantLocation is { } location
            ? new GeoPoint(location.Lat, location.Lng)
            : default;

        var order = new OrderEntity
        {
            Id = orderId,
            ConsumerId = request.ConsumerId,
            RestaurantId = request.RestaurantId,
            Total = total,
            CorrelationId = correlationId,
            Status = OrderStatus.Placed,
            CreatedAt = now,
            UpdatedAt = now,
            Items = items,
        };

        db.Orders.Add(order);
        await publishEndpoint.Publish(
            new OrderPlaced(
                orderId, correlationId, request.ConsumerId, request.RestaurantId, total, items,
                restaurantLocation),
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return new PlaceOrderResponse(orderId, correlationId, OrderStatus.Placed);
    }

    /// <summary>
    /// Returns the order's current status, or <c>null</c> when no such order exists. The live status comes
    /// from the saga's <c>CurrentState</c> (mapped via <see cref="OrderStatusMap"/>); when the saga has not
    /// yet been created the persisted <see cref="OrderEntity.Status"/> snapshot is used.
    /// </summary>
    public async Task<OrderStatusResponse?> GetStatusAsync(
        Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
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
        return new OrderStatusResponse(order.Id, status, order.Total, order.CorrelationId);
    }
}
