using Microsoft.AspNetCore.Http;
using RestaurantDelivery.Catalog.Restaurants;

namespace Catalog.Tests;

/// <summary>
/// Unit tests for the read endpoints' 200/404 decision and payload (task_04 Tests). The handlers are
/// exercised directly against an in-memory store, so no web host or MongoDB is required.
/// </summary>
public class RestaurantEndpointTests
{
    private static async Task<InMemoryRestaurantStore> SeededStoreAsync()
    {
        var store = new InMemoryRestaurantStore();
        foreach (var restaurant in RestaurantSeedData.Restaurants)
        {
            await store.UpsertAsync(restaurant);
        }

        return store;
    }

    [Fact]
    public async Task Get_menu_of_seeded_restaurant_returns_its_items()
    {
        var store = await SeededStoreAsync();
        var seeded = RestaurantSeedData.Restaurants[0];

        var result = await RestaurantEndpoints.GetMenuAsync(store, seeded.Id);

        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status200OK, status.StatusCode);
        var ok = Assert.IsAssignableFrom<IValueHttpResult>(result);
        var menu = Assert.IsAssignableFrom<IReadOnlyList<MenuItem>>(ok.Value);
        Assert.Equal(seeded.Menu.Count, menu.Count);
        Assert.Equal(seeded.Menu.Select(m => m.Id), menu.Select(m => m.Id));
        Assert.All(menu, item => Assert.Equal(seeded.Id, item.RestaurantId));
    }

    [Fact]
    public async Task Get_restaurant_detail_of_seeded_restaurant_returns_200()
    {
        var store = await SeededStoreAsync();
        var seeded = RestaurantSeedData.Restaurants[1];

        var result = await RestaurantEndpoints.GetRestaurantAsync(store, seeded.Id);

        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status200OK, status.StatusCode);

        var detail = Assert.IsType<RestaurantEndpoints.RestaurantDetail>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal(seeded.Id, detail.Id);
        Assert.Equal(seeded.Name, detail.Name);
        Assert.Equal(seeded.Cuisine, detail.Cuisine);
    }

    [Fact]
    public async Task Get_restaurant_detail_with_unknown_id_returns_404()
    {
        var store = await SeededStoreAsync();

        var result = await RestaurantEndpoints.GetRestaurantAsync(store, Guid.NewGuid());

        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, status.StatusCode);
    }

    [Fact]
    public async Task Get_menu_with_unknown_id_returns_404()
    {
        var store = await SeededStoreAsync();

        var result = await RestaurantEndpoints.GetMenuAsync(store, Guid.NewGuid());

        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, status.StatusCode);
    }
}
