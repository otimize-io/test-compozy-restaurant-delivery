using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using RestaurantDelivery.Contracts;
using RestaurantDelivery.Contracts.Commands;
using RestaurantDelivery.Contracts.Events;
using RestaurantDelivery.Order.Orders;
using RestaurantDelivery.Order.Saga;

namespace Order.Tests;

/// <summary>
/// Drives the <see cref="OrderStateMachine"/> through MassTransit's fully in-memory saga test harness (no
/// broker / Docker), using the default in-memory saga repository. Asserts the happy-path transitions, the
/// outbound <see cref="CapturePayment"/> command and <see cref="DriverRequested"/> event, the
/// payment-declined fault, and idempotency of a redelivered settlement. Mirrors the harness style of
/// <c>Payment.Tests.PaymentConsumerHarnessTests</c>.
/// </summary>
public class OrderStateMachineHarnessTests
{
    private static readonly GeoPoint Restaurant = new(-23.561, -46.656);

    private static IReadOnlyList<OrderLine> Cart() =>
        [new OrderLine(Guid.NewGuid(), "Margherita", 2, 30m)];

    private static async Task<(ITestHarness Harness, ISagaStateMachineTestHarness<OrderStateMachine, OrderState> Saga, ServiceProvider Provider)> StartAsync()
    {
        var provider = new ServiceCollection()
            .AddMassTransitTestHarness(cfg =>
                cfg.AddSagaStateMachine<OrderStateMachine, OrderState>())
            .BuildServiceProvider(validateScopes: true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        var saga = harness.GetSagaStateMachineHarness<OrderStateMachine, OrderState>();
        return (harness, saga, provider);
    }

    private static OrderPlaced PlacedFor(Guid orderId, string correlationId = "corr") =>
        new(orderId, correlationId, Guid.NewGuid(), Guid.NewGuid(), 60m, Cart());

    [Fact]
    public async Task OrderPlaced_sends_CapturePayment_and_reaches_AwaitingPayment()
    {
        var (harness, saga, provider) = await StartAsync();
        await using var _ = provider;
        var orderId = Guid.NewGuid();

        await harness.Bus.Publish(PlacedFor(orderId, "corr-place"));

        Assert.True(await saga.Created.Any(s => s.CorrelationId == orderId));
        var instanceId = await saga.Exists(orderId, x => x.AwaitingPayment);
        Assert.NotNull(instanceId);

        Assert.True(await harness.Published.Any<CapturePayment>(c =>
            c.Context!.Message.OrderId == orderId
            && c.Context.Message.CorrelationId == "corr-place"
            && c.Context.Message.Amount == 60m
            && c.Context.Message.IdempotencyKey == orderId.ToString("N")));
    }

    [Fact]
    public async Task PaymentSettled_transitions_to_Paid_and_issues_no_driver_request()
    {
        var (harness, saga, provider) = await StartAsync();
        await using var _ = provider;
        var orderId = Guid.NewGuid();

        await harness.Bus.Publish(PlacedFor(orderId));
        Assert.NotNull(await saga.Exists(orderId, x => x.AwaitingPayment));

        await harness.Bus.Publish(new PaymentSettled(orderId, "corr"));

        Assert.NotNull(await saga.Exists(orderId, x => x.Paid));
        // No driver is requested yet — that only happens at OrderReady.
        Assert.False(await harness.Published.Any<DriverRequested>());
    }

    [Fact]
    public async Task Duplicate_PaymentSettled_for_same_order_is_ignored()
    {
        var (harness, saga, provider) = await StartAsync();
        await using var _ = provider;
        var orderId = Guid.NewGuid();

        await harness.Bus.Publish(PlacedFor(orderId));
        Assert.NotNull(await saga.Exists(orderId, x => x.AwaitingPayment));

        await harness.Bus.Publish(new PaymentSettled(orderId, "corr"));
        Assert.NotNull(await saga.Exists(orderId, x => x.Paid));

        // A redelivered settlement finds the saga already in Paid (not AwaitingPayment), so the
        // PaymentSettled transition is not accepted again — the instance stays in Paid.
        await harness.Bus.Publish(new PaymentSettled(orderId, "corr"));
        Assert.True(await harness.Consumed.SelectAsync<PaymentSettled>().Count() >= 2);
        Assert.NotNull(await saga.Exists(orderId, x => x.Paid));
    }

    [Fact]
    public async Task PaymentDeclined_transitions_to_Faulted()
    {
        var (harness, saga, provider) = await StartAsync();
        await using var _ = provider;
        var orderId = Guid.NewGuid();

        await harness.Bus.Publish(PlacedFor(orderId));
        Assert.NotNull(await saga.Exists(orderId, x => x.AwaitingPayment));

        await harness.Bus.Publish(new PaymentDeclined(orderId, "corr", "insufficient funds"));

        Assert.NotNull(await saga.Exists(orderId, x => x.Faulted));
    }

    [Fact]
    public async Task OrderReady_publishes_DriverRequested_and_reaches_AwaitingDriver()
    {
        var (harness, saga, provider) = await StartAsync();
        await using var _ = provider;
        var orderId = Guid.NewGuid();

        await harness.Bus.Publish(PlacedFor(orderId, "corr-ready"));
        Assert.NotNull(await saga.Exists(orderId, x => x.AwaitingPayment));
        await harness.Bus.Publish(new PaymentSettled(orderId, "corr-ready"));
        Assert.NotNull(await saga.Exists(orderId, x => x.Paid));
        await harness.Bus.Publish(new OrderAccepted(orderId, "corr-ready"));
        Assert.NotNull(await saga.Exists(orderId, x => x.Preparing));

        await harness.Bus.Publish(new OrderReady(orderId, "corr-ready"));

        Assert.NotNull(await saga.Exists(orderId, x => x.AwaitingDriver));
        Assert.True(await harness.Published.Any<DriverRequested>(c =>
            c.Context!.Message.OrderId == orderId && c.Context.Message.CorrelationId == "corr-ready"));
    }

    [Fact]
    public async Task Full_happy_path_drives_the_saga_to_Delivered()
    {
        var (harness, saga, provider) = await StartAsync();
        await using var _ = provider;
        var orderId = Guid.NewGuid();

        await harness.Bus.Publish(PlacedFor(orderId, "corr-e2e"));
        Assert.NotNull(await saga.Exists(orderId, x => x.AwaitingPayment));

        await harness.Bus.Publish(new PaymentSettled(orderId, "corr-e2e"));
        Assert.NotNull(await saga.Exists(orderId, x => x.Paid));

        await harness.Bus.Publish(new OrderAccepted(orderId, "corr-e2e"));
        Assert.NotNull(await saga.Exists(orderId, x => x.Preparing));

        await harness.Bus.Publish(new OrderReady(orderId, "corr-e2e"));
        Assert.NotNull(await saga.Exists(orderId, x => x.AwaitingDriver));

        var driverId = Guid.NewGuid();
        await harness.Bus.Publish(new DriverAssigned(orderId, "corr-e2e", driverId, "Alice", 12));
        Assert.NotNull(await saga.Exists(orderId, x => x.DriverAssignedState));

        await harness.Bus.Publish(new OrderPickedUp(orderId, "corr-e2e"));
        Assert.NotNull(await saga.Exists(orderId, x => x.PickedUp));

        await harness.Bus.Publish(new OrderDelivered(orderId, "corr-e2e"));
        var delivered = await saga.Exists(orderId, x => x.Delivered);
        Assert.NotNull(delivered);

        // The driver assignment was captured on the instance.
        var instance = saga.Sagas.Contains(orderId);
        Assert.NotNull(instance);
        Assert.Equal(driverId, instance!.DriverId);
        Assert.Equal("Alice", instance.DriverName);
        Assert.Equal(12, instance.EtaMinutes);
    }
}
