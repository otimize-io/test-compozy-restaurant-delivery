namespace RestaurantDelivery.Order.Orders;

/// <summary>
/// Body of <c>POST /api/orders</c> (TechSpec "API Endpoints"). The consumer/restaurant identities and the
/// cart are supplied by the caller (the Gateway injects the demo identity in V1). The order total is
/// computed server-side from the line subtotals — never trusted from the client.
/// </summary>
public sealed record PlaceOrderRequest(
    Guid ConsumerId,
    Guid RestaurantId,
    IReadOnlyList<PlaceOrderLine> Items,
    GeoPointDto? RestaurantLocation = null);

/// <summary>A single requested line on a new order.</summary>
public sealed record PlaceOrderLine(Guid ItemId, string Name, int Quantity, decimal UnitPrice);

/// <summary>A geographic point on the wire (mirrors <c>Contracts.GeoPoint</c>), used by the saga later.</summary>
public sealed record GeoPointDto(double Lat, double Lng);

/// <summary>Response of <c>POST /api/orders</c>: the new order id and its correlation id (ADR-004).</summary>
public sealed record PlaceOrderResponse(Guid OrderId, string CorrelationId, OrderStatus Status);

/// <summary>Response of <c>GET /api/orders/{id}</c>: the current order status snapshot.</summary>
public sealed record OrderStatusResponse(
    Guid OrderId,
    OrderStatus Status,
    decimal Total,
    string CorrelationId);

/// <summary>
/// A row in the consumer's order-tracking area (<c>GET /api/consumer/orders/{consumerId}</c>): one of the
/// consumer's orders with its live (saga-derived) status and, once a driver is assigned, the driver and ETA.
/// </summary>
public sealed record ConsumerOrderItem(
    Guid OrderId,
    OrderStatus Status,
    decimal Total,
    Guid RestaurantId,
    DateTime CreatedAt,
    string? DriverName,
    int? EtaMinutes);
