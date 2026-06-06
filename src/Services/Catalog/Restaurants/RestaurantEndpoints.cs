using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RestaurantDelivery.Contracts;

namespace RestaurantDelivery.Catalog.Restaurants;

/// <summary>
/// Maps Catalog's read endpoints (TechSpec "API Endpoints"): restaurant detail and menu (PRD F2).
/// Reads only — happy path only, no create/update/delete (YAGNI for the mocked PoC). The HTTP handlers
/// delegate to <see cref="GetRestaurantAsync"/> / <see cref="GetMenuAsync"/> so the 200/404 decision is
/// unit-testable without standing up the web host.
/// </summary>
public static class RestaurantEndpoints
{
    public static IEndpointRouteBuilder MapRestaurantEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/restaurants/{id} — restaurant detail; 404 when the id is unknown.
        app.MapGet("/api/restaurants/{id:guid}", (
            Guid id, IRestaurantStore store, CancellationToken cancellationToken) =>
            GetRestaurantAsync(store, id, cancellationToken));

        // GET /api/restaurants/{id}/menu — the restaurant's menu items; 404 when the id is unknown.
        app.MapGet("/api/restaurants/{id:guid}/menu", (
            Guid id, IRestaurantStore store, CancellationToken cancellationToken) =>
            GetMenuAsync(store, id, cancellationToken));

        return app;
    }

    /// <summary>Returns 200 with the restaurant detail, or 404 when the id is unknown.</summary>
    public static async Task<IResult> GetRestaurantAsync(
        IRestaurantStore store, Guid id, CancellationToken cancellationToken = default)
    {
        var restaurant = await store.GetByIdAsync(id, cancellationToken);
        return restaurant is null
            ? Results.NotFound()
            : Results.Ok(ToDetail(restaurant));
    }

    /// <summary>Returns 200 with the restaurant's menu items, or 404 when the id is unknown.</summary>
    public static async Task<IResult> GetMenuAsync(
        IRestaurantStore store, Guid id, CancellationToken cancellationToken = default)
    {
        var restaurant = await store.GetByIdAsync(id, cancellationToken);
        return restaurant is null
            ? Results.NotFound()
            : Results.Ok(restaurant.Menu);
    }

    private static RestaurantDetail ToDetail(Restaurant restaurant) =>
        new(restaurant.Id, restaurant.Name, restaurant.Cuisine, restaurant.Location);

    /// <summary>Restaurant-detail read model (excludes the menu, which has its own endpoint).</summary>
    public sealed record RestaurantDetail(
        Guid Id,
        string Name,
        string Cuisine,
        GeoPoint Location);
}
