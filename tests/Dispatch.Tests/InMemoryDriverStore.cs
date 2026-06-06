using RestaurantDelivery.Dispatch.Drivers;

namespace Dispatch.Tests;

/// <summary>
/// Process-local <see cref="IDriverStore"/> for unit tests: lets the matcher tests control exactly which
/// drivers are available without standing up Redis. Mirrors the real store's "only available drivers are
/// returned" contract.
/// </summary>
public sealed class InMemoryDriverStore : IDriverStore
{
    private readonly Dictionary<Guid, Driver> _drivers = new();

    public Task UpsertAsync(Driver driver, CancellationToken cancellationToken = default)
    {
        _drivers[driver.Id] = driver;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Driver>> GetAvailableAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Driver> available = _drivers.Values.Where(d => d.Available).ToList();
        return Task.FromResult(available);
    }
}
