using System.Text.Json;
using RestaurantDelivery.Contracts;
using RestaurantDelivery.Contracts.Events;

namespace Contracts.Tests;

public class JsonRoundTripTests
{
    [Fact]
    public void Every_message_round_trips_through_json_preserving_ids()
    {
        foreach (var message in MessageCatalog.Samples)
        {
            var type = message.GetType();
            var json = JsonSerializer.Serialize(message, type);
            var restored = (IOrderMessage)JsonSerializer.Deserialize(json, type)!;

            Assert.Equal(message.OrderId, restored.OrderId);
            Assert.Equal(message.CorrelationId, restored.CorrelationId);
        }
    }

    [Fact]
    public void OrderPlaced_round_trips_without_losing_line_items()
    {
        var original = new OrderPlaced(
            Guid.NewGuid(), "corr-42", Guid.NewGuid(), Guid.NewGuid(), 59.90m,
            [new OrderLine(Guid.NewGuid(), "Pizza", 2, 24.95m)]);

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<OrderPlaced>(json)!;

        Assert.Equal(original.Total, restored.Total);
        Assert.Single(restored.Items);
        Assert.Equal("Pizza", restored.Items[0].Name);
        Assert.Equal(2, restored.Items[0].Quantity);
        Assert.Equal(24.95m, restored.Items[0].UnitPrice);
    }
}
