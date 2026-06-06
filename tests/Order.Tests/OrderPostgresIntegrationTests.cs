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
/// Integration tests (task_06 Tests): a real PostgreSQL (Testcontainers, image <c>postgres:16</c>) backs
/// both the order aggregate and the MassTransit EF saga repository. An order is placed and persisted, then
/// driven to <c>Paid</c> purely through events delivered via MassTransit's harness bus. Requires Docker.
/// </summary>
[Trait("Category", "Integration")]
public class OrderPostgresIntegrationTests : IAsyncLifetime
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
                        // Optimistic concurrency (xmin row-version); pessimistic mode is SQL-Server-only.
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

    [Fact]
    public async Task Placing_an_order_persists_it_to_postgres_and_publishes_OrderPlaced()
    {
        await using var provider = BuildProvider();
        await EnsureSchemaAsync(provider);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        PlaceOrderResponse placed;
        await using (var scope = provider.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<OrderService>();
            placed = await service.PlaceAsync(new PlaceOrderRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                [new PlaceOrderLine(Guid.NewGuid(), "Pizza", 2, 40m)],
                new GeoPointDto(-23.561, -46.656)));
        }

        // A fresh context proves durability (not just an in-memory tracking graph).
        await using (var verify = NewDbContext())
        {
            var row = await verify.Orders.SingleAsync(o => o.Id == placed.OrderId);
            Assert.Equal(80m, row.Total);
            Assert.Equal(OrderStatus.Placed, row.Status);
            Assert.Single(row.Items);
        }

        Assert.True(await harness.Published.Any<OrderPlaced>(p => p.Context!.Message.OrderId == placed.OrderId));

        OrderDbContext NewDbContext() =>
            new(new DbContextOptionsBuilder<OrderDbContext>()
                .UseNpgsql(_postgres.GetConnectionString()).Options);
    }

    [Fact]
    public async Task Saga_reaches_Paid_end_to_end_when_events_are_delivered_through_the_bus()
    {
        await using var provider = BuildProvider();
        await EnsureSchemaAsync(provider);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        var sagaHarness = harness.GetSagaStateMachineHarness<OrderStateMachine, OrderState>();

        var orderId = Guid.NewGuid();
        const string correlationId = "corr-integration";
        IReadOnlyList<OrderLine> items = [new OrderLine(Guid.NewGuid(), "Pasta", 1, 35m)];

        await harness.Bus.Publish(new OrderPlaced(
            orderId, correlationId, Guid.NewGuid(), Guid.NewGuid(), 35m, items));

        // The saga instance persisted to Postgres and sent CapturePayment.
        Assert.NotNull(await sagaHarness.Exists(orderId, x => x.AwaitingPayment));
        Assert.True(await harness.Published.Any<CapturePayment>(c => c.Context!.Message.OrderId == orderId));

        await harness.Bus.Publish(new PaymentSettled(orderId, correlationId));
        Assert.NotNull(await sagaHarness.Exists(orderId, x => x.Paid));

        // The persisted saga row in Postgres reads back in the Paid state.
        await using var verify = new OrderDbContext(
            new DbContextOptionsBuilder<OrderDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);
        var state = await verify.Set<OrderState>().AsNoTracking()
            .Where(s => s.CorrelationId == orderId)
            .Select(s => s.CurrentState)
            .SingleAsync();
        Assert.Equal(nameof(OrderStateMachine.Paid), state);
        Assert.Equal(OrderStatus.Paid, OrderStatusMap.FromSagaState(state));
    }
}
