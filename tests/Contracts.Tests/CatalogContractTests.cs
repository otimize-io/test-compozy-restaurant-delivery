using System.Text.Json;
using RestaurantDelivery.Contracts;
using RestaurantDelivery.Contracts.Catalog;

namespace Contracts.Tests;

public class CatalogContractTests
{
    [Fact]
    public void RestaurantPublished_round_trips_through_json_without_field_loss()
    {
        var original = new RestaurantPublished(
            Guid.NewGuid(), "Pizza Place", "Italian", new GeoPoint(-23.55, -46.63));

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<RestaurantPublished>(json)!;

        Assert.Equal(original, restored);
    }

    [Fact]
    public void RestaurantPublished_is_not_an_order_message()
    {
        // Catalog events are not order-scoped, so they must not implement IOrderMessage.
        Assert.False(typeof(IOrderMessage).IsAssignableFrom(typeof(RestaurantPublished)));
    }
}
