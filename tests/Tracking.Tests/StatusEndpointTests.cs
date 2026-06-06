using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using RestaurantDelivery.Tracking.Projection;
using RestaurantDelivery.Tracking.Status;

namespace Tracking.Tests;

/// <summary>
/// HTTP-level tests for the <see cref="StatusEndpoints"/> mapping, self-hosting a minimal
/// <see cref="WebApplication"/> on a loopback port backed by an in-memory <see cref="ITrackingStore"/>
/// (no Redis / broker). Mirrors <c>Order.Tests.OrderEndpointTests</c>. Exercises
/// <c>GET /api/orders/{id}/status</c> (200 with the current stage; 404 for an unknown order) — the
/// resync read the gateway/SignalR client calls on (re)connect (ADR-007).
/// </summary>
public class StatusEndpointTests : IAsyncLifetime
{
    private readonly InMemoryTrackingStore _store = new();
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<ITrackingStore>(_store);

        _app = builder.Build();
        _app.MapStatusEndpoints();
        await _app.StartAsync();

        _client = new HttpClient { BaseAddress = new Uri(_app.Urls.First()) };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task Get_status_returns_the_current_stage()
    {
        var orderId = Guid.NewGuid();
        await _store.SaveAsync(new TrackingView(orderId, TrackingStage.Preparing, DateTimeOffset.UtcNow));

        var response = await _client.GetAsync($"/api/orders/{orderId}/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<OrderStatusResponse>();
        Assert.NotNull(status);
        Assert.Equal(orderId, status!.OrderId);
        Assert.Equal(2, status.Stage);
        Assert.Equal("Preparing", status.StageName);
    }

    [Fact]
    public async Task Get_status_for_an_unknown_order_returns_404()
    {
        var response = await _client.GetAsync($"/api/orders/{Guid.NewGuid()}/status");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
