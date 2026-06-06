using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using RestaurantDelivery.Contracts;
using RestaurantDelivery.Contracts.Events;
using RestaurantDelivery.Dispatch.Drivers;
using RestaurantDelivery.Dispatch.Matching;
using RestaurantDelivery.Platform;

namespace Dispatch.Tests;

/// <summary>
/// Drives <see cref="DriverRequestedConsumer"/> through MassTransit's fully in-memory test harness (no
/// broker / Docker), backed by an in-memory <see cref="IDriverStore"/>. Mirrors the harness style of
/// <c>Notification.Tests.NotificationConsumerHarnessTests</c>.
/// </summary>
public class DriverRequestedConsumerHarnessTests
{
    private static readonly GeoPoint Restaurant = new(-23.561, -46.656);

    private static async Task<(ITestHarness Harness, ServiceProvider Provider)> StartHarnessAsync(
        params Driver[] seedDrivers)
    {
        var store = new InMemoryDriverStore();
        foreach (var driver in seedDrivers)
        {
            await store.UpsertAsync(driver);
        }

        var provider = new ServiceCollection()
            .AddSingleton<IDriverStore>(store)
            .AddSingleton<IDriverMatcher, NearestAvailableDriverMatcher>()
            .AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>()
            .AddMassTransitTestHarness(cfg => cfg.AddConsumer<DriverRequestedConsumer>())
            .BuildServiceProvider(validateScopes: true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return (harness, provider);
    }

    [Fact]
    public async Task DriverRequested_with_available_driver_publishes_DriverAssigned()
    {
        var driver = new Driver(Guid.NewGuid(), "Alice", new GeoPoint(-23.562, -46.657), Available: true);
        var (harness, provider) = await StartHarnessAsync(driver);
        await using var _ = provider;

        var orderId = Guid.NewGuid();
        await harness.Bus.Publish(new DriverRequested(orderId, "corr-assign", Restaurant));

        Assert.True(await harness.Consumed.Any<DriverRequested>());
        Assert.False(await harness.Published.Any<DriverUnavailable>());
        Assert.True(await harness.Published.Any<DriverAssigned>(p =>
        {
            var m = p.Context!.Message;
            return m.OrderId == orderId
                && m.CorrelationId == "corr-assign"
                && m.DriverId == driver.Id
                && m.DriverName == "Alice";
        }));
    }

    [Fact]
    public async Task DriverRequested_with_no_available_driver_publishes_DriverUnavailable()
    {
        // Empty store → the deterministic "no driver" compensation trigger.
        var (harness, provider) = await StartHarnessAsync();
        await using var _ = provider;

        var orderId = Guid.NewGuid();
        await harness.Bus.Publish(new DriverRequested(orderId, "corr-none", Restaurant));

        Assert.True(await harness.Consumed.Any<DriverRequested>());
        Assert.False(await harness.Published.Any<DriverAssigned>());
        Assert.True(await harness.Published.Any<DriverUnavailable>(p =>
        {
            var m = p.Context!.Message;
            return m.OrderId == orderId && m.CorrelationId == "corr-none";
        }));
    }

    [Fact]
    public async Task DriverRequested_selects_the_nearer_driver_when_assigning()
    {
        var near = new Driver(Guid.NewGuid(), "Near", new GeoPoint(-23.562, -46.657), Available: true);
        var far = new Driver(Guid.NewGuid(), "Far", new GeoPoint(-24.500, -47.700), Available: true);
        var (harness, provider) = await StartHarnessAsync(far, near);
        await using var _ = provider;

        await harness.Bus.Publish(new DriverRequested(Guid.NewGuid(), "corr-nearer", Restaurant));

        Assert.True(await harness.Published.Any<DriverAssigned>(p => p.Context!.Message.DriverId == near.Id));
    }

    [Fact]
    public async Task Redelivered_DriverRequested_is_idempotent_and_assigns_once()
    {
        var driver = new Driver(Guid.NewGuid(), "Alice", new GeoPoint(-23.562, -46.657), Available: true);
        var (harness, provider) = await StartHarnessAsync(driver);
        await using var _ = provider;

        var request = new DriverRequested(Guid.NewGuid(), "corr-dup", Restaurant);
        await harness.Bus.Publish(request);
        await harness.Bus.Publish(request);

        Assert.True(await harness.Consumed.Any<DriverRequested>());
        // Same (OrderId, CorrelationId) → the idempotency store suppresses the duplicate publish.
        Assert.Equal(1, await harness.Published.SelectAsync<DriverAssigned>().Count());
    }
}
