namespace RestaurantDelivery.Gateway.Realtime;

/// <summary>
/// The real-time payload the SignalR hub pushes to clients (ADR-007, task_14.3). It mirrors the 5-stage
/// tracking model the Tracking service projects, but is computed locally by the gateway from the order
/// lifecycle event type — it is NOT a cross-service contract message (the gateway never adds a wire message
/// for the status push). Clients render the 5-stage tracking bar from <see cref="Stage"/>/<see cref="StageName"/>.
/// </summary>
/// <param name="OrderId">The order whose status changed.</param>
/// <param name="Stage">The numeric stage (1..5, or the refunded terminal value), matching Tracking's mapping.</param>
/// <param name="StageName">The stage's display name (e.g. <c>Preparing</c>).</param>
public sealed record OrderStatusChanged(Guid OrderId, int Stage, string StageName);
