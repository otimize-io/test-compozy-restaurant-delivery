using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RestaurantDelivery.Contracts;
using RestaurantDelivery.Contracts.Commands;
using RestaurantDelivery.Contracts.Events;
using RestaurantDelivery.Order.Orders;
using RestaurantDelivery.Order.Saga;
using Testcontainers.PostgreSql;

namespace Order.Tests;

/// <summary>
/// Integration tests for the restaurant (task_08), driver (task_10), and compensation (task_11) flows over a
/// real PostgreSQL (Testcontainers, image <c>postgres:16</c>) backing both the order aggregate and the
/// MassTransit EF saga repository. Each flow is driven purely through events delivered via the harness bus and
/// asserted against the persisted saga state. Requires Docker.
/// </summary>
[Trait("Category", "Integration")]
public class OrderFlowsPostgresIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ServiceProvider BuildProvider()
    {
        var connectionString = _postgres.GetConnectionString();
        return new ServiceCollection()
            .AddDbContext<OrderDbContext>(db => db.UseNpgsql(connectionString))
            .AddScoped<OrderService>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<OrderStateMachine, OrderState>()
                    .EntityFrameworkRepository(r =>
                    {
                        r.ConcurrencyMode = ConcurrencyMode.Optimistic;
                        r.ExistingDbContext<OrderDbContext>();
                    });
            })
            .BuildServiceProvider(validateScopes: true);
    }

    private static async Task EnsureSchemaAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    private string PersistedState(Guid orderId)
    {
        using var verify = new OrderDbContext(
            new DbContextOptionsBuilder<OrderDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);
        return verify.Set<OrderState>().AsNoTracking()
            .Where(s => s.CorrelationId == orderId)
            .Select(s => s.CurrentState)
            .Single();
    }

    private static IReadOnlyList<OrderLine> Cart() => [new OrderLine(Guid.NewGuid(), "Pizza", 1, 60m)];

    [Fact]
    public async Task Accept_then_ready_drives_a_persisted_order_through_both_transitions()
    {
        await using var provider = BuildProvider();
        await EnsureSchemaAsync(provider);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        var saga = harness.GetSagaStateMachineHarness<OrderStateMachine, OrderState>();

        var orderId = Guid.NewGuid();
        const string correlationId = "corr-rest-int";

        await harness.Bus.Publish(new OrderPlaced(orderId, correlationId, Guid.NewGuid(), Guid.NewGuid(), 60m, Cart()));
        Assert.NotNull(await saga.Exists(orderId, x => x.AwaitingPayment));
        await harness.Bus.Publish(new PaymentSettled(orderId, correlationId));
        Assert.NotNull(await saga.Exists(orderId, x => x.Paid));

        await harness.Bus.Publish(new OrderAccepted(orderId, correlationId));
        Assert.NotNull(await saga.Exists(orderId, x => x.Preparing));

        await harness.Bus.Publish(new OrderReady(orderId, correlationId));
        Assert.NotNull(await saga.Exists(orderId, x => x.AwaitingDriver));

        Assert.True(await harness.Published.Any<DriverRequested>(c => c.Context!.Message.OrderId == orderId));
        Assert.Equal(nameof(OrderStateMachine.AwaitingDriver), PersistedState(orderId));
    }

    [Fact]
    public async Task Pickup_then_deliver_drives_a_persisted_order_to_Delivered()
    {
        await using var provider = BuildProvider();
        await EnsureSchemaAsync(provider);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        var saga = harness.GetSagaStateMachineHarness<OrderStateMachine, OrderState>();

        var orderId = Guid.NewGuid();
        const string correlationId = "corr-driver-int";

        await harness.Bus.Publish(new OrderPlaced(orderId, correlationId, Guid.NewGuid(), Guid.NewGuid(), 60m, Cart()));
        Assert.NotNull(await saga.Exists(orderId, x => x.AwaitingPayment));
        await harness.Bus.Publish(new PaymentSettled(orderId, correlationId));
        Assert.NotNull(await saga.Exists(orderId, x => x.Paid));
        await harness.Bus.Publish(new OrderAccepted(orderId, correlationId));
        Assert.NotNull(await saga.Exists(orderId, x => x.Preparing));
        await harness.Bus.Publish(new OrderReady(orderId, correlationId));
        Assert.NotNull(await saga.Exists(orderId, x => x.AwaitingDriver));
        await harness.Bus.Publish(new DriverAssigned(orderId, correlationId, Guid.NewGuid(), "Alice", 12));
        Assert.NotNull(await saga.Exists(orderId, x => x.DriverAssignedState));

        await harness.Bus.Publish(new OrderPickedUp(orderId, correlationId));
        Assert.NotNull(await saga.Exists(orderId, x => x.PickedUp));

        await harness.Bus.Publish(new OrderDelivered(orderId, correlationId));
        Assert.NotNull(await saga.Exists(orderId, x => x.Delivered));

        Assert.True(await harness.Published.Any<OrderPickedUp>(c => c.Context!.Message.OrderId == orderId));
        Assert.True(await harness.Published.Any<OrderDelivered>(c => c.Context!.Message.OrderId == orderId));
        Assert.Equal(nameof(OrderStateMachine.Delivered), PersistedState(orderId));
    }

    [Fact]
    public async Task Compensation_no_driver_refunds_and_terminates_at_NoDriverRefunded()
    {
        await using var provider = BuildProvider();
        await EnsureSchemaAsync(provider);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        var saga = harness.GetSagaStateMachineHarness<OrderStateMachine, OrderState>();

        var orderId = Guid.NewGuid();
        const string correlationId = "corr-comp-int";

        await harness.Bus.Publish(new OrderPlaced(orderId, correlationId, Guid.NewGuid(), Guid.NewGuid(), 60m, Cart()));
        Assert.NotNull(await saga.Exists(orderId, x => x.AwaitingPayment));
        await harness.Bus.Publish(new PaymentSettled(orderId, correlationId));
        Assert.NotNull(await saga.Exists(orderId, x => x.Paid));
        await harness.Bus.Publish(new OrderAccepted(orderId, correlationId));
        Assert.NotNull(await saga.Exists(orderId, x => x.Preparing));
        await harness.Bus.Publish(new OrderReady(orderId, correlationId));
        Assert.NotNull(await saga.Exists(orderId, x => x.AwaitingDriver));

        // Inject the no-driver outcome: the saga refunds and terminates — no "paid but undelivered" orphan.
        await harness.Bus.Publish(new DriverUnavailable(orderId, correlationId));
        Assert.NotNull(await saga.Exists(orderId, x => x.NoDriverRefunded));

        Assert.True(await harness.Published.Any<RefundPayment>(c =>
            c.Context!.Message.OrderId == orderId && c.Context.Message.CorrelationId == correlationId));
        Assert.Equal(1, await harness.Published.SelectAsync<RefundPayment>().Count());
        Assert.Equal(nameof(OrderStateMachine.NoDriverRefunded), PersistedState(orderId));
    }
}
