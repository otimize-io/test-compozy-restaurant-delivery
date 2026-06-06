using Microsoft.Extensions.Options;
using RestaurantDelivery.Dispatch.Drivers;

namespace Dispatch.Tests;

public class DriverSeederTests
{
    [Fact]
    public async Task Seeds_available_drivers_when_seeding_enabled()
    {
        var store = new InMemoryDriverStore();
        var seeder = new DriverSeeder(store, Options.Create(new DispatchOptions { SeedDrivers = true }));

        await seeder.StartAsync(CancellationToken.None);

        var available = await store.GetAvailableAsync();
        Assert.Equal(3, available.Count); // DriverSeedData ships 4: 3 available, 1 offline
    }

    [Fact]
    public async Task Seeds_nothing_when_seeding_disabled()
    {
        var store = new InMemoryDriverStore();
        var seeder = new DriverSeeder(store, Options.Create(new DispatchOptions { SeedDrivers = false }));

        await seeder.StartAsync(CancellationToken.None);
        await seeder.StopAsync(CancellationToken.None);

        Assert.Empty(await store.GetAvailableAsync());
    }
}
