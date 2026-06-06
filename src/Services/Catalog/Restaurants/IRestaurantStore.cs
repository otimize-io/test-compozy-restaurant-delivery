namespace RestaurantDelivery.Catalog.Restaurants;

/// <summary>
/// Persists restaurants and their embedded menus (ADR-006: Catalog uses MongoDB). Kept behind a port so
/// the seeder and endpoints do not depend on MongoDB directly, and so tests can substitute an in-memory
/// store without standing up a database.
/// </summary>
public interface IRestaurantStore
{
    /// <summary>Adds or replaces a restaurant document (keyed by <see cref="Restaurant.Id"/>).</summary>
    Task UpsertAsync(Restaurant restaurant, CancellationToken cancellationToken = default);

    /// <summary>Returns the restaurant with the given id, or <c>null</c> when it does not exist.</summary>
    Task<Restaurant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns every stored restaurant. Empty when none exist.</summary>
    Task<IReadOnlyList<Restaurant>> GetAllAsync(CancellationToken cancellationToken = default);
}
