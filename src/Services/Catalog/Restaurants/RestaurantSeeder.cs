using MassTransit;
using Microsoft.Extensions.Options;
using RestaurantDelivery.Contracts.Catalog;

namespace RestaurantDelivery.Catalog.Restaurants;

/// <summary>Options controlling restaurant seeding (bound from the <c>Catalog</c> configuration section).</summary>
public sealed class CatalogOptions
{
    public const string SectionName = "Catalog";

    /// <summary>
    /// When <c>true</c> (default), seed mock restaurants/menus into MongoDB at startup and publish one
    /// <see cref="RestaurantPublished"/> per restaurant so Search can index them (task_05). Set to
    /// <c>false</c> to leave the store empty (e.g. when pointing at an already-populated database).
    /// </summary>
    public bool SeedRestaurants { get; init; } = true;
}

/// <summary>
/// Seeds <see cref="RestaurantSeedData.Restaurants"/> into the <see cref="IRestaurantStore"/> at startup
/// when <see cref="CatalogOptions.SeedRestaurants"/> is enabled (ADR-006: seeded data), then publishes
/// one <see cref="RestaurantPublished"/> per restaurant so Search indexes it (ADR-004, task_05).
/// </summary>
public sealed class RestaurantSeeder(
    IRestaurantStore store,
    IPublishEndpoint publishEndpoint,
    IOptions<CatalogOptions> options) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.SeedRestaurants)
        {
            return;
        }

        foreach (var restaurant in RestaurantSeedData.Restaurants)
        {
            await store.UpsertAsync(restaurant, cancellationToken);

            await publishEndpoint.Publish(
                new RestaurantPublished(
                    restaurant.Id,
                    restaurant.Name,
                    restaurant.Cuisine,
                    restaurant.Location),
                cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
