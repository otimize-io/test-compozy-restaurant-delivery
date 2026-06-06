using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RestaurantDelivery.Tracking.Projection;

namespace RestaurantDelivery.Tracking.Status;

/// <summary>The current-status read shape, returned by the status endpoint and used on (re)connect to resync.</summary>
/// <param name="OrderId">The order this status describes.</param>
/// <param name="Stage">The current stage number (1..5, or the refunded terminal value).</param>
/// <param name="StageName">The stage's name (e.g. <c>Preparing</c>), for display.</param>
/// <param name="UpdatedAt">When the stage last advanced (UTC).</param>
public sealed record OrderStatusResponse(Guid OrderId, int Stage, string StageName, DateTimeOffset UpdatedAt);

/// <summary>Maps the Tracking service's HTTP read endpoint (TechSpec "API Endpoints").</summary>
public static class StatusEndpoints
{
    /// <summary>
    /// Maps <c>GET /api/orders/{id}/status</c> (task_12.3): returns the current projected
    /// <see cref="OrderStatusResponse"/> for an order (200), or 404 when no event has been projected yet.
    /// The gateway/SignalR client calls this on (re)connect to resync the live tracking bar (ADR-007).
    /// </summary>
    public static IEndpointRouteBuilder MapStatusEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/orders/{id:guid}/status", async (
            Guid id,
            ITrackingStore store,
            CancellationToken cancellationToken) =>
        {
            var view = await store.GetAsync(id, cancellationToken);
            return view is null
                ? Results.NotFound()
                : Results.Ok(new OrderStatusResponse(
                    view.OrderId, (int)view.Stage, view.Stage.ToString(), view.UpdatedAt));
        });

        return endpoints;
    }
}
