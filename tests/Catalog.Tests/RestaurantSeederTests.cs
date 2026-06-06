using MassTransit;
using Microsoft.Extensions.Options;
using RestaurantDelivery.Catalog.Restaurants;
using RestaurantDelivery.Contracts.Catalog;

namespace Catalog.Tests;

/// <summary>
/// Unit tests for the startup seeder (task_04 Tests): seeding produces the expected restaurant count on a
/// fresh store, publishes one <see cref="RestaurantPublished"/> per restaurant for Search, and is a no-op
/// when disabled. A recording publish endpoint stands in for the broker.
/// </summary>
public class RestaurantSeederTests
{
    private static RestaurantSeeder CreateSeeder(
        IRestaurantStore store, IPublishEndpoint publishEndpoint, bool seed) =>
        new(store, publishEndpoint, Options.Create(new CatalogOptions { SeedRestaurants = seed }));

    [Fact]
    public async Task Seeding_produces_the_expected_restaurant_count()
    {
        var store = new InMemoryRestaurantStore();
        var publish = new RecordingPublishEndpoint();
        var seeder = CreateSeeder(store, publish, seed: true);

        await seeder.StartAsync(CancellationToken.None);

        var all = await store.GetAllAsync();
        Assert.Equal(RestaurantSeedData.Restaurants.Count, all.Count);
        Assert.Equal(3, all.Count); // RestaurantSeedData ships 3 restaurants
    }

    [Fact]
    public async Task Seeding_publishes_one_RestaurantPublished_per_restaurant()
    {
        var store = new InMemoryRestaurantStore();
        var publish = new RecordingPublishEndpoint();
        var seeder = CreateSeeder(store, publish, seed: true);

        await seeder.StartAsync(CancellationToken.None);

        var published = publish.Published.OfType<RestaurantPublished>().ToList();
        Assert.Equal(RestaurantSeedData.Restaurants.Count, published.Count);
        Assert.Equal(
            RestaurantSeedData.Restaurants.Select(r => r.Id).OrderBy(id => id),
            published.Select(p => p.RestaurantId).OrderBy(id => id));

        // The published event carries the catalog fields Search needs to index.
        var first = RestaurantSeedData.Restaurants[0];
        var match = Assert.Single(published, p => p.RestaurantId == first.Id);
        Assert.Equal(first.Name, match.Name);
        Assert.Equal(first.Cuisine, match.Cuisine);
        Assert.Equal(first.Location, match.Location);
    }

    [Fact]
    public async Task Seeds_nothing_and_publishes_nothing_when_seeding_disabled()
    {
        var store = new InMemoryRestaurantStore();
        var publish = new RecordingPublishEndpoint();
        var seeder = CreateSeeder(store, publish, seed: false);

        await seeder.StartAsync(CancellationToken.None);
        await seeder.StopAsync(CancellationToken.None);

        Assert.Empty(await store.GetAllAsync());
        Assert.Empty(publish.Published);
    }
}
