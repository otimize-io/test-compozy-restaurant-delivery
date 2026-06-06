using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace RestaurantDelivery.Order.Driver;

/// <summary>
/// Maps the driver-facing endpoints (task_10, TechSpec "API Endpoints"): the driver assignments read, plus
/// pickup and deliver which publish <c>OrderPickedUp</c>/<c>OrderDelivered</c> to advance the saga to the
/// terminal Delivered state. Invalid transitions return 409; an unknown order returns 404.
/// </summary>
public static class DriverEndpoints
{
    public static IEndpointRouteBuilder MapDriverEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/driver/assignments", async (
            DriverOrderService driver,
            CancellationToken cancellationToken) =>
        {
            var assignments = await driver.GetAssignmentsAsync(cancellationToken);
            return Results.Ok(assignments);
        });

        endpoints.MapPost("/api/orders/{id:guid}/pickup", async (
            Guid id,
            DriverOrderService driver,
            CancellationToken cancellationToken) =>
        {
            var result = await driver.PickupAsync(id, cancellationToken);
            return ToResult(result);
        });

        endpoints.MapPost("/api/orders/{id:guid}/deliver", async (
            Guid id,
            DriverOrderService driver,
            CancellationToken cancellationToken) =>
        {
            var result = await driver.DeliverAsync(id, cancellationToken);
            return ToResult(result);
        });

        return endpoints;
    }

    private static IResult ToResult(DriverTransitionResult result) => result switch
    {
        DriverTransitionResult.Accepted => Results.Accepted(),
        DriverTransitionResult.NotFound => Results.NotFound(),
        _ => Results.Conflict(new { Error = "The order is not in a valid state for this transition." }),
    };
}
