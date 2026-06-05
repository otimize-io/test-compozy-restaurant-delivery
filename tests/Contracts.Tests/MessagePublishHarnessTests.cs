using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using RestaurantDelivery.Contracts;
using RestaurantDelivery.Contracts.Events;

namespace Contracts.Tests;

/// <summary>
/// Verifies a contract message can actually flow through MassTransit. Uses the fully in-memory test
/// harness — no broker / Docker required. Tagged Integration to mirror the task's test grouping.
/// </summary>
[Trait("Category", "Integration")]
public class MessagePublishHarnessTests
{
    [Fact]
    public async Task OrderPlaced_is_published_and_consumed_with_identical_payload()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(cfg => cfg.AddConsumer<OrderPlacedConsumer>())
            .BuildServiceProvider(validateScopes: true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sent = new OrderPlaced(
            Guid.NewGuid(), "corr-77", Guid.NewGuid(), Guid.NewGuid(), 30m,
            [new OrderLine(Guid.NewGuid(), "Burger", 1, 30m)]);

        await harness.Bus.Publish(sent);

        Assert.True(await harness.Consumed.Any<OrderPlaced>());

        var received = OrderPlacedConsumer.Received;
        Assert.NotNull(received);
        Assert.Equal(sent.OrderId, received!.OrderId);
        Assert.Equal(sent.CorrelationId, received.CorrelationId);
        Assert.Equal(sent.Total, received.Total);
        Assert.Equal(sent.Items.Count, received.Items.Count);
        Assert.Equal("Burger", received.Items[0].Name);
    }

    private sealed class OrderPlacedConsumer : IConsumer<OrderPlaced>
    {
        public static OrderPlaced? Received { get; private set; }

        public Task Consume(ConsumeContext<OrderPlaced> context)
        {
            Received = context.Message;
            return Task.CompletedTask;
        }
    }
}
