using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RestaurantDelivery.Contracts;

namespace RestaurantDelivery.Search.Restaurants;

/// <summary>
/// Maps Search's discovery endpoint (TechSpec "API Endpoints": <c>GET /api/restaurants?q=</c>, PRD F1).
/// Searches restaurants by name and/or cuisine. A no-match query returns an empty array with HTTP 200
/// (never an error), per task_05. The HTTP handler delegates to <see cref="SearchAsync"/> so the
/// always-200 behaviour is unit-testable without standing up the web host or Elasticsearch.
/// </summary>
public static class SearchEndpoints
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/restaurants?q=<term> — discovery by name/cuisine; always 200 with a (possibly empty) array.
        app.MapGet("/api/restaurants", (
            string? q, IRestaurantIndex index, CancellationToken cancellationToken) =>
            SearchAsync(index, q, cancellationToken));

        return app;
    }

    /// <summary>
    /// Returns 200 with the matching restaurants as a (possibly empty) array. Never returns an error
    /// for a no-match query — the empty result set is the no-match answer (task_05).
    /// </summary>
    public static async Task<IResult> SearchAsync(
        IRestaurantIndex index, string? q, CancellationToken cancellationToken = default)
    {
        var matches = await index.SearchAsync(q, cancellationToken);
        var results = matches
            .Select(r => new RestaurantSearchResult(r.Id, r.Name, r.Cuisine, r.Location))
            .ToArray();
        return Results.Ok(results);
    }

    /// <summary>Discovery read model returned by the search endpoint.</summary>
    public sealed record RestaurantSearchResult(
        Guid Id,
        string Name,
        string Cuisine,
        GeoPoint Location);
}
