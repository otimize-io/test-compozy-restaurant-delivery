using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace RestaurantDelivery.Payment.Callbacks;

/// <summary>Maps the Payment service's HTTP endpoints (the mock settlement webhook).</summary>
public static class PaymentEndpoints
{
    /// <summary>
    /// Maps <c>POST /api/payments/callback</c> (TechSpec API table): the mock settlement webhook that
    /// resolves an accepted payment into <c>PaymentSettled</c> / <c>PaymentDeclined</c>. Returns 202 on a
    /// resolved settlement, 404 when no payment exists for the order.
    /// </summary>
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/payments/callback", async (
            SettlementCallbackRequest request,
            SettlementService settlement,
            CancellationToken cancellationToken) =>
        {
            var result = await settlement.SettleAsync(request, cancellationToken);
            return result == SettlementResult.NotFound
                ? Results.NotFound()
                : Results.Accepted(value: new { request.OrderId, Outcome = result.ToString() });
        });

        return endpoints;
    }
}
