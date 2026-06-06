using RestaurantDelivery.Search.Restaurants;

namespace Search.Tests;

/// <summary>
/// Process-local <see cref="IRestaurantIndex"/> for unit tests: lets the consumer and endpoint tests
/// run without standing up Elasticsearch. Mirrors the real index's contract — upsert by id, a blank
/// query returns everything, a name/cuisine match returns the restaurant, and a no-match query returns
/// an empty list (never an error).
/// </summary>
public sealed class InMemoryRestaurantIndex : IRestaurantIndex
{
    private readonly Dictionary<Guid, IndexedRestaurant> _restaurants = new();

    public Task IndexAsync(IndexedRestaurant restaurant, CancellationToken cancellationToken = default)
    {
        _restaurants[restaurant.Id] = restaurant;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<IndexedRestaurant>> SearchAsync(
        string? query, CancellationToken cancellationToken = default)
    {
        var term = query?.Trim();

        IReadOnlyList<IndexedRestaurant> matches = string.IsNullOrEmpty(term)
            ? _restaurants.Values.ToList()
            : _restaurants.Values
                .Where(r =>
                    r.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || r.Cuisine.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();

        return Task.FromResult(matches);
    }
}
