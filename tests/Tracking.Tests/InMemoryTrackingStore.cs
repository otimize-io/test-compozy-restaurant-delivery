using System.Collections.Concurrent;
using RestaurantDelivery.Tracking.Projection;

namespace Tracking.Tests;

/// <summary>
/// Process-local <see cref="ITrackingStore"/> for unit tests. Mirrors the real
/// <c>RedisTrackingStore</c> contract — including its <b>atomic, monotonic</b> save (only advance to a
/// strictly greater stage). This matters because the harness can deliver same-order events concurrently;
/// a naive last-write-wins double would race (e.g. OrderRefunded then OrderPlaced leaving stage 1),
/// whereas production's Lua compare-and-set keeps the maximum stage.
/// </summary>
public sealed class InMemoryTrackingStore : ITrackingStore
{
    private readonly ConcurrentDictionary<Guid, TrackingView> _views = new();

    public Task<TrackingView?> GetAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        _views.TryGetValue(orderId, out var view);
        return Task.FromResult(view);
    }

    public Task SaveAsync(TrackingView view, CancellationToken cancellationToken = default)
    {
        // Atomic monotonic upsert: keep whichever view has the greater stage (matches the Redis CAS).
        _views.AddOrUpdate(view.OrderId, view, (_, existing) => view.Stage > existing.Stage ? view : existing);
        return Task.CompletedTask;
    }
}
