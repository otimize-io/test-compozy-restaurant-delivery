namespace RestaurantDelivery.Contracts.Commands;

// Integration commands — imperative instructions the saga sends to a service. Each carries
// OrderId + CorrelationId via IIntegrationCommand. See ADR-004.

/// <summary>Capture payment for an order. Idempotent on <see cref="IdempotencyKey"/>.</summary>
public sealed record CapturePayment(
    Guid OrderId,
    string CorrelationId,
    decimal Amount,
    string IdempotencyKey) : IIntegrationCommand;

/// <summary>Refund a previously captured payment (compensation path).</summary>
public sealed record RefundPayment(Guid OrderId, string CorrelationId) : IIntegrationCommand;

/// <summary>Ask dispatch to find and assign a driver for an order.</summary>
public sealed record RequestDriver(
    Guid OrderId,
    string CorrelationId,
    GeoPoint RestaurantLocation) : IIntegrationCommand;
