using RestaurantDelivery.Contracts;
using RestaurantDelivery.Dispatch.Drivers;

namespace RestaurantDelivery.Dispatch.Matching;

/// <summary>
/// V1 mock <see cref="IDriverMatcher"/>: returns the available driver geographically nearest to the
/// restaurant, or <c>null</c> when the store reports no available drivers (the deterministic "no driver"
/// path that triggers the saga's compensation flow — task_09). Availability/location come from the
/// Redis-backed <see cref="IDriverStore"/> (ADR-006).
/// </summary>
public sealed class NearestAvailableDriverMatcher(IDriverStore store) : IDriverMatcher
{
    public async Task<DriverAssignment?> FindDriverAsync(
        Guid orderId, GeoPoint restaurant, CancellationToken cancellationToken = default)
    {
        var available = await store.GetAvailableAsync(cancellationToken);
        if (available.Count == 0)
        {
            return null;
        }

        Driver? nearest = null;
        var nearestDistance = double.PositiveInfinity;
        foreach (var driver in available)
        {
            var distance = GeoDistance.SquaredBetween(restaurant, driver.Location);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = driver;
            }
        }

        if (nearest is null)
        {
            return null;
        }

        var eta = GeoDistance.EtaMinutes(nearest.Location, restaurant);
        return new DriverAssignment(nearest.Id, nearest.Name, eta);
    }
}
