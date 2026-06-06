using System.Collections.Concurrent;
using RestaurantDelivery.Tracking.Projection;

namespace Tracking.Tests;

/// <summary>
/// Process-local <see cref="ITrackingStore"/> for unit tests: lets the projector tests assert the stored
/// view without standing up Redis. Mirrors the real store's get/save contract.
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
        _views[view.OrderId] = view;
        return Task.CompletedTask;
    }
}
