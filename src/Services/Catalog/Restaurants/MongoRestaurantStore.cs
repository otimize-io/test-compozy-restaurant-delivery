using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace RestaurantDelivery.Catalog.Restaurants;

/// <summary>
/// MongoDB-backed restaurant store (ADR-006). Each <see cref="Restaurant"/> is one document in the
/// <c>restaurants</c> collection with its menu embedded, matching the document model to the nested
/// menu/item access pattern. Decimals are persisted as MongoDB <c>Decimal128</c> for exact prices.
/// </summary>
public sealed class MongoRestaurantStore : IRestaurantStore
{
    public const string DatabaseName = "catalog";
    public const string CollectionName = "restaurants";

    private readonly IMongoCollection<Restaurant> _restaurants;

    static MongoRestaurantStore()
    {
        // Driver 3.x has no global GUID representation default, so register the standard (subtype 4)
        // representation explicitly; otherwise serializing a Guid id/key throws "Unspecified".
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        // Persist decimal prices losslessly as Decimal128 rather than the driver default.
        BsonSerializer.TryRegisterSerializer(new DecimalSerializer(BsonType.Decimal128));
    }

    public MongoRestaurantStore(IMongoClient client)
    {
        var collection = client.GetDatabase(DatabaseName).GetCollection<Restaurant>(CollectionName);
        _restaurants = collection;
    }

    public async Task UpsertAsync(Restaurant restaurant, CancellationToken cancellationToken = default)
    {
        await _restaurants.ReplaceOneAsync(
            r => r.Id == restaurant.Id,
            restaurant,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<Restaurant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cursor = await _restaurants.FindAsync(
            r => r.Id == id, cancellationToken: cancellationToken);
        return await cursor.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Restaurant>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var cursor = await _restaurants.FindAsync(
            FilterDefinition<Restaurant>.Empty, cancellationToken: cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }
}
