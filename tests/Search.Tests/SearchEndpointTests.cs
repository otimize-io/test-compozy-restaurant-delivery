using Microsoft.AspNetCore.Http;
using RestaurantDelivery.Contracts;
using RestaurantDelivery.Search.Restaurants;

namespace Search.Tests;

/// <summary>
/// Unit tests for the discovery endpoint's always-200 behaviour and payload (task_05 Tests). The
/// handler is exercised directly against an in-memory index, so no web host or Elasticsearch is
/// required: a query matching a seeded name returns it, a cuisine query returns all of that cuisine,
/// and a no-match query returns an empty array with HTTP 200 (never an error).
/// </summary>
public class SearchEndpointTests
{
    private static readonly IndexedRestaurant Burger =
        new(Guid.NewGuid(), "Burger Barn", "American", new GeoPoint(-23.561, -46.656));
    private static readonly IndexedRestaurant Sushi =
        new(Guid.NewGuid(), "Sakura Sushi", "Japanese", new GeoPoint(-23.600, -46.700));
    private static readonly IndexedRestaurant Ramen =
        new(Guid.NewGuid(), "Ramen House", "Japanese", new GeoPoint(-23.500, -46.600));

    private static async Task<InMemoryRestaurantIndex> SeededIndexAsync()
    {
        var index = new InMemoryRestaurantIndex();
        await index.IndexAsync(Burger);
        await index.IndexAsync(Sushi);
        await index.IndexAsync(Ramen);
        return index;
    }

    private static IReadOnlyList<SearchEndpoints.RestaurantSearchResult> Results(IResult result)
    {
        var ok = Assert.IsAssignableFrom<IValueHttpResult>(result);
        return Assert.IsAssignableFrom<IReadOnlyList<SearchEndpoints.RestaurantSearchResult>>(ok.Value);
    }

    private static void AssertOk(IResult result)
    {
        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status200OK, status.StatusCode);
    }

    [Fact]
    public async Task Query_matching_a_seeded_name_returns_that_restaurant()
    {
        var index = await SeededIndexAsync();

        var result = await SearchEndpoints.SearchAsync(index, "Burger");

        AssertOk(result);
        var results = Results(result);
        var match = Assert.Single(results);
        Assert.Equal(Burger.Id, match.Id);
        Assert.Equal("Burger Barn", match.Name);
    }

    [Fact]
    public async Task Query_by_cuisine_returns_all_restaurants_of_that_cuisine()
    {
        var index = await SeededIndexAsync();

        var result = await SearchEndpoints.SearchAsync(index, "Japanese");

        AssertOk(result);
        var results = Results(result);
        Assert.Equal(2, results.Count);
        Assert.Equal(
            new[] { Ramen.Id, Sushi.Id }.OrderBy(id => id),
            results.Select(r => r.Id).OrderBy(id => id));
        Assert.All(results, r => Assert.Equal("Japanese", r.Cuisine));
    }

    [Fact]
    public async Task Query_with_no_matches_returns_an_empty_array_and_200()
    {
        var index = await SeededIndexAsync();

        var result = await SearchEndpoints.SearchAsync(index, "Vegan");

        AssertOk(result);
        Assert.Empty(Results(result));
    }

    [Fact]
    public async Task Blank_query_browses_all_indexed_restaurants_with_200()
    {
        var index = await SeededIndexAsync();

        var result = await SearchEndpoints.SearchAsync(index, q: null);

        AssertOk(result);
        Assert.Equal(3, Results(result).Count);
    }
}
