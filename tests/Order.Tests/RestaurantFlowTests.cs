using System.Net;
using System.Net.Http.Json;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using RestaurantDelivery.Contracts.Events;
using RestaurantDelivery.Order.Orders;
using RestaurantDelivery.Order.Restaurant;
using RestaurantDelivery.Order.Saga;

namespace Order.Tests;

/// <summary>
/// HTTP + saga tests for the restaurant flow (task_08): <c>POST /api/orders/{id}/accept</c>,
/// <c>POST /api/orders/{id}/ready</c>, and the <c>GET /api/restaurant/orders</c> queue. The app self-hosts on
/// loopback with the EF in-memory provider and a MassTransit in-memory harness that ALSO hosts the
/// <see cref="OrderStateMachine"/>, so a published accept/ready event actually advances the saga. The EF
/// saga-state row is seeded to the order's current state so the endpoint's status guard sees the same status.
/// </summary>
public class RestaurantFlowTests : IAsyncLifetime
{
    private readonly string _databaseName = "restaurant-tests-" + Guid.NewGuid();
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        builder.Services.AddDbContext<OrderDbContext>(db =>
            db.UseInMemoryDatabase(_databaseName)
              .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        builder.Services.AddScoped<RestaurantOrderService>();
        builder.Services.AddMassTransitTestHarness(cfg =>
            cfg.AddSagaStateMachine<OrderStateMachine, OrderState>());

        _app = builder.Build();
        _app.MapRestaurantEndpoints();
        // Starting the WebApplication also starts the in-process MassTransit harness (a hosted service),
        // so the bus + saga are running; calling Harness.Start() separately would double-start the host.
        await _app.StartAsync();

        _client = new HttpClient { BaseAddress = new Uri(_app.Urls.First()) };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    private ITestHarness Harness => _app.Services.GetRequiredService<ITestHarness>();

    private ISagaStateMachineTestHarness<OrderStateMachine, OrderState> Saga =>
        Harness.GetSagaStateMachineHarness<OrderStateMachine, OrderState>();

    /// <summary>
    /// Seeds an order row plus an EF saga-state row at the given saga state (so the endpoint guard reads it),
    /// and returns the order id. The harness saga is separately driven by published events.
    /// </summary>
    private async Task<Guid> SeedOrderAsync(
        string sagaState, decimal total = 60m, string? driverName = null, int? etaMinutes = null)
    {
        var orderId = Guid.NewGuid();
        var correlationId = "corr-" + orderId.ToString("N");
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        db.Orders.Add(new OrderEntity
        {
            Id = orderId,
            ConsumerId = Guid.NewGuid(),
            RestaurantId = Guid.NewGuid(),
            Total = total,
            CorrelationId = correlationId,
            Status = OrderStatus.Placed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Items = [],
        });
        db.Set<OrderState>().Add(new OrderState
        {
            CorrelationId = orderId,
            CurrentState = sagaState,
            OrderCorrelationId = correlationId,
            DriverName = driverName,
            EtaMinutes = etaMinutes,
        });
        await db.SaveChangesAsync();
        return orderId;
    }

    [Fact]
    public async Task Accept_on_a_Paid_order_publishes_OrderAccepted_and_saga_reaches_Preparing()
    {
        var orderId = Guid.NewGuid();
        const string correlationId = "corr-accept";
        // Drive the harness saga to Paid through the bus.
        await Harness.Bus.Publish(new OrderPlaced(
            orderId, correlationId, Guid.NewGuid(), Guid.NewGuid(), 60m,
            [new RestaurantDelivery.Contracts.OrderLine(Guid.NewGuid(), "Pizza", 1, 60m)]));
        Assert.NotNull(await Saga.Exists(orderId, x => x.AwaitingPayment));
        await Harness.Bus.Publish(new PaymentSettled(orderId, correlationId));
        Assert.NotNull(await Saga.Exists(orderId, x => x.Paid));

        // Seed the EF read model so the endpoint guard sees Paid.
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            db.Orders.Add(new OrderEntity
            {
                Id = orderId,
                ConsumerId = Guid.NewGuid(),
                RestaurantId = Guid.NewGuid(),
                Total = 60m,
                CorrelationId = correlationId,
                Status = OrderStatus.Paid,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Items = [],
            });
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsync($"/api/orders/{orderId}/accept", content: null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.True(await Harness.Published.Any<OrderAccepted>(p =>
            p.Context!.Message.OrderId == orderId && p.Context.Message.CorrelationId == correlationId));
        Assert.NotNull(await Saga.Exists(orderId, x => x.Preparing));
    }

    [Fact]
    public async Task Ready_on_a_Preparing_order_publishes_OrderReady_and_saga_reaches_AwaitingDriver()
    {
        var orderId = Guid.NewGuid();
        const string correlationId = "corr-ready";
        await Harness.Bus.Publish(new OrderPlaced(
            orderId, correlationId, Guid.NewGuid(), Guid.NewGuid(), 60m,
            [new RestaurantDelivery.Contracts.OrderLine(Guid.NewGuid(), "Pizza", 1, 60m)]));
        Assert.NotNull(await Saga.Exists(orderId, x => x.AwaitingPayment));
        await Harness.Bus.Publish(new PaymentSettled(orderId, correlationId));
        Assert.NotNull(await Saga.Exists(orderId, x => x.Paid));
        await Harness.Bus.Publish(new OrderAccepted(orderId, correlationId));
        Assert.NotNull(await Saga.Exists(orderId, x => x.Preparing));

        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            db.Orders.Add(new OrderEntity
            {
                Id = orderId,
                ConsumerId = Guid.NewGuid(),
                RestaurantId = Guid.NewGuid(),
                Total = 60m,
                CorrelationId = correlationId,
                Status = OrderStatus.Placed,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Items = [],
            });
            db.Set<OrderState>().Add(new OrderState
            {
                CorrelationId = orderId,
                CurrentState = nameof(OrderStateMachine.Preparing),
                OrderCorrelationId = correlationId,
            });
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsync($"/api/orders/{orderId}/ready", content: null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.True(await Harness.Published.Any<OrderReady>(p =>
            p.Context!.Message.OrderId == orderId && p.Context.Message.CorrelationId == correlationId));
        Assert.NotNull(await Saga.Exists(orderId, x => x.AwaitingDriver));
    }

    [Fact]
    public async Task Accept_on_a_non_Paid_order_returns_409()
    {
        // Seed an order whose saga state is still AwaitingPayment (not Paid).
        var orderId = await SeedOrderAsync(nameof(OrderStateMachine.AwaitingPayment));

        var response = await _client.PostAsync($"/api/orders/{orderId}/accept", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.False(await Harness.Published.Any<OrderAccepted>(p => p.Context!.Message.OrderId == orderId));
    }

    [Fact]
    public async Task Accept_on_an_unknown_order_returns_404()
    {
        var response = await _client.PostAsync($"/api/orders/{Guid.NewGuid()}/accept", content: null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Restaurant_board_groups_orders_through_the_full_lifecycle_with_driver_info()
    {
        var paid = await SeedOrderAsync(nameof(OrderStateMachine.Paid));
        var preparing = await SeedOrderAsync(nameof(OrderStateMachine.Preparing));
        var ready = await SeedOrderAsync(nameof(OrderStateMachine.AwaitingDriver));
        var assigned = await SeedOrderAsync(
            nameof(OrderStateMachine.DriverAssignedState), driverName: "Alice", etaMinutes: 7);
        var pickedUp = await SeedOrderAsync(
            nameof(OrderStateMachine.PickedUp), driverName: "Bruno", etaMinutes: 4);
        var delivered = await SeedOrderAsync(nameof(OrderStateMachine.Delivered));
        // An order before payment must NOT appear on the board.
        var awaitingPayment = await SeedOrderAsync(nameof(OrderStateMachine.AwaitingPayment));

        var queue = await _client.GetFromJsonAsync<RestaurantQueueResponse>("/api/restaurant/orders");

        Assert.NotNull(queue);
        Assert.Contains(queue!.New, i => i.OrderId == paid);
        Assert.Contains(queue.Cooking, i => i.OrderId == preparing);
        Assert.Contains(queue.AwaitingDriver, i => i.OrderId == ready);
        Assert.Contains(queue.OutForDelivery, i => i.OrderId == pickedUp);
        Assert.Contains(queue.Delivered, i => i.OrderId == delivered);

        // An assigned (not-yet-picked-up) order stays in AwaitingDriver and carries the matched driver + ETA.
        var assignedItem = Assert.Single(queue.AwaitingDriver, i => i.OrderId == assigned);
        Assert.Equal("Alice", assignedItem.DriverName);
        Assert.Equal(7, assignedItem.EtaMinutes);

        // The order before payment is not shown anywhere on the board.
        var all = queue.New.Concat(queue.Cooking).Concat(queue.AwaitingDriver)
            .Concat(queue.OutForDelivery).Concat(queue.Delivered).Select(i => i.OrderId).ToList();
        Assert.DoesNotContain(awaitingPayment, all);
    }
}
