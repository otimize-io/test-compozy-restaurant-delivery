namespace RestaurantDelivery.Search.Restaurants;

/// <summary>
/// Persists and searches the restaurant discovery index (ADR-006: Search uses Elasticsearch). Kept
/// behind a port so the consumer and endpoint do not depend on the Elasticsearch client directly, and
/// so tests can substitute an in-memory index without standing up a real cluster (ADR-001). The index
/// is populated only from the Catalog <c>RestaurantPublished</c> event (ADR-004).
/// </summary>
public interface IRestaurantIndex
{
    /// <summary>Adds or replaces a restaurant in the index (keyed by <see cref="IndexedRestaurant.Id"/>).</summary>
    Task IndexAsync(IndexedRestaurant restaurant, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the restaurants whose name or cuisine matches <paramref name="query"/>. A blank query
    /// matches everything; a query that matches nothing returns an empty list (never an error), so the
    /// endpoint can answer the no-match case with an empty array and HTTP 200 (task_05).
    /// </summary>
    Task<IReadOnlyList<IndexedRestaurant>> SearchAsync(
        string? query, CancellationToken cancellationToken = default);
}
