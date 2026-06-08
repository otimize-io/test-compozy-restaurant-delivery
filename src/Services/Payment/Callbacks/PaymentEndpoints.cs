using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using RestaurantDelivery.Payment.Ports;

namespace RestaurantDelivery.Payment.Callbacks;

/// <summary>Maps the Payment service's HTTP endpoints (the mock settlement webhook).</summary>
public static class PaymentEndpoints
{
    /// <summary>
    /// Maps <c>POST /api/payments/callback</c> (TechSpec API table): the mock settlement webhook that
    /// resolves an accepted payment into <c>PaymentSettled</c> / <c>PaymentDeclined</c>. Returns 202 on a
    /// resolved settlement, 404 when no payment exists for the order. It briefly waits for an in-flight
    /// capture to land (the SPA settles right after placing the order — see
    /// <see cref="SettlementService.SettleWaitingForCaptureAsync"/>) so the race does not surface as a 404.
    /// </summary>
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/payments/callback", async (
            SettlementCallbackRequest request,
            SettlementService settlement,
            IOptions<PaymentOptions> options,
            CancellationToken cancellationToken) =>
        {
            var opts = options.Value;
            var result = await settlement.SettleWaitingForCaptureAsync(
                request, opts.CaptureWaitTimeout, opts.CaptureWaitInterval, cancellationToken);
            return result == SettlementResult.NotFound
                ? Results.NotFound()
                : Results.Accepted(value: new { request.OrderId, Outcome = result.ToString() });
        });

        return endpoints;
    }
}
