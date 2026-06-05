using RestaurantDelivery.Contracts;
using RestaurantDelivery.Contracts.Commands;
using RestaurantDelivery.Contracts.Events;

namespace Contracts.Tests;

/// <summary>One concrete instance of every message type, used to exercise the whole contract set.</summary>
internal static class MessageCatalog
{
    private static readonly Guid Order = Guid.NewGuid();
    private const string Corr = "corr-sample";
    private static readonly GeoPoint Location = new(-23.55, -46.63);

    public static IReadOnlyList<IOrderMessage> Samples { get; } =
    [
        new OrderPlaced(Order, Corr, Guid.NewGuid(), Guid.NewGuid(), 59.90m,
            [new OrderLine(Guid.NewGuid(), "Pizza", 2, 29.95m)]),
        new PaymentAccepted(Order, Corr),
        new PaymentSettled(Order, Corr),
        new PaymentDeclined(Order, Corr, "insufficient funds"),
        new OrderAccepted(Order, Corr),
        new OrderReady(Order, Corr),
        new DriverRequested(Order, Corr, Location),
        new DriverAssigned(Order, Corr, Guid.NewGuid(), "Alex", 12),
        new DriverUnavailable(Order, Corr),
        new OrderPickedUp(Order, Corr),
        new OrderDelivered(Order, Corr),
        new OrderRefunded(Order, Corr),
        new CapturePayment(Order, Corr, 59.90m, "idem-1"),
        new RefundPayment(Order, Corr),
        new RequestDriver(Order, Corr, Location),
    ];
}
