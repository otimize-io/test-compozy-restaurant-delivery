using RestaurantDelivery.Catalog.Restaurants;

namespace Catalog.Tests;

/// <summary>
/// Process-local <see cref="IRestaurantStore"/> for unit tests: lets the endpoint and seeder tests run
/// without standing up MongoDB. Mirrors the real store's contract (upsert by id, null on miss).
/// </summary>
public sealed class InMemoryRestaurantStore : IRestaurantStore
{
    private readonly Dictionary<Guid, Restaurant> _restaurants = new();

    public Task UpsertAsync(Restaurant restaurant, CancellationToken cancellationToken = default)
    {
        _restaurants[restaurant.Id] = restaurant;
        return Task.CompletedTask;
    }

    public Task<Restaurant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _restaurants.TryGetValue(id, out var restaurant);
        return Task.FromResult(restaurant);
    }

    public Task<IReadOnlyList<Restaurant>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Restaurant> all = _restaurants.Values.ToList();
        return Task.FromResult(all);
    }
}
