namespace RestaurantDelivery.Tracking.Projection;

/// <summary>
/// Persists the per-order <see cref="TrackingView"/> projection (ADR-006: Tracking uses Redis). Kept
/// behind a port so the projector and status read do not depend on Redis directly, and so tests can
/// substitute an in-memory store. The Redis state is a disposable projection that can be rebuilt purely
/// from the event stream (task_12 requirement).
/// </summary>
public interface ITrackingStore
{
    /// <summary>Returns the current view for an order, or <c>null</c> when no event has been projected yet.</summary>
    Task<TrackingView?> GetAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Adds or replaces the stored view for an order.</summary>
    Task SaveAsync(TrackingView view, CancellationToken cancellationToken = default);
}
