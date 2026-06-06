using RestaurantDelivery.Contracts;
using RestaurantDelivery.Dispatch.Drivers;
using RestaurantDelivery.Dispatch.Matching;

namespace Dispatch.Tests;

/// <summary>
/// Unit tests for the nearest-available <see cref="IDriverMatcher"/> mock (task_09 Tests). No Redis/broker:
/// the matcher is driven through an in-memory <see cref="IDriverStore"/>.
/// </summary>
public class NearestAvailableDriverMatcherTests
{
    private static readonly GeoPoint Restaurant = new(-23.561, -46.656);

    [Fact]
    public async Task FindDriverAsync_returns_the_only_available_driver()
    {
        var store = new InMemoryDriverStore();
        var driver = new Driver(Guid.NewGuid(), "Alice", new GeoPoint(-23.562, -46.657), Available: true);
        await store.UpsertAsync(driver);
        var matcher = new NearestAvailableDriverMatcher(store);

        var assignment = await matcher.FindDriverAsync(Guid.NewGuid(), Restaurant);

        Assert.NotNull(assignment);
        Assert.Equal(driver.Id, assignment!.DriverId);
        Assert.Equal("Alice", assignment.DriverName);
        Assert.True(assignment.EtaMinutes >= 0);
    }

    [Fact]
    public async Task FindDriverAsync_returns_null_when_no_drivers_are_available()
    {
        var store = new InMemoryDriverStore();
        // A driver exists but is unavailable — the deterministic "no driver" path.
        await store.UpsertAsync(new Driver(Guid.NewGuid(), "Offline", new GeoPoint(0, 0), Available: false));
        var matcher = new NearestAvailableDriverMatcher(store);

        var assignment = await matcher.FindDriverAsync(Guid.NewGuid(), Restaurant);

        Assert.Null(assignment);
    }

    [Fact]
    public async Task FindDriverAsync_returns_null_when_store_is_empty()
    {
        var matcher = new NearestAvailableDriverMatcher(new InMemoryDriverStore());

        var assignment = await matcher.FindDriverAsync(Guid.NewGuid(), Restaurant);

        Assert.Null(assignment);
    }

    [Fact]
    public async Task FindDriverAsync_selects_the_geographically_nearer_of_two_drivers()
    {
        var store = new InMemoryDriverStore();
        var near = new Driver(Guid.NewGuid(), "Near", new GeoPoint(-23.562, -46.657), Available: true);
        var far = new Driver(Guid.NewGuid(), "Far", new GeoPoint(-24.500, -47.700), Available: true);
        await store.UpsertAsync(far);
        await store.UpsertAsync(near);
        var matcher = new NearestAvailableDriverMatcher(store);

        var assignment = await matcher.FindDriverAsync(Guid.NewGuid(), Restaurant);

        Assert.NotNull(assignment);
        Assert.Equal(near.Id, assignment!.DriverId);
        Assert.Equal("Near", assignment.DriverName);
    }

    [Fact]
    public async Task FindDriverAsync_ignores_unavailable_drivers_even_when_nearer()
    {
        var store = new InMemoryDriverStore();
        // Closest driver is unavailable; the available-but-farther driver must be chosen.
        var nearUnavailable = new Driver(Guid.NewGuid(), "NearOffline", Restaurant, Available: false);
        var farAvailable = new Driver(Guid.NewGuid(), "FarOnline", new GeoPoint(-23.600, -46.700), Available: true);
        await store.UpsertAsync(nearUnavailable);
        await store.UpsertAsync(farAvailable);
        var matcher = new NearestAvailableDriverMatcher(store);

        var assignment = await matcher.FindDriverAsync(Guid.NewGuid(), Restaurant);

        Assert.NotNull(assignment);
        Assert.Equal(farAvailable.Id, assignment!.DriverId);
    }
}
