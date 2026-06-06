using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using RestaurantDelivery.Contracts.Events;
using RestaurantDelivery.Order.Orders;
using RestaurantDelivery.Order.Saga;

namespace Order.Tests;

/// <summary>
/// Unit tests for <see cref="OrderService"/> (the logic behind <c>POST</c>/<c>GET /api/orders</c>), using
/// the EF Core in-memory provider for the <see cref="OrderDbContext"/> and MassTransit's in-memory harness
/// for the publish endpoint. These cover placement (persist + emit <see cref="OrderPlaced"/>) and the
/// status read; the Postgres-backed and broker-backed variants live in the integration tests.
/// </summary>
public class OrderServiceTests
{
    private static OrderDbContext NewDbContext(string name)
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseInMemoryDatabase(name)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new OrderDbContext(options);
    }

    private static async Task<(ITestHarness Harness, ServiceProvider Provider)> StartHarnessAsync()
    {
        var provider = new ServiceCollection()
            .AddMassTransitTestHarness()
            .BuildServiceProvider(validateScopes: true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return (harness, provider);
    }

    private static PlaceOrderRequest SampleRequest() => new(
        ConsumerId: Guid.NewGuid(),
        RestaurantId: Guid.NewGuid(),
        Items:
        [
            new PlaceOrderLine(Guid.NewGuid(), "Margherita", 2, 30m),
            new PlaceOrderLine(Guid.NewGuid(), "Soda", 1, 5m),
        ]);

    [Fact]
    public async Task PlaceAsync_persists_a_Placed_order_and_publishes_OrderPlaced()
    {
        var (harness, hp) = await StartHarnessAsync();
        await using var _ = hp;
        await using var db = NewDbContext(nameof(PlaceAsync_persists_a_Placed_order_and_publishes_OrderPlaced));
        var service = new OrderService(db, harness.Bus);
        var request = SampleRequest();

        var response = await service.PlaceAsync(request);

        Assert.Equal(OrderStatus.Placed, response.Status);
        Assert.NotEqual(Guid.Empty, response.OrderId);
        Assert.False(string.IsNullOrWhiteSpace(response.CorrelationId));

        // Total is computed server-side from the line subtotals: 2*30 + 1*5 = 65.
        var persisted = await db.Orders.SingleAsync(o => o.Id == response.OrderId);
        Assert.Equal(OrderStatus.Placed, persisted.Status);
        Assert.Equal(65m, persisted.Total);
        Assert.Equal(2, persisted.Items.Count);
        Assert.Equal(request.ConsumerId, persisted.ConsumerId);

        Assert.True(await harness.Published.Any<OrderPlaced>(p =>
            p.Context!.Message.OrderId == response.OrderId
            && p.Context.Message.CorrelationId == response.CorrelationId
            && p.Context.Message.Total == 65m
            && p.Context.Message.Items.Count == 2));
    }

    [Fact]
    public async Task GetStatusAsync_returns_null_for_an_unknown_order()
    {
        var (harness, hp) = await StartHarnessAsync();
        await using var _ = hp;
        await using var db = NewDbContext(nameof(GetStatusAsync_returns_null_for_an_unknown_order));
        var service = new OrderService(db, harness.Bus);

        Assert.Null(await service.GetStatusAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetStatusAsync_returns_Placed_snapshot_before_the_saga_records_a_state()
    {
        var (harness, hp) = await StartHarnessAsync();
        await using var _ = hp;
        await using var db = NewDbContext(nameof(GetStatusAsync_returns_Placed_snapshot_before_the_saga_records_a_state));
        var service = new OrderService(db, harness.Bus);

        var placed = await service.PlaceAsync(SampleRequest());
        var status = await service.GetStatusAsync(placed.OrderId);

        Assert.NotNull(status);
        Assert.Equal(placed.OrderId, status!.OrderId);
        Assert.Equal(OrderStatus.Placed, status.Status);
        Assert.Equal(65m, status.Total);
        Assert.Equal(placed.CorrelationId, status.CorrelationId);
    }

    [Fact]
    public async Task GetStatusAsync_maps_the_live_saga_state_to_an_OrderStatus()
    {
        var (harness, hp) = await StartHarnessAsync();
        await using var _ = hp;
        await using var db = NewDbContext(nameof(GetStatusAsync_maps_the_live_saga_state_to_an_OrderStatus));
        var service = new OrderService(db, harness.Bus);

        var placed = await service.PlaceAsync(SampleRequest());

        // Simulate the saga having advanced: write a saga instance row in the AwaitingPayment state.
        db.Set<OrderState>().Add(new OrderState
        {
            CorrelationId = placed.OrderId,
            CurrentState = nameof(OrderStateMachine.AwaitingPayment),
            OrderCorrelationId = placed.CorrelationId,
        });
        await db.SaveChangesAsync();

        var status = await service.GetStatusAsync(placed.OrderId);
        Assert.Equal(OrderStatus.AwaitingPayment, status!.Status);
    }
}
