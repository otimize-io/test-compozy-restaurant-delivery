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

    /// <summary>
    /// How many times to re-publish the seeded catalog after startup (bounded). The seeder publishes once
    /// on startup, but Search may bind its <see cref="RestaurantPublished"/> queue a moment later — a publish
    /// to a topic with no bound queue is dropped by RabbitMQ, so the first events can be lost (the
    /// consumer-bind race). Re-publishing a few times lets Search index the catalog regardless of bind order;
    /// Search indexes idempotently by id, so re-publishes are harmless. Set to <c>0</c> to disable.
    /// </summary>
    public int RepublishCount { get; init; } = 6;

    /// <summary>Delay between catalog re-publishes (see <see cref="RepublishCount"/>).</summary>
    public TimeSpan RepublishInterval { get; init; } = TimeSpan.FromSeconds(10);
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

/// <summary>
/// After startup, re-publishes the seeded catalog a bounded number of times so Search indexes the
/// restaurants even when it bound its <see cref="RestaurantPublished"/> queue after the seeder's first
/// publish (the consumer-bind race — see <see cref="CatalogOptions.RepublishCount"/>). It reads the current
/// catalog from the store and re-publishes each restaurant; Search indexes idempotently by id, so this only
/// fills the index, never duplicates. Bounded: it stops after <see cref="CatalogOptions.RepublishCount"/>
/// rounds (or when seeding/re-publishing is disabled).
/// </summary>
public sealed class RestaurantRepublisher(
    IRestaurantStore store,
    IPublishEndpoint publishEndpoint,
    IOptions<CatalogOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        if (!opts.SeedRestaurants || opts.RepublishCount <= 0)
        {
            return;
        }

        for (var round = 0; round < opts.RepublishCount; round++)
        {
            try
            {
                await Task.Delay(opts.RepublishInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            var restaurants = await store.GetAllAsync(stoppingToken);
            foreach (var restaurant in restaurants)
            {
                await publishEndpoint.Publish(
                    new RestaurantPublished(
                        restaurant.Id,
                        restaurant.Name,
                        restaurant.Cuisine,
                        restaurant.Location),
                    stoppingToken);
            }
        }
    }
}
