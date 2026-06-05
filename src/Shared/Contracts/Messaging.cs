namespace RestaurantDelivery.Contracts;

/// <summary>
/// Base contract for every cross-service message. Every event and command carries the order it
/// concerns and a correlation id used for tracing and idempotency (see ADR-004).
/// Namespace versioning: messages live under <c>RestaurantDelivery.Contracts</c>; a future
/// breaking change would introduce a parallel <c>.V2</c> namespace rather than mutate these.
/// </summary>
public interface IOrderMessage
{
    Guid OrderId { get; }
    string CorrelationId { get; }
}

/// <summary>Marker for facts that have already happened (published, past tense).</summary>
public interface IIntegrationEvent : IOrderMessage;

/// <summary>Marker for instructions to perform an action (sent, imperative).</summary>
public interface IIntegrationCommand : IOrderMessage;
