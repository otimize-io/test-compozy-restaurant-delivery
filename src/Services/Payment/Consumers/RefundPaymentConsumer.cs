using MassTransit;
using RestaurantDelivery.Contracts.Commands;
using RestaurantDelivery.Payment.Ports;
using RestaurantDelivery.Platform;

namespace RestaurantDelivery.Payment.Consumers;

/// <summary>
/// Consumes the saga's <see cref="RefundPayment"/> compensation command (ADR-004) and performs the refund
/// through the <see cref="IPaymentPort"/> seam, recording the payment as refunded. Payment does NOT emit
/// <c>OrderRefunded</c> — that terminal event belongs to the Order saga; this consumer only carries out the
/// refund and records it. Idempotent on <c>(OrderId, CorrelationId)</c> via the Platform
/// <see cref="IIdempotencyStore"/> so a redelivered refund command is applied once.
/// </summary>
public sealed class RefundPaymentConsumer(IPaymentPort payment, IIdempotencyStore idempotency)
    : IConsumer<RefundPayment>
{
    public async Task Consume(ConsumeContext<RefundPayment> context)
    {
        var message = context.Message;
        var key = IdempotencyKey.For(message.OrderId, message.CorrelationId);

        await idempotency.RunOnceAsync(
            key,
            () => payment.RefundAsync(message.OrderId, message.CorrelationId, context.CancellationToken),
            context.CancellationToken);
    }
}
