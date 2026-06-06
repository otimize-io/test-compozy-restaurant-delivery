using Microsoft.Extensions.Logging;

namespace RestaurantDelivery.Notification.Notifications;

/// <summary>
/// V1 mock adapter for <see cref="INotificationPort"/>. Logs the message and returns
/// accepted-for-delivery without contacting any real channel — fire-and-forget by contract
/// (ADR-001, ADR-006: stateless, no datastore). A real email/SMS/push adapter can replace this
/// later without changing the event consumers.
/// </summary>
public sealed class MockNotificationAdapter(ILogger<MockNotificationAdapter> logger) : INotificationPort
{
    public Task<NotificationAccepted> SendAsync(
        NotificationMessage message,
        CancellationToken cancellationToken = default)
    {
        var accepted = new NotificationAccepted(Guid.NewGuid());

        logger.LogInformation(
            "Notification {NotificationId} accepted for order {OrderId} (correlation {CorrelationId}): {Text}",
            accepted.NotificationId,
            message.OrderId,
            message.CorrelationId,
            message.Text);

        return Task.FromResult(accepted);
    }
}
