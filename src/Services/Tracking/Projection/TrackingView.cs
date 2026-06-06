namespace RestaurantDelivery.Tracking.Projection;

/// <summary>
/// The projected current status of one order (TechSpec "Data Models → TrackingView"): the order id, its
/// current <see cref="TrackingStage"/>, and when the stage last changed. Stored in Redis keyed by
/// <c>OrderId</c> and rebuildable purely from the event stream (ADR-006: a disposable projection).
/// </summary>
/// <param name="OrderId">The order this view tracks.</param>
/// <param name="Stage">The current stage (1..5, or the refunded terminal stage).</param>
/// <param name="UpdatedAt">When the stage last advanced (UTC).</param>
public sealed record TrackingView(Guid OrderId, TrackingStage Stage, DateTimeOffset UpdatedAt);
