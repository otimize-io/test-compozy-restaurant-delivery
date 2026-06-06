using RestaurantDelivery.Contracts;

namespace RestaurantDelivery.Tracking.Projection;

/// <summary>
/// Maps each order-lifecycle integration event to the <see cref="TrackingStage"/> it represents
/// (task_12 requirement; PRD F8). This is the single source of truth for the event→stage projection
/// rule, kept as a pure function so it can be unit-tested and replayed deterministically.
/// </summary>
/// <remarks>
/// Mapping (by the message's CLR type name, so it works for any <see cref="IIntegrationEvent"/>):
/// <list type="bullet">
/// <item><c>OrderPlaced</c> → <see cref="TrackingStage.OrderPlaced"/> (stage 1).</item>
/// <item><c>PaymentSettled</c> → stage 1 (payment is a sub-step of "order placed"; it does not advance the bar).</item>
/// <item><c>OrderAccepted</c> → <see cref="TrackingStage.Preparing"/> (stage 2).</item>
/// <item><c>OrderReady</c> → stage 2 (a sub-step of preparing; kept at stage 2 by design).</item>
/// <item><c>DriverAssigned</c> → <see cref="TrackingStage.DriverAssigned"/> (stage 3).</item>
/// <item><c>OrderPickedUp</c> → <see cref="TrackingStage.OutForDelivery"/> (stage 4).</item>
/// <item><c>OrderDelivered</c> → <see cref="TrackingStage.Delivered"/> (stage 5).</item>
/// <item><c>OrderRefunded</c> → <see cref="TrackingStage.Refunded"/> (terminal).</item>
/// </list>
/// Any event with no tracking meaning maps to <see cref="TrackingStage.Unknown"/> and is ignored by the
/// projector. Stage advancement itself is enforced by <see cref="TrackingStageExtensions.AdvanceTo"/>,
/// not here, so duplicates/out-of-order events never move the bar backwards.
/// </remarks>
public static class StageMapping
{
    /// <summary>Returns the stage an event represents, or <see cref="TrackingStage.Unknown"/> when it carries no tracking meaning.</summary>
    public static TrackingStage ToStage(IIntegrationEvent @event) => @event switch
    {
        Contracts.Events.OrderPlaced => TrackingStage.OrderPlaced,
        Contracts.Events.PaymentSettled => TrackingStage.OrderPlaced,
        Contracts.Events.OrderAccepted => TrackingStage.Preparing,
        Contracts.Events.OrderReady => TrackingStage.Preparing,
        Contracts.Events.DriverAssigned => TrackingStage.DriverAssigned,
        Contracts.Events.OrderPickedUp => TrackingStage.OutForDelivery,
        Contracts.Events.OrderDelivered => TrackingStage.Delivered,
        Contracts.Events.OrderRefunded => TrackingStage.Refunded,
        _ => TrackingStage.Unknown,
    };
}

/// <summary>Stage-advancement rules shared by the projector and store.</summary>
public static class TrackingStageExtensions
{
    /// <summary>
    /// Returns the later of <paramref name="current"/> and <paramref name="next"/> so the projection only
    /// ever moves forward. The <see cref="TrackingStage.Refunded"/> terminal stage outranks every forward
    /// stage, so once an order is refunded a late forward event cannot overwrite it. An
    /// <see cref="TrackingStage.Unknown"/> next stage leaves <paramref name="current"/> unchanged.
    /// </summary>
    public static TrackingStage AdvanceTo(this TrackingStage current, TrackingStage next)
    {
        if (next == TrackingStage.Unknown)
        {
            return current;
        }

        return next > current ? next : current;
    }
}
