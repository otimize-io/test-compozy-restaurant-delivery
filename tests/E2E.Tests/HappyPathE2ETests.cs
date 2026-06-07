using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using E2E.Tests.Infrastructure;
using Microsoft.AspNetCore.SignalR.Client;
using RestaurantDelivery.Gateway.Hubs;

namespace E2E.Tests;

/// <summary>
/// The capstone end-to-end happy-path test (task_14.5). It hosts the order-flow services (Order, Payment,
/// Dispatch, Tracking) AND the Gateway in-process, all connected by a REAL RabbitMQ (Testcontainers) plus the
/// real datastores each service owns — NOT per-service mocks. The full journey is driven over HTTP THROUGH THE
/// GATEWAY (YARP), and a real SignalR client connected to the gateway hub verifies the live-update path. The
/// test asserts the order reaches <c>Delivered</c>, Tracking reports the delivered stage, and the hub emitted
/// <c>OrderStatusChanged</c> for each stage. It is slow by design (five hosts + four containers).
/// </summary>
[Trait("Category", "Integration")]
[Collection(StackCollection.Name)]
public sealed class HappyPathE2ETests(StackFixture stack)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task Full_journey_completes_and_status_propagates_through_the_gateway_and_hub()
    {
        // --- Arrange: host every service on real Kestrel ports over the shared broker + datastores. ---
        await using var order = new OrderFactory(stack);
        await using var payment = new PaymentFactory(stack);
        await using var dispatch = new DispatchFactory(stack);
        await using var tracking = new TrackingFactory(stack);

        await order.StartAsync();
        await payment.StartAsync();
        await dispatch.StartAsync();
        await tracking.StartAsync();

        // The gateway proxies to the just-bound service addresses (the gateway IS in the HTTP path).
        await using var gateway = new GatewayFactory(
            stack, order.BaseAddress, payment.BaseAddress, tracking.BaseAddress);
        using var client = await gateway.StartAsync();

        // --- Connect a real SignalR client to the gateway hub and subscribe before placing the order. ---
        var received = new ConcurrentQueue<StatusChanged>();
        await using var connection = new HubConnectionBuilder()
            .WithUrl($"{gateway.BaseAddress}hubs/orders")
            .Build();
        connection.On<StatusChanged>(OrdersHub.StatusChangedMethod, msg => received.Enqueue(msg));
        await connection.StartAsync();

        // --- Act: place the order through the gateway (X-Demo-Role exercises the role switcher). ---
        client.DefaultRequestHeaders.Add("X-Demo-Role", "consumer");
        var place = await client.PostAsJsonAsync("/api/orders", new
        {
            consumerId = Guid.NewGuid(),
            restaurantId = Guid.NewGuid(),
            items = new[] { new { itemId = Guid.NewGuid(), name = "Pizza", quantity = 2, unitPrice = 30m } },
            restaurantLocation = new { lat = -23.561, lng = -46.656 },
        }, Json);
        Assert.Equal(HttpStatusCode.Created, place.StatusCode);
        var placed = await place.Content.ReadFromJsonAsync<PlacedOrder>(Json);
        var orderId = placed!.OrderId;
        Assert.NotEqual(Guid.Empty, orderId);

        await connection.InvokeAsync("Subscribe", orderId);

        // Payment is async: the saga publishes CapturePayment, Payment records it, then settles on callback.
        await WaitUntilStatusAsync(client, orderId, "AwaitingPayment");

        // Settle the payment via the mock webhook (through the gateway → Payment). Payment then publishes
        // PaymentSettled over the REAL broker; the saga (in Order) consumes it and advances to Paid.
        var settle = await client.PostAsJsonAsync(
            "/api/payments/callback", new { orderId, outcome = "settle" }, Json);
        Assert.Equal(HttpStatusCode.Accepted, settle.StatusCode);
        await WaitUntilStatusAsync(client, orderId, "Paid");

        // Restaurant accepts → ready, then driver picks up → delivers — all driven through the gateway HTTP
        // endpoints. The real accept/ready/pickup/deliver endpoints publish their lifecycle event AND flush the
        // EF bus outbox, so the genuine cross-service machinery runs: the saga advances, Dispatch auto-assigns a
        // seeded driver on OrderReady→DriverRequested, Tracking projects each stage, and the gateway fans
        // OrderStatusChanged out over SignalR.
        SetRole(client, "restaurant");
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync($"/api/orders/{orderId}/accept", null)).StatusCode);
        await WaitUntilStatusAsync(client, orderId, "Preparing");

        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync($"/api/orders/{orderId}/ready", null)).StatusCode);
        await WaitUntilStatusAsync(client, orderId, "DriverAssigned"); // Dispatch auto-assigned a seeded driver.

        SetRole(client, "driver");
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync($"/api/orders/{orderId}/pickup", null)).StatusCode);
        await WaitUntilStatusAsync(client, orderId, "PickedUp");

        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync($"/api/orders/{orderId}/deliver", null)).StatusCode);
        var finalStatus = await WaitUntilStatusAsync(client, orderId, "Delivered");

        // --- Assert: the order is Delivered, Tracking agrees, and the hub pushed every stage. ---
        Assert.Equal("Delivered", finalStatus);

        // Tracking's projected status (proxied through the gateway) reports the delivered stage (5).
        var trackingStage = await WaitUntilTrackingStageAsync(client, orderId, 5);
        Assert.Equal(5, trackingStage);

        // The hub pushed an OrderStatusChanged for each forward stage (1..5). Live updates may coalesce
        // duplicate same-stage pushes (e.g. OrderPlaced+PaymentSettled both map to 1), so assert the set of
        // stages observed includes every forward stage rather than an exact count.
        var stages = await WaitForStagesAsync(received, [1, 2, 3, 4, 5]);
        Assert.Equal([1, 2, 3, 4, 5], stages.Where(s => s <= 5).Distinct().OrderBy(s => s).ToArray());

        // Every push carried this order's id and a non-empty stage name.
        Assert.All(received, m =>
        {
            Assert.Equal(orderId, m.OrderId);
            Assert.False(string.IsNullOrWhiteSpace(m.StageName));
        });

        await connection.StopAsync();
    }

    /// <summary>
    /// Polls <c>GET /api/orders/{id}</c> through the gateway until it reports the expected status. The Order
    /// service serializes <c>OrderStatus</c> as its numeric enum value, so the comparison is by ordinal.
    /// </summary>
    private static async Task<string> WaitUntilStatusAsync(HttpClient client, Guid orderId, string expected)
    {
        var expectedValue = OrderStatusValues.Of(expected);
        var deadline = DateTime.UtcNow + Timeout;
        int? last = null;
        while (DateTime.UtcNow < deadline)
        {
            var response = await client.GetAsync($"/api/orders/{orderId}");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var body = await response.Content.ReadFromJsonAsync<OrderStatusDto>(Json);
                last = body!.Status;
                if (last == expectedValue)
                {
                    return expected;
                }
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Order {orderId} never reached '{expected}' (last status ordinal: {last}).");
    }

    /// <summary>Polls Tracking's status read (proxied through the gateway) until it reports the expected stage.</summary>
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

    /// <summary>Waits until every expected stage has been observed on the hub, then returns the observed stages.</summary>
    private static async Task<int[]> WaitForStagesAsync(ConcurrentQueue<StatusChanged> received, int[] expected)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (DateTime.UtcNow < deadline)
        {
            var observed = received.Select(m => m.Stage).ToHashSet();
            if (expected.All(observed.Contains))
            {
                return received.Select(m => m.Stage).ToArray();
            }

            await Task.Delay(250);
        }

        var got = string.Join(",", received.Select(m => m.Stage).Distinct().OrderBy(s => s));
        throw new TimeoutException($"Hub did not emit every stage [{string.Join(",", expected)}]; observed [{got}].");
    }

    /// <summary>Sets the demo role header so the gateway role-switcher attaches the matching seeded identity.</summary>
    private static void SetRole(HttpClient client, string role)
    {
        client.DefaultRequestHeaders.Remove("X-Demo-Role");
        client.DefaultRequestHeaders.Add("X-Demo-Role", role);
    }

    private sealed record PlacedOrder(
        [property: JsonPropertyName("orderId")] Guid OrderId,
        [property: JsonPropertyName("correlationId")] string CorrelationId);

    private sealed record OrderStatusDto(
        [property: JsonPropertyName("orderId")] Guid OrderId,
        [property: JsonPropertyName("status")] int Status,
        [property: JsonPropertyName("total")] decimal Total);

    private sealed record TrackingStatusDto(
        [property: JsonPropertyName("orderId")] Guid OrderId,
        [property: JsonPropertyName("stage")] int Stage,
        [property: JsonPropertyName("stageName")] string StageName);

    private sealed record StatusChanged(
        [property: JsonPropertyName("orderId")] Guid OrderId,
        [property: JsonPropertyName("stage")] int Stage,
        [property: JsonPropertyName("stageName")] string StageName);
}
