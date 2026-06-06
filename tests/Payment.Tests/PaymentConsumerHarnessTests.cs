using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RestaurantDelivery.Contracts.Commands;
using RestaurantDelivery.Contracts.Events;
using RestaurantDelivery.Payment.Consumers;
using RestaurantDelivery.Payment.Payments;
using RestaurantDelivery.Payment.Ports;
using RestaurantDelivery.Platform;

namespace Payment.Tests;

/// <summary>
/// Drives the <see cref="CapturePaymentConsumer"/> and <see cref="RefundPaymentConsumer"/> through
/// MassTransit's fully in-memory harness (no broker / Docker), backed by the in-memory store and the mock
/// adapter. Mirrors the harness style of <c>Dispatch.Tests.DriverRequestedConsumerHarnessTests</c>.
/// </summary>
public class PaymentConsumerHarnessTests
{
    private static async Task<(ITestHarness Harness, ServiceProvider Provider, InMemoryPaymentStore Store)> StartAsync(
        PaymentOptions? options = null)
    {
        var store = new InMemoryPaymentStore();
        var provider = new ServiceCollection()
            .AddSingleton<IPaymentStore>(store)
            .AddSingleton(Options.Create(options ?? new PaymentOptions()))
            .AddSingleton<IPaymentPort, MockPaymentAdapter>()
            .AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<CapturePaymentConsumer>();
                cfg.AddConsumer<RefundPaymentConsumer>();
            })
            .BuildServiceProvider(validateScopes: true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return (harness, provider, store);
    }

    [Fact]
    public async Task CapturePayment_publishes_PaymentAccepted_and_records_one_charge()
    {
        var (harness, provider, store) = await StartAsync();
        await using var _ = provider;
        var orderId = Guid.NewGuid();

        await harness.Bus.Publish(new CapturePayment(orderId, "corr-cap", 40m, "idem-cap"));

        Assert.True(await harness.Consumed.Any<CapturePayment>());
        Assert.True(await harness.Published.Any<PaymentAccepted>(p =>
            p.Context!.Message.OrderId == orderId && p.Context.Message.CorrelationId == "corr-cap"));
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public async Task Redelivered_CapturePayment_is_idempotent_and_charges_once()
    {
        var (harness, provider, store) = await StartAsync();
        await using var _ = provider;
        var command = new CapturePayment(Guid.NewGuid(), "corr-dup", 40m, "idem-dup");

        await harness.Bus.Publish(command);
        await harness.Bus.Publish(command);

        Assert.True(await harness.Consumed.Any<CapturePayment>());
        Assert.Equal(1, await harness.Published.SelectAsync<PaymentAccepted>().Count());
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public async Task RefundPayment_marks_the_payment_refunded()
    {
        var (harness, provider, store) = await StartAsync();
        await using var _ = provider;
        var orderId = Guid.NewGuid();

        await harness.Bus.Publish(new CapturePayment(orderId, "corr-r", 40m, "idem-r"));
        Assert.True(await harness.Published.Any<PaymentAccepted>());

        await harness.Bus.Publish(new RefundPayment(orderId, "corr-r-refund"));
        Assert.True(await harness.Consumed.Any<RefundPayment>());

        // The refund is applied to the recorded charge.
        Assert.Equal(PaymentStatus.Refunded, (await store.FindByOrderIdAsync(orderId))!.Status);
    }
}
