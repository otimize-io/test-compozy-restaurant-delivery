using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using RestaurantDelivery.Gateway.Hubs;

namespace E2E.Tests.Gateway;

/// <summary>
/// Unit tests for the SignalR hub (task_14.3): a subscriber is placed into the correct per-order group, so the
/// gateway's status fan-out reaches exactly the clients tracking that order.
/// </summary>
public class OrdersHubTests
{
    [Fact]
    public async Task Subscribe_adds_the_connection_to_the_per_order_group()
    {
        var orderId = Guid.NewGuid();
        var groups = new RecordingGroupManager();
        var hub = new OrdersHub
        {
            Context = new FakeHubCallerContext("conn-1"),
            Groups = groups,
        };

        await hub.Subscribe(orderId);

        Assert.Equal(("conn-1", OrdersHub.GroupFor(orderId)), Assert.Single(groups.Added));
        Assert.Equal($"order-{orderId:N}", OrdersHub.GroupFor(orderId));
    }

    [Fact]
    public async Task Unsubscribe_removes_the_connection_from_the_per_order_group()
    {
        var orderId = Guid.NewGuid();
        var groups = new RecordingGroupManager();
        var hub = new OrdersHub
        {
            Context = new FakeHubCallerContext("conn-2"),
            Groups = groups,
        };

        await hub.Unsubscribe(orderId);

        Assert.Equal(("conn-2", OrdersHub.GroupFor(orderId)), Assert.Single(groups.Removed));
    }

    [Fact]
    public void Group_names_are_deterministic_and_distinct_per_order()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        Assert.Equal(OrdersHub.GroupFor(a), OrdersHub.GroupFor(a));
        Assert.NotEqual(OrdersHub.GroupFor(a), OrdersHub.GroupFor(b));
    }

    private sealed class RecordingGroupManager : IGroupManager
    {
        public List<(string Connection, string Group)> Added { get; } = [];
        public List<(string Connection, string Group)> Removed { get; } = [];

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Added.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Removed.Add((connectionId, groupName));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHubCallerContext(string connectionId) : HubCallerContext
    {
        public override string ConnectionId { get; } = connectionId;
        public override string? UserIdentifier => null;
        public override System.Security.Claims.ClaimsPrincipal? User => null;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() { }
    }
}
