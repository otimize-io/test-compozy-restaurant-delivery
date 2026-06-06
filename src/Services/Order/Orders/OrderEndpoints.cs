using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace RestaurantDelivery.Order.Orders;

/// <summary>
/// Maps the Order service's HTTP endpoints (TechSpec "API Endpoints"): order placement and status read.
/// The restaurant (accept/ready), driver (pickup/deliver), and payment-initiation endpoints are added by
/// later tasks; this task owns placement and the status read only.
/// </summary>
public static class OrderEndpoints
{
    /// <summary>
    /// Maps <c>POST /api/orders</c> (create the order, start the saga via <c>OrderPlaced</c> — returns 201
    /// with the new order id + correlation id) and <c>GET /api/orders/{id}</c> (current status, 200 or 404).
    /// </summary>
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/orders", async (
            PlaceOrderRequest request,
            OrderService orders,
            CancellationToken cancellationToken) =>
        {
            if (request.Items is null || request.Items.Count == 0)
            {
                return Results.BadRequest(new { Error = "An order must contain at least one item." });
            }

            if (request.Items.Any(i => i.Quantity <= 0))
            {
                return Results.BadRequest(new { Error = "Every item must have a positive quantity." });
            }

            var response = await orders.PlaceAsync(request, cancellationToken);
            return Results.Created($"/api/orders/{response.OrderId}", response);
        });

        endpoints.MapGet("/api/orders/{id:guid}", async (
            Guid id,
            OrderService orders,
            CancellationToken cancellationToken) =>
        {
            var status = await orders.GetStatusAsync(id, cancellationToken);
            return status is null ? Results.NotFound() : Results.Ok(status);
        });

        return endpoints;
    }
}
