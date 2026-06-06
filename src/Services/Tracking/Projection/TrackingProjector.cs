using RestaurantDelivery.Contracts;

namespace RestaurantDelivery.Tracking.Projection;

/// <summary>
/// Applies order-lifecycle events to the per-order <see cref="TrackingView"/> in the
/// <see cref="ITrackingStore"/> (task_12: the 5-stage projection). Stage advancement is monotonic via
/// <see cref="TrackingStageExtensions.AdvanceTo"/>, so duplicate and out-of-order events never move the
/// bar backwards and replaying the full event stream from empty reconstructs the same view (ADR-006:
/// the Redis state is a disposable projection rebuildable from events).
/// </summary>
public sealed class TrackingProjector(ITrackingStore store, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    /// <summary>
    /// Projects a single event onto <paramref name="event"/>'s order and returns the resulting view.
    /// Events with no tracking meaning (e.g. payment/driver-internal events) leave the view unchanged;
    /// the returned view reflects the (possibly unchanged) current stage. Returns the existing view
    /// untouched when an event would not advance the stage.
    /// </summary>
    public async Task<TrackingView> ApplyAsync(IIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var orderId = @event.OrderId;
        var existing = await store.GetAsync(orderId, cancellationToken);
        var currentStage = existing?.Stage ?? TrackingStage.Unknown;

        var nextStage = currentStage.AdvanceTo(StageMapping.ToStage(@event));
        if (existing is not null && nextStage == currentStage)
        {
            // No forward movement (duplicate, out-of-order, or non-tracking event) — keep the view as-is.
            return existing;
        }

        var view = new TrackingView(orderId, nextStage, _time.GetUtcNow());
        await store.SaveAsync(view, cancellationToken);
        return view;
    }
}
