using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using RestaurantDelivery.Contracts;
using RestaurantDelivery.Contracts.Events;
using RestaurantDelivery.Platform;
using RestaurantDelivery.Tracking.Consumers;
using RestaurantDelivery.Tracking.Projection;
using RestaurantDelivery.Tracking.Status;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace Tracking.Tests;

/// <summary>
/// Integration (task_12 Tests): a real Redis (Testcontainers, image <c>redis:7</c>) backs the projection
/// store; the consumers are driven through MassTransit's in-memory harness. Asserts that a sequence of
/// lifecycle events on the bus is projected into Redis and the status read returns the latest stage, and
/// that the refunded terminal stage is persisted and read back. Requires Docker.
/// </summary>
[Trait("Category", "Integration")]
public class TrackingRedisIntegrationTests : IAsyncLifetime
{
    private const string Corr = "corr-int";

    private readonly RedisContainer _redis = new RedisBuilder("redis:7").Build();
    private IConnectionMultiplexer _connection = null!;

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        _connection = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        await _redis.DisposeAsync();
    }

    private async Task<(ITestHarness Harness, ServiceProvider Provider)> StartHarnessAsync()
    {
        var provider = new ServiceCollection()
            .AddSingleton(_connection)
            .AddSingleton<ITrackingStore, RedisTrackingStore>()
            .AddSingleton<TrackingProjector>()
            .AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<OrderPlacedConsumer>();
                cfg.AddConsumer<OrderAcceptedConsumer>();
                cfg.AddConsumer<DriverAssignedConsumer>();
                cfg.AddConsumer<OrderPickedUpConsumer>();
                cfg.AddConsumer<OrderDeliveredConsumer>();
                cfg.AddConsumer<OrderRefundedConsumer>();
            })
            .BuildServiceProvider(validateScopes: true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return (harness, provider);
    }

    [Fact]
    public async Task Lifecycle_events_project_into_Redis_and_status_read_returns_latest_stage()
    {
        var (harness, provider) = await StartHarnessAsync();
        await using var _ = provider;

        var orderId = Guid.NewGuid();
        await harness.Bus.Publish(NewPlaced(orderId));
        await harness.Bus.Publish(new OrderAccepted(orderId, Corr));
        await harness.Bus.Publish(new DriverAssigned(orderId, Corr, Guid.NewGuid(), "Alice", 5));
        await harness.Bus.Publish(new OrderPickedUp(orderId, Corr));
        await harness.Bus.Publish(new OrderDelivered(orderId, Corr));

        Assert.True(await harness.Consumed.Any<OrderDelivered>());

        // Read back through the same status mapping the GET endpoint uses (resync on (re)connect).
        var store = provider.GetRequiredService<ITrackingStore>();
        var view = await store.GetAsync(orderId);
        Assert.NotNull(view);
        Assert.Equal(TrackingStage.Delivered, view!.Stage);

        var response = new OrderStatusResponse(view.OrderId, (int)view.Stage, view.Stage.ToString(), view.UpdatedAt);
        Assert.Equal(5, response.Stage);
        Assert.Equal("Delivered", response.StageName);
        Assert.Equal(orderId, response.OrderId);
    }

    [Fact]
    public async Task Refunded_event_persists_the_terminal_stage_in_Redis()
    {
        var (harness, provider) = await StartHarnessAsync();
        await using var _ = provider;

        var orderId = Guid.NewGuid();
        await harness.Bus.Publish(NewPlaced(orderId));
        await harness.Bus.Publish(new OrderRefunded(orderId, Corr));

        Assert.True(await harness.Consumed.Any<OrderRefunded>());

        var store = provider.GetRequiredService<ITrackingStore>();
        var view = await store.GetAsync(orderId);
        Assert.NotNull(view);
        Assert.Equal(TrackingStage.Refunded, view!.Stage);
    }

    [Fact]
    public async Task Unknown_order_has_no_view_in_Redis()
    {
        var (harness, provider) = await StartHarnessAsync();
        await using var _ = provider;

        var store = provider.GetRequiredService<ITrackingStore>();
        var view = await store.GetAsync(Guid.NewGuid());

        Assert.Null(view);
    }

    private static OrderPlaced NewPlaced(Guid orderId) => new(
        orderId, Corr, Guid.NewGuid(), Guid.NewGuid(), 42m,
        [new OrderLine(Guid.NewGuid(), "Pizza", 1, 42m)]);
}
