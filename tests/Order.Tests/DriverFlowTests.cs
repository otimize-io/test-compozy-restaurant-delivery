using System.Net;
using System.Net.Http.Json;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using RestaurantDelivery.Contracts;
using RestaurantDelivery.Contracts.Events;
using RestaurantDelivery.Order.Driver;
using RestaurantDelivery.Order.Orders;
using RestaurantDelivery.Order.Saga;

namespace Order.Tests;

/// <summary>
/// HTTP + saga tests for the driver flow (task_10): <c>POST /api/orders/{id}/pickup</c>,
/// <c>POST /api/orders/{id}/deliver</c>, and the <c>GET /api/driver/assignments</c> read. The app self-hosts
/// with the EF in-memory provider and a MassTransit in-memory harness that also hosts the
/// <see cref="OrderStateMachine"/>, so a published pickup/deliver event actually advances the saga. The EF
/// saga-state row is seeded so the endpoint's status guard sees the same status.
/// </summary>
public class DriverFlowTests : IAsyncLifetime
{
    private readonly string _databaseName = "driver-tests-" + Guid.NewGuid();
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        builder.Services.AddDbContext<OrderDbContext>(db =>
            db.UseInMemoryDatabase(_databaseName)
              .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        builder.Services.AddScoped<DriverOrderService>();
        builder.Services.AddMassTransitTestHarness(cfg =>
            cfg.AddSagaStateMachine<OrderStateMachine, OrderState>());

        _app = builder.Build();
        _app.MapDriverEndpoints();
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

    private async Task SeedReadModelAsync(
        Guid orderId, string correlationId, string sagaState, Guid? driverId = null)
    {
        using var scope = _app.Services.CreateScope();
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
            CurrentState = sagaState,
            OrderCorrelationId = correlationId,
            DriverId = driverId,
            DriverName = driverId is null ? null : "Alice",
            EtaMinutes = driverId is null ? null : 12,
        });
        await db.SaveChangesAsync();
    }

    private async Task DriveSagaToDriverAssignedAsync(Guid orderId, string correlationId, Guid driverId)
    {
        await Harness.Bus.Publish(new OrderPlaced(
            orderId, correlationId, Guid.NewGuid(), Guid.NewGuid(), 60m,
            [new OrderLine(Guid.NewGuid(), "Pizza", 1, 60m)]));
        Assert.NotNull(await Saga.Exists(orderId, x => x.AwaitingPayment));
        await Harness.Bus.Publish(new PaymentSettled(orderId, correlationId));
        Assert.NotNull(await Saga.Exists(orderId, x => x.Paid));
        await Harness.Bus.Publish(new OrderAccepted(orderId, correlationId));
        Assert.NotNull(await Saga.Exists(orderId, x => x.Preparing));
        await Harness.Bus.Publish(new OrderReady(orderId, correlationId));
        Assert.NotNull(await Saga.Exists(orderId, x => x.AwaitingDriver));
        await Harness.Bus.Publish(new DriverAssigned(orderId, correlationId, driverId, "Alice", 12));
        Assert.NotNull(await Saga.Exists(orderId, x => x.DriverAssignedState));
    }

    [Fact]
    public async Task Pickup_on_a_DriverAssigned_order_publishes_OrderPickedUp_and_saga_reaches_PickedUp()
    {
        var orderId = Guid.NewGuid();
        const string correlationId = "corr-pickup";
        var driverId = Guid.NewGuid();
        await DriveSagaToDriverAssignedAsync(orderId, correlationId, driverId);
        await SeedReadModelAsync(orderId, correlationId, nameof(OrderStateMachine.DriverAssignedState), driverId);

        var response = await _client.PostAsync($"/api/orders/{orderId}/pickup", content: null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.True(await Harness.Published.Any<OrderPickedUp>(p =>
            p.Context!.Message.OrderId == orderId && p.Context.Message.CorrelationId == correlationId));
        Assert.NotNull(await Saga.Exists(orderId, x => x.PickedUp));
    }

    [Fact]
    public async Task Deliver_on_a_PickedUp_order_publishes_OrderDelivered_and_saga_reaches_Delivered()
    {
        var orderId = Guid.NewGuid();
        const string correlationId = "corr-deliver";
        var driverId = Guid.NewGuid();
        await DriveSagaToDriverAssignedAsync(orderId, correlationId, driverId);
        await Harness.Bus.Publish(new OrderPickedUp(orderId, correlationId));
        Assert.NotNull(await Saga.Exists(orderId, x => x.PickedUp));
        await SeedReadModelAsync(orderId, correlationId, nameof(OrderStateMachine.PickedUp), driverId);

        var response = await _client.PostAsync($"/api/orders/{orderId}/deliver", content: null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.True(await Harness.Published.Any<OrderDelivered>(p =>
            p.Context!.Message.OrderId == orderId && p.Context.Message.CorrelationId == correlationId));
        Assert.NotNull(await Saga.Exists(orderId, x => x.Delivered));
    }

    [Fact]
    public async Task Pickup_on_an_order_not_in_DriverAssigned_returns_409()
    {
        var orderId = Guid.NewGuid();
        const string correlationId = "corr-pickup-bad";
        // Order is only ReadyForPickup (AwaitingDriver), not yet assigned.
        await SeedReadModelAsync(orderId, correlationId, nameof(OrderStateMachine.AwaitingDriver));

        var response = await _client.PostAsync($"/api/orders/{orderId}/pickup", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.False(await Harness.Published.Any<OrderPickedUp>(p => p.Context!.Message.OrderId == orderId));
    }

    [Fact]
    public async Task Pickup_on_an_unknown_order_returns_404()
    {
        var response = await _client.PostAsync($"/api/orders/{Guid.NewGuid()}/pickup", content: null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Assignments_lists_an_assigned_order()
    {
        var assignedId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        await SeedReadModelAsync(assignedId, "corr-assigned", nameof(OrderStateMachine.DriverAssignedState), driverId);
        // An unassigned (awaiting-driver) order must not appear in the assignments list.
        await SeedReadModelAsync(Guid.NewGuid(), "corr-unassigned", nameof(OrderStateMachine.AwaitingDriver));

        var assignments = await _client.GetFromJsonAsync<List<DriverAssignmentItem>>("/api/driver/assignments");

        Assert.NotNull(assignments);
        var item = Assert.Single(assignments!, a => a.OrderId == assignedId);
        Assert.Equal(driverId, item.DriverId);
        Assert.Equal("Alice", item.DriverName);
        Assert.Equal(12, item.EtaMinutes);
        Assert.Equal(OrderStatus.DriverAssigned, item.Status);
    }
}
