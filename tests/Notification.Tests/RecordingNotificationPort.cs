using System.Collections.Concurrent;
using RestaurantDelivery.Notification.Notifications;

namespace Notification.Tests;

/// <summary>
/// Test double for <see cref="INotificationPort"/> that records every accepted message so a test can
/// assert exactly which notifications a consumer produced. Honors the fire-and-forget contract:
/// returns accepted-for-delivery immediately and never blocks.
/// </summary>
public sealed class RecordingNotificationPort : INotificationPort
{
    private readonly ConcurrentQueue<NotificationMessage> _sent = new();

    public IReadOnlyCollection<NotificationMessage> Sent => _sent.ToArray();

    public Task<NotificationAccepted> SendAsync(
        NotificationMessage message,
        CancellationToken cancellationToken = default)
    {
        _sent.Enqueue(message);
        return Task.FromResult(new NotificationAccepted(Guid.NewGuid()));
    }
}
