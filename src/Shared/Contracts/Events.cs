namespace RestaurantDelivery.Contracts.Events;

// Integration events — past-tense facts published over the broker. Each carries OrderId +
// CorrelationId via IIntegrationEvent. See TechSpec "Data Models → Integration events" and ADR-004.

public sealed record OrderPlaced(
    Guid OrderId,
    string CorrelationId,
    Guid ConsumerId,
    Guid RestaurantId,
    decimal Total,
    IReadOnlyList<OrderLine> Items) : IIntegrationEvent;

public sealed record PaymentAccepted(Guid OrderId, string CorrelationId) : IIntegrationEvent;

public sealed record PaymentSettled(Guid OrderId, string CorrelationId) : IIntegrationEvent;

public sealed record PaymentDeclined(Guid OrderId, string CorrelationId, string Reason) : IIntegrationEvent;

public sealed record OrderAccepted(Guid OrderId, string CorrelationId) : IIntegrationEvent;

public sealed record OrderReady(Guid OrderId, string CorrelationId) : IIntegrationEvent;

public sealed record DriverRequested(Guid OrderId, string CorrelationId, GeoPoint RestaurantLocation) : IIntegrationEvent;

public sealed record DriverAssigned(
    Guid OrderId,
    string CorrelationId,
    Guid DriverId,
    string DriverName,
    int EtaMinutes) : IIntegrationEvent;

public sealed record DriverUnavailable(Guid OrderId, string CorrelationId) : IIntegrationEvent;

public sealed record OrderPickedUp(Guid OrderId, string CorrelationId) : IIntegrationEvent;

public sealed record OrderDelivered(Guid OrderId, string CorrelationId) : IIntegrationEvent;

public sealed record OrderRefunded(Guid OrderId, string CorrelationId) : IIntegrationEvent;
