using MassTransit;
using RestaurantDelivery.Contracts.Commands;
using RestaurantDelivery.Contracts.Events;
using RestaurantDelivery.Payment.Ports;
using RestaurantDelivery.Platform;

namespace RestaurantDelivery.Payment.Consumers;

/// <summary>
/// Consumes the saga's <see cref="CapturePayment"/> command (ADR-004), captures through the
/// <see cref="IPaymentPort"/> seam, and publishes the <see cref="PaymentAccepted"/> integration event —
/// the async-shaped acknowledgement (the terminal <c>PaymentSettled</c>/<c>PaymentDeclined</c> follows
/// later via the settlement callback). Idempotent on <c>(OrderId, CorrelationId)</c> via the Platform
/// <see cref="IIdempotencyStore"/>; the port is additionally idempotent on the command's idempotency key,
/// so a redelivered command publishes <c>PaymentAccepted</c> at most once and never double-charges.
/// </summary>
public sealed class CapturePaymentConsumer(IPaymentPort payment, IIdempotencyStore idempotency)
    : IConsumer<CapturePayment>
{
    public async Task Consume(ConsumeContext<CapturePayment> context)
    {
        var message = context.Message;
        var key = IdempotencyKey.For(message.OrderId, message.CorrelationId);

        await idempotency.RunOnceAsync(
            key,
            async () =>
            {
                await payment.CaptureAsync(
                    message.OrderId, message.Amount, message.IdempotencyKey, context.CancellationToken);

                await context.Publish(
                    new PaymentAccepted(message.OrderId, message.CorrelationId),
                    context.CancellationToken);
            },
            context.CancellationToken);
    }
}
