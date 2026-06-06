using MassTransit;
using RestaurantDelivery.Contracts.Events;
using RestaurantDelivery.Platform;

namespace RestaurantDelivery.Dispatch.Matching;

/// <summary>
/// Consumes <see cref="DriverRequested"/> and publishes either <see cref="DriverAssigned"/> (the nearest
/// available driver was found) or <see cref="DriverUnavailable"/> (none — the compensation trigger), per
/// ADR-004. Idempotent on <c>(OrderId, CorrelationId)</c> via the Platform <see cref="IIdempotencyStore"/>:
/// a redelivered request publishes the assignment outcome at most once.
/// </summary>
public sealed class DriverRequestedConsumer(IDriverMatcher matcher, IIdempotencyStore idempotency)
    : IConsumer<DriverRequested>
{
    public async Task Consume(ConsumeContext<DriverRequested> context)
    {
        var message = context.Message;
        var key = IdempotencyKey.For(message.OrderId, message.CorrelationId);

        await idempotency.RunOnceAsync(
            key,
            async () =>
            {
                var assignment = await matcher.FindDriverAsync(
                    message.OrderId, message.RestaurantLocation, context.CancellationToken);

                if (assignment is null)
                {
                    await context.Publish(
                        new DriverUnavailable(message.OrderId, message.CorrelationId),
                        context.CancellationToken);
                    return;
                }

                await context.Publish(
                    new DriverAssigned(
                        message.OrderId,
                        message.CorrelationId,
                        assignment.DriverId,
                        assignment.DriverName,
                        assignment.EtaMinutes),
                    context.CancellationToken);
            },
            context.CancellationToken);
    }
}
