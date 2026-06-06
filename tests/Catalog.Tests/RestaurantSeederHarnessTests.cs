using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RestaurantDelivery.Catalog.Restaurants;
using RestaurantDelivery.Contracts.Catalog;

namespace Catalog.Tests;

/// <summary>
/// Integration (task_04 Tests): on startup the seeder publishes a <see cref="RestaurantPublished"/> per
/// restaurant, observable on the broker. Driven through MassTransit's fully in-memory test harness (no
/// RabbitMQ / Docker), mirroring the harness style of the other services.
/// </summary>
[Trait("Category", "Integration")]
public class RestaurantSeederHarnessTests
{
    private static async Task<(ITestHarness Harness, ServiceProvider Provider)> StartHarnessAsync()
    {
        var provider = new ServiceCollection()
            .AddMassTransitTestHarness()
            .BuildServiceProvider(validateScopes: true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return (harness, provider);
    }

    [Fact]
    public async Task Seeding_publishes_RestaurantPublished_observable_on_the_broker()
    {
        var (harness, provider) = await StartHarnessAsync();
        await using var _ = provider;

        var store = new InMemoryRestaurantStore();
        var seeder = new RestaurantSeeder(
            store,
            harness.Bus,
            Options.Create(new CatalogOptions { SeedRestaurants = true }));

        await seeder.StartAsync(CancellationToken.None);

        Assert.True(await harness.Published.Any<RestaurantPublished>());

        var publishedIds = new List<Guid>();
        foreach (var element in harness.Published.Select<RestaurantPublished>())
        {
            publishedIds.Add(element.Context!.Message.RestaurantId);
        }

        Assert.Equal(RestaurantSeedData.Restaurants.Count, publishedIds.Count);
        Assert.Equal(
            RestaurantSeedData.Restaurants.Select(r => r.Id).OrderBy(id => id),
            publishedIds.OrderBy(id => id));
    }
}
