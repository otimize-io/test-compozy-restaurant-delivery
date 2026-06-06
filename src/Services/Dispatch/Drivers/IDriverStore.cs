namespace RestaurantDelivery.Dispatch.Drivers;

/// <summary>
/// Persists driver availability/location for nearest-available matching (ADR-006: Dispatch uses Redis).
/// Kept behind a port so the matcher and seeder do not depend on Redis directly, and so tests can
/// substitute an in-memory store.
/// </summary>
public interface IDriverStore
{
    /// <summary>Adds or replaces a driver record.</summary>
    Task UpsertAsync(Driver driver, CancellationToken cancellationToken = default);

    /// <summary>Returns every currently available driver. Empty when none are available.</summary>
    Task<IReadOnlyList<Driver>> GetAvailableAsync(CancellationToken cancellationToken = default);
}
