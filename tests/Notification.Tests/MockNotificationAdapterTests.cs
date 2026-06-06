using Microsoft.Extensions.Logging.Abstractions;
using RestaurantDelivery.Notification.Notifications;

namespace Notification.Tests;

/// <summary>
/// Unit tests for the fire-and-forget mock adapter: it acknowledges accepted-for-delivery with an id
/// and never blocks on delivery.
/// </summary>
public class MockNotificationAdapterTests
{
    private static MockNotificationAdapter CreateAdapter() =>
        new(NullLogger<MockNotificationAdapter>.Instance);

    [Fact]
    public async Task SendAsync_returns_NotificationAccepted_with_an_id()
    {
        var adapter = CreateAdapter();
        var message = new NotificationMessage(Guid.NewGuid(), "corr-1", "Your order is ready.");

        var accepted = await adapter.SendAsync(message);

        Assert.NotNull(accepted);
        Assert.NotEqual(Guid.Empty, accepted.NotificationId);
    }

    [Fact]
    public async Task SendAsync_does_not_block_and_completes_synchronously()
    {
        var adapter = CreateAdapter();
        var message = new NotificationMessage(Guid.NewGuid(), "corr-2", "Your order has been placed.");

        // Fire-and-forget: the returned task is already completed — no awaiting on a real channel.
        var task = adapter.SendAsync(message);

        Assert.True(task.IsCompleted);
        await task;
    }

    [Fact]
    public async Task SendAsync_returns_a_distinct_id_per_call()
    {
        var adapter = CreateAdapter();
        var message = new NotificationMessage(Guid.NewGuid(), "corr-3", "Delivered.");

        var first = await adapter.SendAsync(message);
        var second = await adapter.SendAsync(message);

        Assert.NotEqual(first.NotificationId, second.NotificationId);
    }
}
