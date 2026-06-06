using MassTransit;
using RestaurantDelivery.Contracts.Catalog;
using RestaurantDelivery.Platform;

namespace RestaurantDelivery.Search.Restaurants;

/// <summary>
/// Consumes <see cref="RestaurantPublished"/> from Catalog and indexes the restaurant into
/// Elasticsearch (ADR-004: indexing is fed by events, never by reading Catalog's database). Idempotent
/// on the restaurant id via the Platform <see cref="IIdempotencyStore"/>: a redelivered event indexes
/// the restaurant at most once. <see cref="RestaurantPublished"/> is not order-scoped, so the key is
/// built from the restaurant id alone.
/// </summary>
public sealed class RestaurantPublishedConsumer(IRestaurantIndex index, IIdempotencyStore idempotency)
    : IConsumer<RestaurantPublished>
{
    public async Task Consume(ConsumeContext<RestaurantPublished> context)
    {
        var message = context.Message;
        var key = $"restaurant-published:{message.RestaurantId:N}";

        await idempotency.RunOnceAsync(
            key,
            () => index.IndexAsync(
                new IndexedRestaurant(
                    message.RestaurantId,
                    message.Name,
                    message.Cuisine,
                    message.Location),
                context.CancellationToken),
            context.CancellationToken);
    }
}
