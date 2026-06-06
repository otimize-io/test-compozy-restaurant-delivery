using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace RestaurantDelivery.Order.Restaurant;

/// <summary>
/// Maps the restaurant-facing endpoints (task_08, TechSpec "API Endpoints"): accept and mark-ready, which
/// publish <c>OrderAccepted</c>/<c>OrderReady</c> to advance the saga, and the restaurant order queue grouped
/// New/In-Progress/Ready. Invalid transitions return 409; an unknown order returns 404.
/// </summary>
public static class RestaurantEndpoints
{
    public static IEndpointRouteBuilder MapRestaurantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/orders/{id:guid}/accept", async (
            Guid id,
            RestaurantOrderService restaurant,
            CancellationToken cancellationToken) =>
        {
            var result = await restaurant.AcceptAsync(id, cancellationToken);
            return ToResult(result);
        });

        endpoints.MapPost("/api/orders/{id:guid}/ready", async (
            Guid id,
            RestaurantOrderService restaurant,
            CancellationToken cancellationToken) =>
        {
            var result = await restaurant.ReadyAsync(id, cancellationToken);
            return ToResult(result);
        });

        endpoints.MapGet("/api/restaurant/orders", async (
            RestaurantOrderService restaurant,
            CancellationToken cancellationToken) =>
        {
            var queue = await restaurant.GetQueueAsync(cancellationToken);
            return Results.Ok(queue);
        });

        return endpoints;
    }

    private static IResult ToResult(RestaurantTransitionResult result) => result switch
    {
        RestaurantTransitionResult.Accepted => Results.Accepted(),
        RestaurantTransitionResult.NotFound => Results.NotFound(),
        _ => Results.Conflict(new { Error = "The order is not in a valid state for this transition." }),
    };
}
