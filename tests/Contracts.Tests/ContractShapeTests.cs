using RestaurantDelivery.Contracts;
using RestaurantDelivery.Contracts.Commands;
using RestaurantDelivery.Contracts.Events;

namespace Contracts.Tests;

public class ContractShapeTests
{
    private static IEnumerable<Type> ConcreteMessageTypes() =>
        typeof(OrderPlaced).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false }
                        && typeof(IOrderMessage).IsAssignableFrom(t));

    [Fact]
    public void Every_concrete_message_exposes_Guid_OrderId_and_string_CorrelationId()
    {
        var types = ConcreteMessageTypes().ToList();

        Assert.NotEmpty(types);
        foreach (var t in types)
        {
            Assert.Equal(typeof(Guid), t.GetProperty(nameof(IOrderMessage.OrderId))?.PropertyType);
            Assert.Equal(typeof(string), t.GetProperty(nameof(IOrderMessage.CorrelationId))?.PropertyType);
        }
    }

    [Fact]
    public void All_fifteen_expected_message_types_are_present()
    {
        var names = ConcreteMessageTypes().Select(t => t.Name).ToHashSet();
        string[] expected =
        [
            nameof(OrderPlaced), nameof(PaymentAccepted), nameof(PaymentSettled), nameof(PaymentDeclined),
            nameof(OrderAccepted), nameof(OrderReady), nameof(DriverRequested), nameof(DriverAssigned),
            nameof(DriverUnavailable), nameof(OrderPickedUp), nameof(OrderDelivered), nameof(OrderRefunded),
            nameof(CapturePayment), nameof(RefundPayment), nameof(RequestDriver),
        ];

        Assert.All(expected, name => Assert.Contains(name, names));
        Assert.Equal(expected.Length, MessageCatalog.Samples.Count);
    }

    [Fact]
    public void CapturePayment_carries_a_non_empty_idempotency_key()
    {
        var cmd = new CapturePayment(Guid.NewGuid(), "corr-1", 42.50m, "idem-123");

        Assert.False(string.IsNullOrWhiteSpace(cmd.IdempotencyKey));
    }

    [Fact]
    public void Messages_have_value_equality_and_printable_form()
    {
        var id = Guid.NewGuid();
        var a = new OrderDelivered(id, "x");
        var equal = new OrderDelivered(id, "x");
        var different = new OrderDelivered(Guid.NewGuid(), "x");

        Assert.Equal(a, equal);
        Assert.Equal(a.GetHashCode(), equal.GetHashCode());
        Assert.NotEqual(a, different);
        Assert.Contains(nameof(OrderDelivered), a.ToString());
    }
}
