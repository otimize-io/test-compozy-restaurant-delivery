using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using RestaurantDelivery.Contracts;
using RestaurantDelivery.Contracts.Catalog;
using RestaurantDelivery.Platform;
using RestaurantDelivery.Search.Restaurants;

namespace Search.Tests;

/// <summary>
/// Drives <see cref="RestaurantPublishedConsumer"/> through MassTransit's fully in-memory test harness
/// (no broker / Docker), backed by an in-memory <see cref="IRestaurantIndex"/>. Mirrors the harness
/// style of the other services. Asserts that consuming a <see cref="RestaurantPublished"/> indexes the
/// restaurant (so it becomes searchable) and that a redelivered event indexes it at most once.
/// </summary>
public class RestaurantPublishedConsumerHarnessTests
{
    private static readonly GeoPoint Location = new(-23.561, -46.656);

    private static async Task<(ITestHarness Harness, ServiceProvider Provider)> StartHarnessAsync()
    {
        var provider = new ServiceCollection()
            .AddSingleton<IRestaurantIndex, InMemoryRestaurantIndex>()
            .AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>()
            .AddMassTransitTestHarness(cfg => cfg.AddConsumer<RestaurantPublishedConsumer>())
            .BuildServiceProvider(validateScopes: true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return (harness, provider);
    }

    [Fact]
    public async Task Consuming_RestaurantPublished_indexes_the_restaurant_and_it_is_searchable()
    {
        var (harness, provider) = await StartHarnessAsync();
        await using var _ = provider;

        var restaurantId = Guid.NewGuid();
        await harness.Bus.Publish(
            new RestaurantPublished(restaurantId, "Burger Barn", "American", Location));

        Assert.True(await harness.Consumed.Any<RestaurantPublished>());

        var index = provider.GetRequiredService<IRestaurantIndex>();
        var byName = await index.SearchAsync("Burger");
        var match = Assert.Single(byName);
        Assert.Equal(restaurantId, match.Id);

        var byCuisine = await index.SearchAsync("American");
        Assert.Equal(restaurantId, Assert.Single(byCuisine).Id);
    }

    [Fact]
    public async Task Redelivered_RestaurantPublished_is_idempotent_and_indexes_once()
    {
        var (harness, provider) = await StartHarnessAsync();
        await using var _ = provider;

        var message = new RestaurantPublished(Guid.NewGuid(), "Sakura Sushi", "Japanese", Location);
        await harness.Bus.Publish(message);
        await harness.Bus.Publish(message);

        Assert.True(await harness.Consumed.Any<RestaurantPublished>());

        // Same restaurant id → the idempotency store suppresses the duplicate; one document remains.
        var index = provider.GetRequiredService<IRestaurantIndex>();
        Assert.Single(await index.SearchAsync("Sakura"));
    }
}
