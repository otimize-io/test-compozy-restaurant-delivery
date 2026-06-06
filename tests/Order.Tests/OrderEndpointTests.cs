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

namespace Order.Tests;

/// <summary>
/// HTTP-level tests for the <see cref="OrderEndpoints"/> mappings, self-hosting a minimal
/// <see cref="WebApplication"/> on a loopback port with the EF Core in-memory provider and a MassTransit
/// in-memory bus (no Postgres / broker). Exercises <c>POST /api/orders</c> (201 + body, 400 validation)
/// and <c>GET /api/orders/{id}</c> (200 with status, 404).
/// </summary>
public class OrderEndpointTests : IAsyncLifetime
{
    private readonly string _databaseName = "endpoint-tests-" + Guid.NewGuid();
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        // A single, stable database name so every scoped DbContext (one per HTTP request) shares state.
        builder.Services.AddDbContext<OrderDbContext>(db =>
            db.UseInMemoryDatabase(_databaseName)
              .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        builder.Services.AddScoped<OrderService>();
        builder.Services.AddMassTransitTestHarness();

        _app = builder.Build();
        _app.MapOrderEndpoints();
        await _app.StartAsync();

        var address = _app.Urls.First();
        _client = new HttpClient { BaseAddress = new Uri(address) };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    private static PlaceOrderRequest ValidRequest() => new(
        ConsumerId: Guid.NewGuid(),
        RestaurantId: Guid.NewGuid(),
        Items: [new PlaceOrderLine(Guid.NewGuid(), "Burger", 1, 25m)]);

    [Fact]
    public async Task Post_orders_with_a_valid_cart_creates_an_order_in_Placed()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", ValidRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PlaceOrderResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.OrderId);
        Assert.Equal(OrderStatus.Placed, body.Status);
        Assert.False(string.IsNullOrWhiteSpace(body.CorrelationId));

        // The placement published OrderPlaced onto the bus.
        var harness = _app.Services.GetRequiredService<ITestHarness>();
        Assert.True(await harness.Published.Any<OrderPlaced>(p => p.Context!.Message.OrderId == body.OrderId));
    }

    [Fact]
    public async Task Post_orders_with_an_empty_cart_is_rejected()
    {
        var request = new PlaceOrderRequest(Guid.NewGuid(), Guid.NewGuid(), []);
        var response = await _client.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_orders_with_a_non_positive_quantity_is_rejected()
    {
        var request = new PlaceOrderRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            [new PlaceOrderLine(Guid.NewGuid(), "Free?", 0, 10m)]);
        var response = await _client.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_orders_returns_the_current_status()
    {
        var created = await _client.PostAsJsonAsync("/api/orders", ValidRequest());
        var placed = await created.Content.ReadFromJsonAsync<PlaceOrderResponse>();

        var response = await _client.GetAsync($"/api/orders/{placed!.OrderId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<OrderStatusResponse>();
        Assert.NotNull(status);
        Assert.Equal(placed.OrderId, status!.OrderId);
        Assert.Equal(OrderStatus.Placed, status.Status);
        Assert.Equal(25m, status.Total);
    }

    [Fact]
    public async Task Get_orders_for_an_unknown_id_returns_404()
    {
        var response = await _client.GetAsync($"/api/orders/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
