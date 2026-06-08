using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using E2E.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace E2E.Tests.Gateway;

/// <summary>
/// Integration tests for the gateway's HTTP surface over the composed stack (task_14): the role switcher is
/// applied on real requests, an unknown route 404s, the pre-seeded identities are exposed, and the reconnect
/// resync path re-fetches the current status (proxied to Tracking) and matches the latest stage (task_14.4).
/// Shares the slow container fixture with the E2E flow.
/// </summary>
[Trait("Category", "Integration")]
[Collection(StackCollection.Name)]
public sealed class GatewayHttpTests(StackFixture stack)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private sealed class HostedStack : IAsyncDisposable
    {
        public OrderFactory Order { get; }
        public PaymentFactory Payment { get; }
        public DispatchFactory Dispatch { get; }
        public TrackingFactory Tracking { get; }
        public GatewayFactory Gateway { get; }
        public HttpClient Client { get; }

        private HostedStack(OrderFactory order, PaymentFactory payment, DispatchFactory dispatch,
            TrackingFactory tracking, GatewayFactory gateway, HttpClient client)
        {
            Order = order;
            Payment = payment;
            Dispatch = dispatch;
            Tracking = tracking;
            Gateway = gateway;
            Client = client;
        }

        public static async Task<HostedStack> StartAsync(StackFixture stack)
        {
            var order = new OrderFactory(stack);
            var payment = new PaymentFactory(stack);
            var dispatch = new DispatchFactory(stack);
            var tracking = new TrackingFactory(stack);
            await order.StartAsync();
            await payment.StartAsync();
            await dispatch.StartAsync();
            await tracking.StartAsync();

            var gateway = new GatewayFactory(stack, order.BaseAddress, payment.BaseAddress, tracking.BaseAddress);
            var client = await gateway.StartAsync();
            return new HostedStack(order, payment, dispatch, tracking, gateway, client);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Gateway.DisposeAsync();
            await Tracking.DisposeAsync();
            await Dispatch.DisposeAsync();
            await Payment.DisposeAsync();
            await Order.DisposeAsync();
        }
    }

    [Fact]
    public async Task An_unknown_route_returns_404_from_the_gateway()
    {
        await using var hosted = await HostedStack.StartAsync(stack);
        var response = await hosted.Client.GetAsync("/api/not-a-real-endpoint");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_gateway_exposes_the_three_pre_seeded_demo_identities()
    {
        await using var hosted = await HostedStack.StartAsync(stack);
        var roles = await hosted.Client.GetFromJsonAsync<DemoIdentityDto[]>("/api/demo/roles", Json);

        Assert.NotNull(roles);
        Assert.Equal(["consumer", "restaurant", "driver"], roles!.Select(r => r.Role).ToArray());
    }

    [Fact]
    public async Task X_Demo_Role_selects_the_seeded_identity_used_for_the_request()
    {
        await using var hosted = await HostedStack.StartAsync(stack);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/demo/whoami");
        request.Headers.Add("X-Demo-Role", "restaurant");

        var response = await hosted.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var who = await response.Content.ReadFromJsonAsync<DemoIdentityDto>(Json);
        Assert.Equal("restaurant", who!.Role);
    }

    [Fact]
    public async Task On_reconnect_the_current_status_is_re_fetched_and_matches_the_latest_stage()
    {
        // ADR-007 reconnect mitigation (task_14.4): a (re)connecting client recovers the current state via the
        // REST status read (proxied to Tracking) rather than replaying missed pushes. Drive the order to a known
        // stage, then prove a fresh GET /status — the resync call — reports that same latest stage.
        await using var hosted = await HostedStack.StartAsync(stack);
        var client = hosted.Client;

        var place = await client.PostAsJsonAsync("/api/orders", new
        {
            consumerId = Guid.NewGuid(),
            restaurantId = Guid.NewGuid(),
            items = new[] { new { itemId = Guid.NewGuid(), name = "Sushi", quantity = 1, unitPrice = 42m } },
            restaurantLocation = new { lat = -23.5, lng = -46.6 },
        }, Json);
        var placed = (await place.Content.ReadFromJsonAsync<PlacedOrder>(Json))!;
        var orderId = placed.OrderId;

        await WaitUntilStatusAsync(client, orderId, "AwaitingPayment");
        await client.PostAsJsonAsync("/api/payments/callback", new { orderId, outcome = "settle" }, Json);
        await WaitUntilStatusAsync(client, orderId, "Paid");

        // Advance to Preparing over the REAL broker (the shipped accept endpoint drops its event in the bus
        // outbox without a SaveChanges flush — see HappyPathE2ETests for the full note).
        var bus = hosted.Order.Services.GetRequiredService<MassTransit.IBus>();
        await bus.Publish(new RestaurantDelivery.Contracts.Events.OrderAccepted(orderId, placed.CorrelationId));
        await WaitUntilStatusAsync(client, orderId, "Preparing");

        // The order is at the Preparing stage (stage 2). The resync read must report stage 2.
        var stage = await WaitUntilTrackingStageAsync(client, orderId, 2);
        Assert.Equal(2, stage);
    }

    [Fact]
    public async Task Cors_preflight_from_the_spa_origin_is_allowed_on_a_proxied_route()
    {
        // A browser preflight (OPTIONS) from the SPA origin to a YARP-proxied route must be answered at the
        // gateway with the matching Access-Control-Allow-Origin — otherwise the SPA gets a CORS error.
        await using var hosted = await HostedStack.StartAsync(stack);
        using var preflight = new HttpRequestMessage(HttpMethod.Options, $"/api/orders/{Guid.NewGuid()}/status");
        preflight.Headers.Add("Origin", "http://localhost:4200");
        preflight.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await hosted.Client.SendAsync(preflight);

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowOrigin));
        Assert.Contains("http://localhost:4200", allowOrigin!);
    }

    [Fact]
    public async Task Cross_origin_request_from_the_spa_returns_the_allow_origin_header()
    {
        await using var hosted = await HostedStack.StartAsync(stack);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/demo/roles");
        request.Headers.Add("Origin", "http://localhost:4200");

        var response = await hosted.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowOrigin));
        Assert.Contains("http://localhost:4200", allowOrigin!);
    }

    private static async Task WaitUntilStatusAsync(HttpClient client, Guid orderId, string expected)
    {
        var expectedValue = OrderStatusValues.Of(expected);
        var deadline = DateTime.UtcNow + Timeout;
        while (DateTime.UtcNow < deadline)
        {
            var response = await client.GetAsync($"/api/orders/{orderId}");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var body = await response.Content.ReadFromJsonAsync<OrderStatusDto>(Json);
                if (body!.Status == expectedValue)
                {
                    return;
                }
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Order {orderId} never reached '{expected}'.");
    }

    private static async Task<int> WaitUntilTrackingStageAsync(HttpClient client, Guid orderId, int expectedStage)
    {
        var deadline = DateTime.UtcNow + Timeout;
        int? last = null;
        while (DateTime.UtcNow < deadline)
        {
            var response = await client.GetAsync($"/api/orders/{orderId}/status");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var body = await response.Content.ReadFromJsonAsync<TrackingStatusDto>(Json);
                last = body!.Stage;
                if (last == expectedStage)
                {
                    return last.Value;
                }
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Tracking for {orderId} never reached stage {expectedStage} (last: {last}).");
    }

    private sealed record DemoIdentityDto(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("userId")] Guid UserId,
        [property: JsonPropertyName("displayName")] string DisplayName);

    private sealed record PlacedOrder(
        [property: JsonPropertyName("orderId")] Guid OrderId,
        [property: JsonPropertyName("correlationId")] string CorrelationId);

    private sealed record OrderStatusDto([property: JsonPropertyName("status")] int Status);

    private sealed record TrackingStatusDto([property: JsonPropertyName("stage")] int Stage);
}
