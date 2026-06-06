using RestaurantDelivery.Contracts;

namespace RestaurantDelivery.Order.Orders;

/// <summary>
/// The order aggregate (TechSpec "Data Models → Order"; ADR-006: Order owns its PostgreSQL database).
/// This is the durable record of what was ordered, created when the consumer places an order. The
/// live lifecycle position is driven by the saga (<see cref="OrderStateMachine"/>); this row keeps a
/// denormalised <see cref="Status"/> snapshot so <c>GET /api/orders/{id}</c> can answer without the
/// saga store. Named <c>OrderEntity</c> rather than <c>Order</c> to avoid clashing with the
/// <c>RestaurantDelivery.Order</c> namespace root.
/// </summary>
public sealed class OrderEntity
{
    /// <summary>Surrogate key for the order (also the saga's correlation id partition key).</summary>
    public Guid Id { get; set; }

    /// <summary>The consumer who placed the order.</summary>
    public Guid ConsumerId { get; set; }

    /// <summary>The restaurant the order is for.</summary>
    public Guid RestaurantId { get; set; }

    /// <summary>The order total (sum of line subtotals), the amount captured by Payment.</summary>
    public decimal Total { get; set; }

    /// <summary>Correlation id propagated across every event/command for this order (ADR-004).</summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>Last-known lifecycle status; a snapshot updated as the saga advances.</summary>
    public OrderStatus Status { get; set; }

    /// <summary>When the order was placed (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>When the order row was last updated (UTC).</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>The ordered lines (owned collection, stored as JSON).</summary>
    public IReadOnlyList<OrderLine> Items { get; set; } = [];
}
