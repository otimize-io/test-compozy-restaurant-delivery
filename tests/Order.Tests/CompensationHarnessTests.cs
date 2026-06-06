using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using RestaurantDelivery.Contracts;
using RestaurantDelivery.Contracts.Commands;
using RestaurantDelivery.Contracts.Events;
using RestaurantDelivery.Order.Saga;

namespace Order.Tests;

/// <summary>
/// Compensation tests for the <see cref="OrderStateMachine"/> (task_11, PRD F9). Using MassTransit's fully
/// in-memory saga harness, drives a paid order to <c>AwaitingDriver</c> and injects <c>DriverUnavailable</c>,
/// asserting the saga issues exactly one <see cref="RefundPayment"/> command and reaches the terminal
/// <c>NoDriverRefunded</c> state — with no "paid but undelivered" orphan — and that a redelivered
/// <c>DriverUnavailable</c> does not issue a second refund (idempotent).
/// </summary>
public class CompensationHarnessTests
{
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

    private static OrderPlaced PlacedFor(Guid orderId, string correlationId) =>
        new(orderId, correlationId, Guid.NewGuid(), Guid.NewGuid(), 60m, Cart());

    private static async Task DriveToAwaitingDriverAsync(
        ITestHarness harness,
        ISagaStateMachineTestHarness<OrderStateMachine, OrderState> saga,
        Guid orderId,
        string correlationId)
    {
        await harness.Bus.Publish(PlacedFor(orderId, correlationId));
        Assert.NotNull(await saga.Exists(orderId, x => x.AwaitingPayment));
        await harness.Bus.Publish(new PaymentSettled(orderId, correlationId));
        Assert.NotNull(await saga.Exists(orderId, x => x.Paid));
        await harness.Bus.Publish(new OrderAccepted(orderId, correlationId));
        Assert.NotNull(await saga.Exists(orderId, x => x.Preparing));
        await harness.Bus.Publish(new OrderReady(orderId, correlationId));
        Assert.NotNull(await saga.Exists(orderId, x => x.AwaitingDriver));
    }

    [Fact]
    public async Task DriverUnavailable_in_AwaitingDriver_issues_one_RefundPayment_and_reaches_NoDriverRefunded()
    {
        var (harness, saga, provider) = await StartAsync();
        await using var _ = provider;
        var orderId = Guid.NewGuid();
        const string correlationId = "corr-comp";

        await DriveToAwaitingDriverAsync(harness, saga, orderId, correlationId);

        await harness.Bus.Publish(new DriverUnavailable(orderId, correlationId));

        // Reaches the terminal compensation state.
        Assert.NotNull(await saga.Exists(orderId, x => x.NoDriverRefunded));

        // Exactly one RefundPayment command was published, carrying the order + business correlation id.
        Assert.True(await harness.Published.Any<RefundPayment>(c =>
            c.Context!.Message.OrderId == orderId
            && c.Context.Message.CorrelationId == correlationId));
        Assert.Equal(1, await harness.Published.SelectAsync<RefundPayment>().Count());

        // And OrderRefunded is announced once so Tracking/Notification/the consumer reflect the refund.
        Assert.True(await harness.Published.Any<OrderRefunded>(c =>
            c.Context!.Message.OrderId == orderId
            && c.Context.Message.CorrelationId == correlationId));
        Assert.Equal(1, await harness.Published.SelectAsync<OrderRefunded>().Count());
    }

    [Fact]
    public async Task Duplicate_DriverUnavailable_does_not_issue_a_second_refund()
    {
        var (harness, saga, provider) = await StartAsync();
        await using var _ = provider;
        var orderId = Guid.NewGuid();
        const string correlationId = "corr-comp-dup";

        await DriveToAwaitingDriverAsync(harness, saga, orderId, correlationId);

        await harness.Bus.Publish(new DriverUnavailable(orderId, correlationId));
        Assert.NotNull(await saga.Exists(orderId, x => x.NoDriverRefunded));

        // A redelivered DriverUnavailable finds the saga already in NoDriverRefunded (not AwaitingDriver), so
        // OnUnhandledEvent ignores it: the instance stays terminal and no second refund is issued.
        await harness.Bus.Publish(new DriverUnavailable(orderId, correlationId));
        Assert.True(await harness.Consumed.SelectAsync<DriverUnavailable>().Count() >= 2);
        Assert.NotNull(await saga.Exists(orderId, x => x.NoDriverRefunded));
        Assert.Equal(1, await harness.Published.SelectAsync<RefundPayment>().Count());
    }

    [Fact]
    public async Task DriverUnavailable_before_AwaitingDriver_does_not_compensate()
    {
        var (harness, saga, provider) = await StartAsync();
        await using var _ = provider;
        var orderId = Guid.NewGuid();
        const string correlationId = "corr-comp-early";

        await harness.Bus.Publish(PlacedFor(orderId, correlationId));
        Assert.NotNull(await saga.Exists(orderId, x => x.AwaitingPayment));
        await harness.Bus.Publish(new PaymentSettled(orderId, correlationId));
        Assert.NotNull(await saga.Exists(orderId, x => x.Paid));

        // No driver was ever requested (we are only in Paid), so a stray DriverUnavailable is ignored.
        await harness.Bus.Publish(new DriverUnavailable(orderId, correlationId));

        Assert.NotNull(await saga.Exists(orderId, x => x.Paid));
        Assert.False(await harness.Published.Any<RefundPayment>());
    }
}
