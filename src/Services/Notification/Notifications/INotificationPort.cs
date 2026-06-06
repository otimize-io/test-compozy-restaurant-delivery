namespace RestaurantDelivery.Notification.Notifications;

/// <summary>
/// Fire-and-forget notification seam (TechSpec "Core Interfaces" / "Integration Points → Notification").
/// <para>
/// <see cref="SendAsync"/> hands a message off for delivery and returns
/// <see cref="NotificationAccepted"/> — accepted-for-delivery, NOT delivered. The contract never
/// blocks on the underlying channel, so an outbox/retry seam or a real channel (email/SMS/push) can
/// replace the mock adapter later without changing any caller (ADR-001).
/// </para>
/// </summary>
public interface INotificationPort
{
    /// <summary>
    /// Accepts <paramref name="message"/> for delivery and returns immediately. The returned
    /// <see cref="NotificationAccepted"/> carries an id for the accepted-for-delivery hand-off; it is
    /// not a delivery receipt.
    /// </summary>
    Task<NotificationAccepted> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default);
}

/// <summary>A notification to send about an order: which order, and the rendered text.</summary>
/// <param name="OrderId">The order the notification concerns.</param>
/// <param name="CorrelationId">Trace/idempotency correlation id carried from the source event.</param>
/// <param name="Text">The rendered, human-readable notification body.</param>
public sealed record NotificationMessage(Guid OrderId, string CorrelationId, string Text);

/// <summary>
/// Acknowledges that a <see cref="NotificationMessage"/> was accepted for delivery (not delivered).
/// </summary>
/// <param name="NotificationId">Server-side id of the accepted hand-off.</param>
public sealed record NotificationAccepted(Guid NotificationId);
