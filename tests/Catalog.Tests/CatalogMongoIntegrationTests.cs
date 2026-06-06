using MongoDB.Driver;
using RestaurantDelivery.Catalog.Restaurants;
using RestaurantDelivery.Contracts;
using Testcontainers.MongoDb;

namespace Catalog.Tests;

/// <summary>
/// Integration (task_04 Tests): a real MongoDB (Testcontainers, image <c>mongo:7</c>) backs the store.
/// Asserts the store writes and reads a restaurant document (round-tripping the embedded menu, GeoPoint,
/// and decimal prices) and that an unknown id reads back as null. Requires Docker.
/// </summary>
[Trait("Category", "Integration")]
public class CatalogMongoIntegrationTests : IAsyncLifetime
{
    private readonly MongoDbContainer _mongo = new MongoDbBuilder("mongo:7").Build();
    private IMongoClient _client = null!;

    public async Task InitializeAsync()
    {
        await _mongo.StartAsync();
        _client = new MongoClient(_mongo.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await _mongo.DisposeAsync();
    }

    [Fact]
    public async Task Writes_and_reads_a_restaurant_document_from_MongoDB()
    {
        var store = new MongoRestaurantStore(_client);
        var id = Guid.NewGuid();
        var restaurant = new Restaurant(
            id,
            "Test Diner",
            "Fusion",
            new GeoPoint(-23.55, -46.63),
            [
                new MenuItem(Guid.NewGuid(), id, "Special", "Chef's special", 49.90m),
                new MenuItem(Guid.NewGuid(), id, "Side", "A side dish", 9.50m),
            ]);

        await store.UpsertAsync(restaurant);

        var read = await store.GetByIdAsync(id);
        Assert.NotNull(read);
        Assert.Equal(restaurant.Name, read!.Name);
        Assert.Equal(restaurant.Cuisine, read.Cuisine);
        Assert.Equal(restaurant.Location, read.Location);
        Assert.Equal(2, read.Menu.Count);
        Assert.Equal(restaurant.Menu[0].Id, read.Menu[0].Id);
        Assert.Equal(49.90m, read.Menu[0].Price); // decimal persisted exactly (Decimal128)
        Assert.Equal(id, read.Menu[0].RestaurantId);
    }

    [Fact]
    public async Task Upsert_replaces_an_existing_restaurant_and_unknown_id_reads_null()
    {
        var store = new MongoRestaurantStore(_client);
        var id = Guid.NewGuid();

        await store.UpsertAsync(new Restaurant(id, "Before", "A", new GeoPoint(0, 0), []));
        await store.UpsertAsync(new Restaurant(id, "After", "B", new GeoPoint(1, 1), []));

        var read = await store.GetByIdAsync(id);
        Assert.NotNull(read);
        Assert.Equal("After", read!.Name);

        Assert.Null(await store.GetByIdAsync(Guid.NewGuid()));
    }
}
