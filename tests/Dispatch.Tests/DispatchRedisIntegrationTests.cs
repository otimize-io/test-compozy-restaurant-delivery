using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using RestaurantDelivery.Contracts;
using RestaurantDelivery.Contracts.Events;
using RestaurantDelivery.Dispatch.Drivers;
using RestaurantDelivery.Dispatch.Matching;
using RestaurantDelivery.Platform;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace Dispatch.Tests;

/// <summary>
/// Integration (task_09 Tests): a real Redis (Testcontainers, image <c>redis:7</c>) backs the driver
/// store and matcher; the consumer is driven through MassTransit's in-memory harness. Asserts that a
/// seeded available driver in Redis turns a <see cref="DriverRequested"/> into a <see cref="DriverAssigned"/>,
/// and that an empty store yields <see cref="DriverUnavailable"/>. Requires Docker.
/// </summary>
[Trait("Category", "Integration")]
public class DispatchRedisIntegrationTests : IAsyncLifetime
{
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
            .AddSingleton<IDriverStore, RedisDriverStore>()
            .AddSingleton<IDriverMatcher, NearestAvailableDriverMatcher>()
            .AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>()
            .AddMassTransitTestHarness(cfg => cfg.AddConsumer<DriverRequestedConsumer>())
            .BuildServiceProvider(validateScopes: true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return (harness, provider);
    }

    [Fact]
    public async Task Seeded_driver_in_Redis_yields_DriverAssigned()
    {
        var (harness, provider) = await StartHarnessAsync();
        await using var _ = provider;

        var store = provider.GetRequiredService<IDriverStore>();
        var driver = new Driver(Guid.NewGuid(), "Alice", new GeoPoint(-23.562, -46.657), Available: true);
        await store.UpsertAsync(driver);

        var orderId = Guid.NewGuid();
        await harness.Bus.Publish(new DriverRequested(orderId, "corr-int", new GeoPoint(-23.561, -46.656)));

        Assert.True(await harness.Consumed.Any<DriverRequested>());
        Assert.True(await harness.Published.Any<DriverAssigned>(p =>
        {
            var m = p.Context!.Message;
            return m.OrderId == orderId
                && m.CorrelationId == "corr-int"
                && m.DriverId == driver.Id
                && m.DriverName == "Alice";
        }));
    }

    [Fact]
    public async Task Empty_Redis_store_yields_DriverUnavailable()
    {
        var (harness, provider) = await StartHarnessAsync();
        await using var _ = provider;

        var orderId = Guid.NewGuid();
        await harness.Bus.Publish(new DriverRequested(orderId, "corr-empty", new GeoPoint(-23.561, -46.656)));

        Assert.True(await harness.Consumed.Any<DriverRequested>());
        Assert.True(await harness.Published.Any<DriverUnavailable>());
        Assert.False(await harness.Published.Any<DriverAssigned>());
    }
}
